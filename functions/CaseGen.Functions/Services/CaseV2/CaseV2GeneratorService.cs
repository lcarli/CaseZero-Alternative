using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using CaseGen.Functions.Models.CaseV2;
using CaseGen.Functions.Services.CaseV2.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;

namespace CaseGen.Functions.Services.CaseV2;

public interface ICaseV2GeneratorService
{
    Task<GenerateCaseV2Response> GenerateAsync(GenerateCaseV2Request request, CancellationToken ct = default);

    /// <summary>
    /// Same as <see cref="GenerateAsync(GenerateCaseV2Request, CancellationToken)"/> but reports the current phase
    /// to <paramref name="reporter"/> at every stage boundary so async job pollers can show progress.
    /// </summary>
    Task<GenerateCaseV2Response> GenerateAsync(GenerateCaseV2Request request, IJobPhaseReporter reporter, CancellationToken ct = default);
}

/// <summary>
/// Fine-grained orchestrator: each LLM call is a micro-task with a focused prompt.
/// Tasks that share a phase but are independent (per-suspect, per-asset, per-outcome,
/// per-question) run in parallel via <see cref="Task.WhenAll(IEnumerable{Task})"/>.
/// Final assembly + schema validation runs in deterministic C#.
/// </summary>
public class CaseV2GeneratorService : ICaseV2GeneratorService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly ILLMProvider _llm;
    private readonly IConfiguration _config;
    private readonly IAssetRenderingService _renderer;
    private readonly ICaseV2BlobPublisher _blobPublisher;
    private readonly MechanicalRulesBuilder _mechanicalRules;
    private readonly IConsistencyValidator _consistency;
    private readonly ISchemaErrorAutoFixer _autoFixer;
    private readonly ILogger<CaseV2GeneratorService> _logger;
    private readonly JSchema _v2Schema;
    private readonly string _v2SchemaJson;

    public CaseV2GeneratorService(
        ILLMProvider llm,
        IConfiguration config,
        IAssetRenderingService renderer,
        ICaseV2BlobPublisher blobPublisher,
        MechanicalRulesBuilder mechanicalRules,
        IConsistencyValidator consistency,
        ISchemaErrorAutoFixer autoFixer,
        ILogger<CaseV2GeneratorService> logger)
    {
        _llm = llm;
        _config = config;
        _renderer = renderer;
        _blobPublisher = blobPublisher;
        _mechanicalRules = mechanicalRules;
        _consistency = consistency;
        _autoFixer = autoFixer;
        _logger = logger;

        var schemaPath = ResolveSchemaPath();
        _v2SchemaJson = File.ReadAllText(schemaPath);
        _v2Schema = JSchema.Parse(_v2SchemaJson);
        _logger.LogInformation("CaseV2 generator loaded schema from {Path}", schemaPath);
    }

    public Task<GenerateCaseV2Response> GenerateAsync(GenerateCaseV2Request request, CancellationToken ct = default)
        => GenerateAsync(request, NullJobPhaseReporter.Instance, ct);

    public async Task<GenerateCaseV2Response> GenerateAsync(GenerateCaseV2Request request, IJobPhaseReporter reporter, CancellationToken ct = default)
    {
        var draft = new CaseDraft
        {
            CaseId = NormaliseCaseId(request.CaseId),
            Request = request
        };

        var stageMs = new Dictionary<string, double>();

        _logger.LogInformation("Generating v2 case {CaseId} (difficulty={Difficulty}, theme={Theme})",
            draft.CaseId, request.Difficulty, request.Theme);

        // Local helper: report phase to the job reporter, then time the stage body.
        // The reporter swallows its own exceptions so it never breaks the pipeline.
        async Task Stage(string name, Func<Task> body)
        {
            await reporter.ReportPhaseAsync(name, ct);
            await TimeStage(name, stageMs, body);
        }

        // === Phase 1: plot outline (sequential gate)
        await Stage("plotOutline", () => new PlotOutlineTask(_llm, _logger).RunAsync(draft, ct));

        // === Phase 2: suspect cards in parallel
        await Stage("suspectCards", async () =>
        {
            var task = new SuspectCardTask(_llm, _logger);
            var results = await Task.WhenAll(draft.SuspectStubs.Select(stub => task.RunAsync(draft, stub, ct)));
            draft.SuspectFull = results.ToList();
        });

        // === Phase 3: asset plan (gate)
        await Stage("assetPlan", () => new AssetPlanTask(_llm, _logger).RunAsync(draft, ct));

        // === Phase 4: asset cards + timeline + briefing in parallel
        await Stage("assetsAndTimelineAndBriefing", async () =>
        {
            var assetTask = new AssetCardTask(_llm, _logger);
            var assetsTask = Task.WhenAll(draft.AssetStubs.Select(stub => assetTask.RunAsync(draft, stub, ct)));

            var timelineTask = new TimelineTask(_llm, _logger).RunAsync(draft, ct);
            var briefingTask = new BriefingEmailTask(_llm, _logger).RunAsync(draft, ct);

            var assets = await assetsTask;
            await timelineTask;
            await briefingTask;
            draft.AssetFull = assets.ToList();

            // Propagate the request language onto every structured EvidenceDocument
            // so the renderer's chrome (classification band, page numbering,
            // letterhead labels, signature captions) localises correctly.
            var language = string.IsNullOrWhiteSpace(draft.Request.Language) ? "en-US" : draft.Request.Language!;
            foreach (var a in draft.AssetFull)
                if (a.BodyDoc is not null) a.BodyDoc.Language = language;
        });

        // === Phase 5: forensics plan (gate)
        await Stage("forensicsPlan", () => new ForensicsPlanTask(_llm, _logger).RunAsync(draft, ct));

        // === Phase 6: outcome details + initial emails in parallel
        await Stage("outcomesAndInitialEmails", async () =>
        {
            var outcomeTask = new ForensicOutcomeTask(_llm, _logger);
            var detailTasks = draft.ForensicStubs.Select(s => outcomeTask.RunAsync(draft, s, ct));
            var emailsTask = new InitialEmailsTask(_llm, _logger).RunAsync(draft, ct);

            var details = await Task.WhenAll(detailTasks);
            await emailsTask;

            foreach (var (full, asset, email) in details)
            {
                draft.ForensicFull.Add(full);
                if (asset is not null)
                {
                    if (asset.BodyDoc is not null)
                        asset.BodyDoc.Language = string.IsNullOrWhiteSpace(draft.Request.Language) ? "en-US" : draft.Request.Language!;
                    draft.ResultAssets.Add(asset);
                }
                if (email is not null) draft.ResultEmails.Add(email);
            }
        });

        // === Phase 7: deterministic mechanical rules (cuts the most common LLM mistake)
        await Stage("mechanicalRules", () =>
        {
            _mechanicalRules.Build(draft);
            // Mark Rookie cases: result assets/emails are revealed automatically by all_initial,
            // but we also mirror them as 'initial' so SolutionSkeleton & Solver see them as visible.
            if (string.Equals(draft.Metadata.RequiredRank, "Rookie", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var a in draft.ResultAssets) a.Visibility = "initial";
                foreach (var e in draft.ResultEmails) e.Visibility = "initial";
            }
            return Task.CompletedTask;
        });

        // === Phase 8: SolutionSkeleton (sequential, sees mechanical rules) + RulesTask LLM (narrative)
        await Stage("rulesAndSolutionSkeleton", async () =>
        {
            // SolutionSkeleton needs the pre-built reveal rules already in the draft so it knows
            // which hidden assets/emails are reachable. Run it first, then narrative rules in parallel
            // with Questions in the next phase.
            await new SolutionSkeletonTask(_llm, _logger).RunAsync(draft, ct);
            await new RulesTask(_llm, _logger).RunAsync(draft, ct);
        });

        // === Phase 9: questions (parallel) + explanation
        await Stage("questionsAndExplanation", async () =>
        {
            var qTask = new QuestionTask(_llm, _logger);
            var qTasks = draft.QuestionTopics.Select(t => qTask.RunAsync(draft, t, ct));
            var explanationTask = new ExplanationTask(_llm, _logger).RunAsync(draft, ct);

            var questions = await Task.WhenAll(qTasks);
            await explanationTask;
            draft.Questions = questions.ToList();
        });

        // === Phase 10: deterministic consistency pass — catches anything the LLM still drifted
        ConsistencyReport? consistencyReport = null;
        await Stage("consistency", () =>
        {
            consistencyReport = _consistency.Validate(draft);
            if (consistencyReport.AutoFixes.Count > 0)
                _logger.LogInformation("Consistency auto-fixed: {Fixes}", string.Join(" · ", consistencyReport.AutoFixes));
            if (consistencyReport.Errors.Count > 0)
                _logger.LogWarning("Consistency errors: {Errors}", string.Join(" · ", consistencyReport.Errors));
            return Task.CompletedTask;
        });

        // === Phase 11: assemble + JSON-schema validate (structural)
        var assembled = Assemble(draft);
        var json = assembled.ToJsonString(JsonOpts);

        // Local helper: schema + consistency errors combined. Refine accepts the
        // refined JSON based on this combined count, so consistency findings don't
        // silently get erased when refine fixes schema issues.
        List<string> ValidateAll(string j)
        {
            var all = Validate(j);
            if (consistencyReport is not null) all.AddRange(consistencyReport.Errors);
            return all;
        }

        var errors = ValidateAll(json);

        // === Phase 11b: deterministic auto-fix for common LLM ID-format slips
        //     (e.g. `asset_xxx` → `asset.xxx`). Runs only when there are errors.
        var autoFixes = new List<string>();
        if (errors.Count > 0)
        {
            await Stage("autoFixSchema", () =>
            {
                autoFixes.AddRange(_autoFixer.Fix(assembled));
                if (autoFixes.Count > 0)
                {
                    json = assembled.ToJsonString(JsonOpts);
                    var rev = ValidateAll(json);
                    _logger.LogInformation("AutoFixer cleared {Before}→{After} validation errors",
                        errors.Count, rev.Count);
                    errors = rev;
                }
                return Task.CompletedTask;
            });
        }

        // === Phase 12: red-team + solver (semantic validation) — run in parallel
        // Always run these — even on schema-error cases the reports help diagnose what went wrong.
        // Both the initial red-team pass and any reruns use the assembled JSON path so the
        // input to the LLM is identical across iterations.
        Tasks.RedTeamTask.Report? redTeam = null;
        Tasks.SolverTask.SolverResult? solver = null;
        await Stage("redTeamAndSolver", async () =>
        {
            var rt = new Tasks.RedTeamTask(_llm, _logger).RunOnAssembledAsync(json, ct);
            var sv = new Tasks.SolverTask(_llm, _logger).RunAsync(draft, ct);
            redTeam = await rt;
            solver = await sv;
        });

        // Rookie-aware verdict calibration: the LLM reviewer is intentionally strict, and
        // Rookie cases (by design simple, short, single-day) routinely earn 1-2 medium
        // nits even when fully playable. Promote needs_review → ok when (a) it's a Rookie
        // case, (b) zero high-severity findings, and (c) at most a small number of
        // medium-severity findings. Findings themselves are preserved for transparency.
        var isRookie = string.Equals(draft.Metadata.Difficulty, "Rookie", StringComparison.OrdinalIgnoreCase)
            || string.Equals(draft.Metadata.RequiredRank, "Rookie", StringComparison.OrdinalIgnoreCase);
        var rookieMaxMedium = int.TryParse(_config["CaseGenV2:RookieMaxMediumFindings"], out var cfgRm) && cfgRm >= 0
            ? cfgRm : 2;
        PromoteRookieVerdict(redTeam, isRookie, rookieMaxMedium);

        // === Phase 12b: iterative refine + red-team loop.
        // Goal: drive the case toward an `ok` verdict without schema errors.
        // Up to N refine attempts; each accepted refine triggers a fresh red-team audit.
        // Stops early on success, plateau, or regression (with revert).
        // For Rookie cases, default to a single refine pass — repeated refines on a Rookie
        // case tend to inject complexity that re-triggers `medium` findings without
        // converging on `ok`.
        var defaultMaxRefineIterations = isRookie ? 1 : 3;
        var maxRefineIterations = int.TryParse(_config["CaseGenV2:RefineMaxIterations"], out var cfgN) && cfgN > 0
            ? cfgN : defaultMaxRefineIterations;
        if (isRookie && int.TryParse(_config["CaseGenV2:RefineMaxIterationsRookie"], out var cfgRookieN) && cfgRookieN > 0)
            maxRefineIterations = cfgRookieN;
        var refineIterations = 0;
        var refineErrorsBefore = errors.Count;
        Tasks.RedTeamTask.Report? redTeamInitial = null;
        var verdictTrajectory = new List<string>();
        if (!string.IsNullOrEmpty(redTeam?.Verdict))
            verdictTrajectory.Add(redTeam!.Verdict);

        static int VerdictRank(string? v) => (v?.ToLowerInvariant()) switch
        {
            "ok" => 0,
            "needs_review" => 1,
            _ => 2 // reject or unknown
        };

        // Severity-weighted score: high counts much more than medium, medium much
        // more than low. Used for plateau / regression comparisons so swapping
        // 3 medium findings for 1 high finding registers as a regression.
        static int FindingsScore(IEnumerable<Tasks.RedTeamTask.Finding>? findings)
        {
            if (findings is null) return 0;
            var score = 0;
            foreach (var f in findings)
            {
                score += (f.Severity?.ToLowerInvariant()) switch
                {
                    "high" => 100,
                    "medium" => 10,
                    _ => 1
                };
            }
            return score;
        }

        while (refineIterations < maxRefineIterations)
        {
            // Refine if schema errors remain OR if red-team verdict is not `ok` AND has at least one non-low finding.
            var actionableFindings = redTeam?.Findings.Where(f =>
                !string.Equals(f.Severity, "low", StringComparison.OrdinalIgnoreCase)).ToList()
                ?? new List<Tasks.RedTeamTask.Finding>();
            var verdictLower = redTeam?.Verdict?.ToLowerInvariant();
            var shouldRefine = errors.Count > 0
                || ((verdictLower == "reject" || verdictLower == "needs_review") && actionableFindings.Count > 0);

            if (!shouldRefine) break;

            refineIterations++;
            if (redTeamInitial is null) redTeamInitial = redTeam;

            // Snapshot for potential revert on regression / plateau (semantic-only).
            var prevJson = json;
            var prevErrors = errors;
            var prevRedTeam = redTeam;
            var prevFindingScore = FindingsScore(redTeam?.Findings);
            var prevRank = VerdictRank(redTeam?.Verdict);
            var prevHadSchemaErrors = errors.Count > 0;
            var refinedAccepted = false;

            await Stage("refineCase", async () =>
            {
                var result = await new Tasks.RefineCaseTask(_llm, _logger)
                    .RunAsync(json, errors, actionableFindings, _v2SchemaJson, ct,
                        difficulty: draft.Metadata.Difficulty);

                if (result.Succeeded)
                {
                    var refined = result.RefinedJson!;
                    var revalidated = ValidateAll(refined);
                    if (revalidated.Count < errors.Count
                        || (errors.Count == 0 && !string.Equals(refined, json, StringComparison.Ordinal)))
                    {
                        _logger.LogInformation("Refine iteration {N} accepted (validation errors {Before}→{After})",
                            refineIterations, errors.Count, revalidated.Count);
                        json = refined;
                        errors = revalidated;
                        refinedAccepted = true;
                    }
                    else
                    {
                        _logger.LogWarning("Refine iteration {N} did NOT improve — stopping loop", refineIterations);
                    }
                }
                else
                {
                    _logger.LogWarning("RefineCaseTask iteration {N} returned no document: {Error}",
                        refineIterations, result.Error);
                }
            });

            if (!refinedAccepted) break;

            // Re-run RedTeam only once the schema is clean; otherwise spend the next iteration
            // on fixing the remaining schema errors first.
            if (errors.Count > 0) continue;

            await Stage("redTeamRerun", async () =>
            {
                redTeam = await new Tasks.RedTeamTask(_llm, _logger).RunOnAssembledAsync(json, ct);
                PromoteRookieVerdict(redTeam, isRookie, rookieMaxMedium);
                verdictTrajectory.Add(redTeam.Verdict);
                _logger.LogInformation("RedTeam rerun {N}: verdict={Verdict} findings={Count}",
                    refineIterations, redTeam.Verdict, redTeam.Findings.Count);
            });

            var newRank = VerdictRank(redTeam?.Verdict);
            var newFindingScore = FindingsScore(redTeam?.Findings);

            // Regression → revert and stop.
            if (newRank > prevRank || (newRank == prevRank && newFindingScore > prevFindingScore))
            {
                _logger.LogWarning("Refine iteration {N} regressed (verdict {PrevV}→{NewV}, score {PrevS}→{NewS}) — reverting",
                    refineIterations, prevRedTeam?.Verdict, redTeam?.Verdict, prevFindingScore, newFindingScore);
                json = prevJson;
                errors = prevErrors;
                redTeam = prevRedTeam;
                if (verdictTrajectory.Count > 0)
                    verdictTrajectory[verdictTrajectory.Count - 1] += " (reverted)";
                break;
            }

            // Success.
            if (newRank == 0) break;

            // Plateau — verdict didn't improve and severity-weighted score didn't decrease.
            if (newRank >= prevRank && newFindingScore >= prevFindingScore)
            {
                _logger.LogInformation("Refine iteration {N} plateaued — stopping loop", refineIterations);
                // For semantic-only iterations (no pre-existing schema errors), revert to
                // the snapshot since the LLM edits brought no measurable benefit.
                if (!prevHadSchemaErrors)
                {
                    json = prevJson;
                    errors = prevErrors;
                    redTeam = prevRedTeam;
                    if (verdictTrajectory.Count > 0)
                        verdictTrajectory[verdictTrajectory.Count - 1] += " (no gain)";
                }
                break;
            }
        }

        var refineAttempted = refineIterations > 0;
        var redTeamRerun = (redTeamInitial is not null
                && !string.Equals(redTeamInitial.Verdict, redTeam?.Verdict, StringComparison.OrdinalIgnoreCase))
            || verdictTrajectory.Count > 1;

        // === Phase 13: persist + render assets
        var outputPath = string.Empty;
        AssetRenderingReport? renderingReport = null;
        int blobsPublished = 0;
        if (errors.Count == 0 && request.WriteToDisk)
        {
            outputPath = WriteToDisk(draft.CaseId, json);
            // Materialise PDFs / images / sidecars next to case.json
            await Stage("renderAssets", async () =>
            {
                var basePath = ResolveCasesBasePath();
                var allAssets = draft.AssetFull.Concat(draft.ResultAssets).ToList();
                renderingReport = await _renderer.RenderAllAsync(draft.CaseId, basePath, allAssets, ct);
            });

            // === Phase 13b: publish to Blob Storage so the website (running on a different
            // host in production) picks the case up via CaseV2StorageService. No-op locally
            // when Azurite is off and no connection string is set.
            if (_blobPublisher.IsConfigured)
            {
                await Stage("publishToBlob", async () =>
                {
                    var assetsDir = Path.Combine(ResolveCasesBasePath(), draft.CaseId, "assets");
                    blobsPublished = await _blobPublisher.PublishAsync(draft.CaseId, outputPath, assetsDir, ct);
                });
            }
        }
        else if (errors.Count > 0)
        {
            _logger.LogWarning("Case {CaseId} failed schema validation: {Errors}", draft.CaseId, string.Join("; ", errors));
        }

        return new GenerateCaseV2Response
        {
            CaseId = draft.CaseId,
            OutputPath = outputPath,
            CaseJson = json,
            ValidationErrors = errors,
            StageLatencyMs = stageMs,
            AssetsRenderedPdfs = renderingReport?.PdfsWritten ?? 0,
            AssetsRenderedImages = renderingReport?.ImagesWritten ?? 0,
            AssetsSkipped = renderingReport?.Skipped ?? 0,
            AssetRenderingErrors = renderingReport?.Errors ?? new(),
            BlobsPublished = blobsPublished,
            AutoFixesApplied = autoFixes,
            RefineAttempted = refineAttempted,
            RefineErrorsBefore = refineErrorsBefore,
            RefineErrorsAfter = errors.Count,
            RefineIterations = refineIterations,
            RedTeamRerun = redTeamRerun,
            RedTeam = redTeam,
            RedTeamInitial = redTeamInitial,
            RedTeamVerdictTrajectory = verdictTrajectory,
            Solver = solver,
            Consistency = consistencyReport
        };
    }

    // ----------------------------------------------------------------------
    // Verdict calibration
    // ----------------------------------------------------------------------

    /// <summary>
    /// Promotes <c>needs_review</c> to <c>ok</c> for Rookie cases when the report
    /// contains zero high-severity findings and at most <paramref name="maxMedium"/>
    /// medium-severity findings. Original findings are preserved so downstream
    /// consumers (HTTP response, audit blob) still see what the reviewer flagged.
    /// No-op for non-Rookie cases or when the report is null.
    /// </summary>
    private void PromoteRookieVerdict(Tasks.RedTeamTask.Report? report, bool isRookie, int maxMedium)
    {
        if (!isRookie || report is null) return;
        if (!string.Equals(report.Verdict, "needs_review", StringComparison.OrdinalIgnoreCase)) return;

        var highCount = report.Findings.Count(f =>
            string.Equals(f.Severity, "high", StringComparison.OrdinalIgnoreCase));
        if (highCount > 0) return;

        var mediumCount = report.Findings.Count(f =>
            string.Equals(f.Severity, "medium", StringComparison.OrdinalIgnoreCase));
        if (mediumCount > maxMedium) return;

        _logger.LogInformation(
            "Promoting Rookie RedTeam verdict needs_review→ok (highCount={High}, mediumCount={Medium}, threshold={Threshold})",
            highCount, mediumCount, maxMedium);
        report.Verdict = "ok";
        report.Notes = string.IsNullOrEmpty(report.Notes)
            ? $"Promoted to ok for Rookie case (mediumCount={mediumCount} ≤ {maxMedium}, no high findings)."
            : report.Notes + $" | Promoted to ok for Rookie case (mediumCount={mediumCount} ≤ {maxMedium}, no high findings).";
    }

    // ----------------------------------------------------------------------
    // Assembly
    // ----------------------------------------------------------------------

    private JsonObject Assemble(CaseDraft d)
    {
        var assetsNode = new JsonArray();
        foreach (var a in d.AssetFull) assetsNode.Add(BuildAssetNode(d.CaseId, a));
        foreach (var a in d.ResultAssets) assetsNode.Add(BuildAssetNode(d.CaseId, a));

        var emailsNode = new JsonArray { BuildBriefingNode(d) };
        foreach (var e in d.FollowUpEmails) emailsNode.Add(BuildEmailNode(e, "initial"));
        foreach (var e in d.ResultEmails) emailsNode.Add(BuildEmailNode(e, "hidden"));

        var suspectsNode = new JsonArray();
        foreach (var s in d.SuspectFull)
        {
            suspectsNode.Add(new JsonObject
            {
                ["id"] = s.Id,
                ["name"] = s.Name,
                ["age"] = s.Age,
                ["occupation"] = s.Occupation,
                ["relationship"] = s.Relationship,
                ["motive"] = s.Motive,
                ["alibi"] = s.Alibi,
                ["alibiVerified"] = s.AlibiVerified,
                ["status"] = "suspect",
                ["background"] = s.Background,
                ["visibility"] = "initial"
            });
        }

        var timeline = JsonSerializer.SerializeToNode(d.Timeline, JsonOpts)!.AsArray();
        var temporalEvents = JsonSerializer.SerializeToNode(d.TemporalEvents, JsonOpts)!.AsArray();
        var rules = JsonSerializer.SerializeToNode(d.Rules, JsonOpts)!.AsArray();
        var forensicOutcomes = JsonSerializer.SerializeToNode(d.ForensicFull, JsonOpts)!.AsArray();

        var forensicsDefaults = new JsonObject
        {
            ["analysisTypes"] = JsonSerializer.SerializeToNode(d.AnalysisTypes, JsonOpts),
            ["noFindingsEmail"] = new JsonObject
            {
                ["template"] = "Detective,\n\nThe {{analysisType}} analysis on {{assetName}} did not reveal any actionable leads.\n\nForensics Lab",
                ["from"] = "Forensics Lab <lab@citypolice.gov>",
                ["subject"] = "Analysis Results — {{analysisType}} — No Findings"
            }
        };

        // Normalise solution partial credit weights to sum 1.0
        var partial = new SolutionPartialCredit();
        var total = partial.CulpritWeight + partial.EvidenceWeight + partial.AnalysisWeight + partial.QuestionsWeight;
        if (Math.Abs(total - 1.0) > 0.001)
        {
            partial.CulpritWeight /= total; partial.EvidenceWeight /= total;
            partial.AnalysisWeight /= total; partial.QuestionsWeight /= total;
        }

        var solution = new JsonObject
        {
            ["culpritId"] = d.CulpritId,
            ["requiredEvidenceIds"] = JsonSerializer.SerializeToNode(d.RequiredEvidenceIds, JsonOpts),
            ["requiredAnalysisIds"] = JsonSerializer.SerializeToNode(d.RequiredAnalysisIds, JsonOpts),
            ["questions"] = JsonSerializer.SerializeToNode(d.Questions, JsonOpts),
            ["explanation"] = d.Explanation,
            ["minimumScore"] = 0.7,
            ["maxAttempts"] = 3,
            ["partialCreditRules"] = JsonSerializer.SerializeToNode(partial, JsonOpts)
        };

        var metadata = JsonSerializer.SerializeToNode(d.Metadata, JsonOpts)!.AsObject();
        if (string.Equals(d.Metadata.RequiredRank, "Rookie", StringComparison.OrdinalIgnoreCase))
            metadata["unlockMode"] = "all_initial";
        else if (!metadata.ContainsKey("unlockMode"))
            metadata["unlockMode"] = "gated";
        if (!metadata.ContainsKey("estimatedDurationMinutes"))
            metadata["estimatedDurationMinutes"] = 90;

        var gameMetadata = new JsonObject
        {
            ["schemaVersion"] = "2.0",
            ["createdAt"] = DateTime.UtcNow.ToString("o"),
            ["tags"] = JsonSerializer.SerializeToNode(d.Metadata.Tags, JsonOpts),
            ["contentWarnings"] = new JsonArray(),
            ["localizations"] = new JsonArray { d.Request.Language ?? "en-US" },
            ["generation"] = new JsonObject
            {
                ["pipelineVersion"] = "v2-microtasks",
                ["model"] = _config["AzureFoundry:ModelName"] ?? "unknown",
                ["seed"] = d.Request.Seed ?? 0,
                ["bundleChecksum"] = string.Empty
            }
        };

        return new JsonObject
        {
            ["version"] = "2.0",
            ["caseId"] = d.CaseId,
            ["metadata"] = metadata,
            ["assets"] = assetsNode,
            ["emails"] = emailsNode,
            ["suspects"] = suspectsNode,
            ["timeline"] = timeline,
            ["temporalEvents"] = temporalEvents,
            ["rules"] = rules,
            ["forensicsDefaults"] = forensicsDefaults,
            ["forensicOutcomes"] = forensicOutcomes,
            ["solution"] = solution,
            ["gameMetadata"] = gameMetadata
        };
    }

    private static JsonNode BuildAssetNode(string caseId, EvidenceAsset a)
    {
        // Map asset.type to the file extension the renderer produced under cases/<id>/assets/.
        // Image extensions stay flexible (renderer sniffs and may produce png/jpg/webp).
        var ext = a.Type switch
        {
            "photo" or "image" => "png",
            "pdf" or "document" or "digital" => "pdf",
            "audio" => "mp3",
            _ => "bin"
        };
        var category = a.Category ?? a.Type switch
        {
            "photo" or "image" => "Document",
            "pdf" or "document" => "Document",
            "digital" => "Digital",
            "audio" => "Communication",
            _ => "Document"
        };
        return new JsonObject
        {
            ["id"] = a.Id,
            ["type"] = a.Type,
            ["title"] = a.Title,
            ["description"] = a.Description,
            ["uri"] = $"case://{caseId}/assets/{a.Id.Replace("asset.", "")}.{ext}",
            ["visibility"] = a.Visibility ?? "initial",
            ["category"] = category
        };
    }

    private static JsonNode BuildBriefingNode(CaseDraft d)
    {
        var initialAttachmentIds = d.AssetFull
            .Where(a => a.Visibility == "initial")
            .Take(2)
            .Select(a => a.Id)
            .ToList();
        return new JsonObject
        {
            ["id"] = "email.briefing",
            ["from"] = string.IsNullOrEmpty(d.Briefing.From) ? "Chief of Police <chief@citypolice.gov>" : d.Briefing.From,
            ["to"] = new JsonArray("Detective Alex Morgan <detective@citypolice.gov>"),
            ["subject"] = string.IsNullOrEmpty(d.Briefing.Subject) ? "URGENT: Case Assignment" : d.Briefing.Subject,
            ["body"] = d.Briefing.Body,
            ["sentAt"] = string.IsNullOrEmpty(d.Metadata.OpenedAt) ? DateTime.UtcNow.ToString("o") : d.Metadata.OpenedAt,
            ["priority"] = "urgent",
            ["attachments"] = new JsonArray(initialAttachmentIds.Select(id => (JsonNode?)id).ToArray()),
            ["visibility"] = "initial"
        };
    }

    private static JsonNode BuildEmailNode(EvidenceEmail e, string defaultVisibility)
    {
        return new JsonObject
        {
            ["id"] = e.Id,
            ["from"] = e.From,
            ["to"] = new JsonArray("Detective Alex Morgan <detective@citypolice.gov>"),
            ["subject"] = e.Subject,
            ["body"] = e.Body,
            ["sentAt"] = string.IsNullOrEmpty(e.SentAt) ? DateTime.UtcNow.ToString("o") : e.SentAt,
            ["priority"] = e.Priority ?? "normal",
            ["attachments"] = new JsonArray((e.Attachments ?? new()).Select(a => (JsonNode?)a).ToArray()),
            ["visibility"] = string.IsNullOrEmpty(e.Visibility) ? defaultVisibility : e.Visibility
        };
    }

    // ----------------------------------------------------------------------
    // Validation + utilities
    // ----------------------------------------------------------------------

    private List<string> Validate(string json)
    {
        var errors = new List<string>();
        try
        {
            var jObject = JObject.Parse(json);
            if (!jObject.IsValid(_v2Schema, out IList<string> messages))
                errors.AddRange(messages);
        }
        catch (Exception ex)
        {
            errors.Add($"JSON parse error: {ex.Message}");
        }
        return errors;
    }

    private string WriteToDisk(string caseId, string json)
    {
        var basePath = ResolveCasesBasePath();
        var caseDir = Path.Combine(basePath, caseId);
        Directory.CreateDirectory(caseDir);
        var path = Path.Combine(caseDir, "case.json");
        File.WriteAllText(path, json);
        _logger.LogInformation("Wrote case {CaseId} to {Path}", caseId, path);
        return path;
    }

    private string ResolveCasesBasePath()
    {
        var configured = _config["CaseGenV2:CasesBasePath"];
        if (!string.IsNullOrWhiteSpace(configured) && Directory.Exists(configured)) return configured!;

        // Walk up from the binary looking for the repo-root `cases/` directory
        // (identified by the presence of `case_001` or by being a sibling of `functions/`).
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 8 && dir is not null; i++)
        {
            var candidate = Path.Combine(dir.FullName, "cases");
            if (Directory.Exists(candidate) &&
                (Directory.Exists(Path.Combine(candidate, "case_001")) ||
                 Directory.Exists(Path.Combine(dir.FullName, "functions"))))
            {
                return candidate;
            }
            dir = dir.Parent;
        }

        // Fallback when nothing else exists. Use the OS temp dir so we never try
        // to write under the deployment-package mount (e.g. /home/site/wwwroot
        // on Linux Function Apps, which is read-only). Works on Windows, macOS
        // and Linux without per-environment app settings.
        var fallback = Path.Combine(Path.GetTempPath(), "casegen");
        Directory.CreateDirectory(fallback);
        return fallback;
    }

    private static string ResolveSchemaPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Schemas", "case.v2.schema.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Schemas", "case.v2.schema.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "schemas", "case.schema.json")
        };
        foreach (var c in candidates)
        {
            var full = Path.GetFullPath(c);
            if (File.Exists(full)) return full;
        }
        throw new FileNotFoundException("Could not locate case.v2 schema next to the build output.");
    }

    private static string NormaliseCaseId(string? caseId)
    {
        if (!string.IsNullOrWhiteSpace(caseId))
        {
            var s = caseId.Trim().ToLowerInvariant().Replace('-', '_');
            if (!s.StartsWith("case_")) s = "case_" + s;
            return s;
        }
        return $"case_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
    }

    private static async Task TimeStage(string name, Dictionary<string, double> sink, Func<Task> body)
    {
        var sw = Stopwatch.StartNew();
        try { await body(); }
        finally
        {
            // Accumulate latency so stages that fire multiple times during the
            // refine loop (refineCase, redTeamRerun) report total time spent.
            sink.TryGetValue(name, out var prev);
            sink[name] = prev + sw.Elapsed.TotalMilliseconds;
        }
    }
}
