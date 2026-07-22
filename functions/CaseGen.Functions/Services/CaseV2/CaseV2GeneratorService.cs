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
        using var usageScope = CaseV2TokenUsageTracker.Begin();
        var draft = new CaseDraft
        {
            CaseId = NormaliseCaseId(request.CaseId),
            Request = request
        };
        var graphModeEnabled = CaseGraphFeatureOptions.From(_config).Enabled;

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
        var isRookie = string.Equals(draft.Metadata.Difficulty, "Rookie", StringComparison.OrdinalIgnoreCase)
            || string.Equals(draft.Metadata.RequiredRank, "Rookie", StringComparison.OrdinalIgnoreCase);
        var difficultyProfile = DifficultyProfileCatalog.Get(draft);
        LogStageValidation("blueprint", PipelineStageValidator.ValidateBlueprint(draft));

        // === Phase 2: suspect cards in parallel
        await Stage("suspectCards", async () =>
        {
            var task = new SuspectCardTask(_llm, _logger);
            var results = await Task.WhenAll(draft.SuspectStubs.Select(stub => task.RunAsync(draft, stub, ct)));
            draft.SuspectFull = results.ToList();
        });

        // === Phase 3: asset plan (gate)
        await Stage("assetPlan", () => new AssetPlanTask(_llm, _logger).RunAsync(draft, ct));
        EvidenceGraphCompiler.Compile(draft);
        LogStageValidation("evidencePlan", PipelineStageValidator.ValidateEvidencePlan(draft));

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
        LogStageValidation("evidenceContent", PipelineStageValidator.ValidateEvidenceContent(draft));

        // Rookie cases intentionally have no forensic workflow. Every clue needed
        // to solve them is authored directly into the initial evidence portfolio.
        if (!isRookie)
        {
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
        }
        else
        {
            await Stage("rookieInitialEvidence", () => new InitialEmailsTask(_llm, _logger).RunAsync(draft, ct));
        }
        EvidenceGraphCompiler.Compile(draft);
        LogStageValidation("forensics", PipelineStageValidator.ValidateForensics(draft));

        // === Phase 7: deterministic mechanical rules (cuts the most common LLM mistake)
        await Stage("mechanicalRules", () =>
        {
            _mechanicalRules.Build(draft);
            return Task.CompletedTask;
        });

        // === Phase 8: SolutionSkeleton (sequential, sees mechanical rules) + RulesTask LLM (narrative)
        await Stage("rulesAndSolutionSkeleton", async () =>
        {
            // SolutionSkeleton needs the pre-built reveal rules already in the draft so it knows
            // which hidden assets/emails are reachable. Run it first, then narrative rules in parallel
            // with Questions in the next phase.
            await new SolutionSkeletonTask(_llm, _logger).RunAsync(draft, ct);
            if (!isRookie)
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
        var assembled = AssembleForMode(draft, graphModeEnabled, out var parityReport);
        var json = assembled.ToJsonString(JsonOpts);
        var currentRepairIssues = new List<RepairIssue>();

        // Recompute deterministic and stage validation from the current draft every time.
        List<string> ValidateAll(string j)
        {
            var all = Validate(j);
            currentRepairIssues = all
                .Select(error => new RepairIssue("schema", error))
                .ToList();
            consistencyReport = _consistency.Validate(draft);
            all.AddRange(consistencyReport.Errors);
            currentRepairIssues.AddRange(consistencyReport.Errors.Select(error => new RepairIssue("consistency", error)));
            AddStageIssues("blueprint", PipelineStageValidator.ValidateBlueprint(draft), all, currentRepairIssues);
            AddStageIssues("evidencePlan", PipelineStageValidator.ValidateEvidencePlan(draft), all, currentRepairIssues);
            AddStageIssues("evidenceContent", PipelineStageValidator.ValidateEvidenceContent(draft), all, currentRepairIssues);
            AddStageIssues("forensics", PipelineStageValidator.ValidateForensics(draft), all, currentRepairIssues);
            AddStageIssues("locale", PipelineStageValidator.ValidateLocale(draft), all, currentRepairIssues);
            AddStageIssues("difficulty", PipelineStageValidator.ValidateDifficultyTopology(draft), all, currentRepairIssues);
            if (graphModeEnabled)
            {
                foreach (var issue in parityReport.Issues)
                {
                    var message = $"CaseGraph public parity failed for '{issue.Surface}': {issue.Message}";
                    all.Add(message);
                    currentRepairIssues.Add(new RepairIssue("graphProjection", message)
                    {
                        NodeId = $"public.{issue.Surface}",
                        ConstraintId = $"parity.{issue.Surface}"
                    });
                }
            }
            return all.Distinct(StringComparer.Ordinal).ToList();
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
            var rt = new Tasks.RedTeamTask(_llm, _logger).RunOnAssembledAsync(json, draft, ct);
            var sv = new Tasks.SolverTask(_llm, _logger).RunAsync(draft, ct);
            redTeam = await rt;
            solver = await sv;
        });

        // Rookie-aware verdict calibration: the LLM reviewer is intentionally strict, and
        // Rookie cases (by design simple, short, single-day) routinely earn 1-2 medium
        // nits even when fully playable. Promote needs_review → ok when (a) it's a Rookie
        // case, (b) zero high-severity findings, and (c) at most a small number of
        // medium-severity findings. Findings themselves are preserved for transparency.
        var rookieMaxMedium = int.TryParse(_config["CaseGenV2:RookieMaxMediumFindings"], out var cfgRm) && cfgRm >= 0
            ? cfgRm : 2;
        PromoteRookieVerdict(redTeam, isRookie, rookieMaxMedium);

        // === Phase 12b: owner-stage repair loop.
        // Red-team findings are routed back to the task that owns the invalid content;
        // downstream stages are then regenerated from the repaired draft.
        var maxRepairIterations = int.TryParse(_config["CaseGenV2:RepairMaxIterations"], out var cfgN) && cfgN > 0
            ? cfgN : difficultyProfile.MaxRepairIterations;
        var refineIterations = 0; // response field retained for public API compatibility
        var refineErrorsBefore = errors.Count;
        Tasks.RedTeamTask.Report? redTeamInitial = null;
        var verdictTrajectory = new List<string>();
        if (!string.IsNullOrEmpty(redTeam?.Verdict))
            verdictTrajectory.Add(redTeam!.Verdict);
        var repairCoordinator = new RepairCoordinator(_llm, _mechanicalRules, _logger);
        var totalRepairOperations = 0;
        var totalRepairPlateaus = 0;
        var bestDraft = CloneDraft(draft);
        var bestJson = json;
        var bestErrors = errors.ToList();
        var bestRedTeam = redTeam;
        var bestSolver = solver;
        var bestPenalty = QualityPenalty(errors, redTeam, solver);
        var plateauIterations = 0;
        string? repairInfrastructureError = null;

        while (refineIterations < maxRepairIterations)
        {
            var actionableFindings = redTeam?.Findings.Where(f => f.Blocking).ToList()
                ?? new List<Tasks.RedTeamTask.Finding>();
            var shouldRepair = errors.Count > 0
                || solver?.Correct != true
                || actionableFindings.Count > 0;
            if (!shouldRepair) break;

            refineIterations++;
            if (redTeamInitial is null) redTeamInitial = redTeam;
            try
            {
                await Stage("targetedRepair", async () =>
                {
                    await repairCoordinator.RepairAsync(
                        draft,
                        actionableFindings,
                        currentRepairIssues,
                        solver?.Correct != true,
                        ct);
                    totalRepairOperations += repairCoordinator.LastGranularReport.Operations.Count;
                    totalRepairPlateaus += repairCoordinator.LastGranularReport.Plateaus.Count;
                    if (graphModeEnabled && draft.CaseGraph.Origin == CaseGraphOrigin.Generated)
                        draft.CaseGraph.Origin = CaseGraphOrigin.TransitionalClueLadder;
                    EvidenceGraphCompiler.Compile(draft);
                    assembled = AssembleForMode(draft, graphModeEnabled, out parityReport);
                    json = EnforceDifficultyContract(assembled.ToJsonString(JsonOpts), isRookie);
                    errors = ValidateAll(json);
                    if (errors.Count > 0)
                    {
                        autoFixes.AddRange(_autoFixer.Fix(assembled));
                        json = EnforceDifficultyContract(assembled.ToJsonString(JsonOpts), isRookie);
                        errors = ValidateAll(json);
                    }

                    var redTeamTask = new Tasks.RedTeamTask(_llm, _logger)
                        .RunOnAssembledAsync(json, draft, ct);
                    var solverTask = new Tasks.SolverTask(_llm, _logger).RunAsync(draft, ct);
                    redTeam = await redTeamTask;
                    solver = await solverTask;
                    PromoteRookieVerdict(redTeam, isRookie, rookieMaxMedium);
                    verdictTrajectory.Add(redTeam.Verdict);
                    _logger.LogInformation(
                        "Targeted repair {Iteration}: verdict={Verdict}, findings={Findings}, solverScore={Score}, errors={Errors}",
                        refineIterations, redTeam.Verdict, redTeam.Findings.Count, solver.Score, errors.Count);
                });
            }
            catch (AggregateException exception) when (ContainsTransientNetworkFailure(exception))
            {
                repairInfrastructureError = "targeted repair stopped because the LLM endpoint was temporarily unreachable";
                _logger.LogError(exception, "{Message}; restoring the best snapshot", repairInfrastructureError);
                draft = CloneDraft(bestDraft);
                assembled = AssembleForMode(draft, graphModeEnabled, out parityReport);
                json = bestJson;
                errors = bestErrors.ToList();
                redTeam = bestRedTeam;
                solver = bestSolver;
                break;
            }
            var penalty = QualityPenalty(errors, redTeam, solver);
            if (penalty < bestPenalty)
            {
                bestPenalty = penalty;
                bestDraft = CloneDraft(draft);
                bestJson = json;
                bestErrors = errors.ToList();
                bestRedTeam = redTeam;
                bestSolver = solver;
                plateauIterations = 0;
            }
            else
            {
                plateauIterations++;
            }
            if (string.Equals(redTeam?.Verdict, "ok", StringComparison.OrdinalIgnoreCase)
                && solver?.Correct == true
                && errors.Count == 0)
                break;
            if (plateauIterations >= 2)
            {
                _logger.LogWarning("Targeted repair stopped after {Count} non-improving iterations", plateauIterations);
                break;
            }
        }

        if (QualityPenalty(errors, redTeam, solver) > bestPenalty)
        {
            draft = bestDraft;
            assembled = AssembleForMode(draft, graphModeEnabled, out parityReport);
            json = bestJson;
            errors = bestErrors;
            redTeam = bestRedTeam;
            solver = bestSolver;
            _logger.LogInformation("Restored the best targeted-repair snapshot");
        }

        json = EnforceDifficultyContract(json, isRookie);
        errors = ValidateAll(json);

        var remainingBlockingFindings = redTeam?.Findings.Count(f => f.Blocking) ?? 0;
        if (remainingBlockingFindings > 0)
        {
            errors.Add($"deterministic specialist quality gate rejected the case ({remainingBlockingFindings} blocking finding(s))");
        }
        if (solver?.Correct != true)
            errors.Add($"blind-solver quality gate rejected the case (score={solver?.Score ?? 0:0.####})");
        if (repairInfrastructureError is not null)
            errors.Add(repairInfrastructureError);

        var finalValidation = new CaseV2FinalValidator(_v2SchemaJson).Validate(
            draft,
            json,
            solver,
            redTeam,
            graphModeEnabled ? parityReport : null);
        errors.AddRange(finalValidation.Issues
            .Where(issue => issue.Blocking)
            .Select(issue => $"final {issue.Gate}/{issue.Code} [{issue.NodeId}]: {issue.Message}"));
        errors = errors.Distinct(StringComparer.Ordinal).ToList();

        var refineAttempted = refineIterations > 0;
        var redTeamRerun = (redTeamInitial is not null
                && !string.Equals(redTeamInitial.Verdict, redTeam?.Verdict, StringComparison.OrdinalIgnoreCase))
            || verdictTrajectory.Count > 1;

        // === Phase 13: persist + render assets
        var outputPath = string.Empty;
        AssetRenderingReport? renderingReport = null;
        int blobsPublished = 0;
        if (GenerationPersistenceGate.CanPersist(request.WriteToDisk, errors, finalValidation))
        {
            outputPath = WriteToDisk(draft.CaseId, json);
            if (graphModeEnabled)
            {
                var caseDirectory = Path.GetDirectoryName(outputPath)
                                    ?? throw new InvalidOperationException("Could not resolve generated case directory.");
                PrivateCaseArtifactPersistence.Write(caseDirectory, draft, finalValidation, solver);
            }
            // Materialise PDFs / images / sidecars next to case.json
            await Stage("renderAssets", async () =>
            {
                var basePath = ResolveCasesBasePath();
                // Use the FINAL json (post-refine) as the source of truth for which assets
                // should be rendered — refine can add new assets or remove old ones, and
                // those changes only land in `json`, never in `draft.AssetFull`. Reuse the
                // rich Body / BodyDoc from the draft when an id matches; otherwise materialise
                // a minimal EvidenceAsset from the JSON fields and let the renderer fall back
                // to description-as-body / description-as-prompt.
                var allAssets = MaterializeAssetsForRendering(draft, json);
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

        var tokenUsage = CaseV2TokenUsageTracker.Snapshot();
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
            CaseGraphEnabled = graphModeEnabled,
            PublicContractParity = parityReport,
            FinalValidation = finalValidation,
            RepairPlateauCount = totalRepairPlateaus,
            RepairOperationCount = totalRepairOperations,
            InputTokens = tokenUsage.InputTokens,
            OutputTokens = tokenUsage.OutputTokens,
            EvidenceLayoutDiversity = draft.AssetStubs
                .Select(asset => asset.LayoutHint)
                .Where(layout => !string.IsNullOrWhiteSpace(layout))
                .Distinct(StringComparer.Ordinal)
                .Count(),
            EvidenceLayouts = draft.AssetStubs
                .Select(asset => asset.LayoutHint)
                .Where(layout => !string.IsNullOrWhiteSpace(layout))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToList(),
            SpecialistFindingsByCategory = redTeam?.Findings
                .GroupBy(finding => finding.Area, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal)
                ?? new Dictionary<string, int>(StringComparer.Ordinal),
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

    private static void AddStageIssues(
        string stage,
        StageValidationReport report,
        ICollection<string> errors,
        ICollection<RepairIssue> repairIssues)
    {
        foreach (var error in report.Errors)
        {
            errors.Add(error);
            repairIssues.Add(new RepairIssue(stage, error));
        }
    }

    private static CaseDraft CloneDraft(CaseDraft draft)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(draft, JsonOpts);
        return System.Text.Json.JsonSerializer.Deserialize<CaseDraft>(json, JsonOpts)
            ?? throw new InvalidOperationException("Could not snapshot the case draft.");
    }

    private static int QualityPenalty(
        IReadOnlyCollection<string> errors,
        Tasks.RedTeamTask.Report? redTeam,
        Tasks.SolverTask.SolverResult? solver)
    {
        var high = redTeam?.Findings.Count(finding =>
            string.Equals(finding.Severity, "high", StringComparison.OrdinalIgnoreCase)) ?? 0;
        var medium = redTeam?.Findings.Count(finding =>
            string.Equals(finding.Severity, "medium", StringComparison.OrdinalIgnoreCase)) ?? 0;
        var verdictPenalty = redTeam?.Verdict?.ToLowerInvariant() switch
        {
            "ok" => 0,
            "needs_review" => 100,
            _ => 300
        };
        var solverPenalty = solver?.Correct == true ? 0 : 500;
        return errors.Count * 1000 + high * 300 + medium * 30 + verdictPenalty + solverPenalty;
    }

    private static bool ContainsTransientNetworkFailure(Exception exception)
    {
        if (exception is HttpRequestException or System.Net.Sockets.SocketException)
            return true;
        if (exception is AggregateException aggregate)
            return aggregate.InnerExceptions.Any(ContainsTransientNetworkFailure);
        return exception.InnerException is not null
            && ContainsTransientNetworkFailure(exception.InnerException);
    }

    private void LogStageValidation(string stage, StageValidationReport report)
    {
        if (report.Errors.Count > 0)
            _logger.LogWarning("{Stage} validation errors: {Errors}", stage, string.Join(" · ", report.Errors));
        if (report.Warnings.Count > 0)
            _logger.LogInformation("{Stage} validation warnings: {Warnings}", stage, string.Join(" · ", report.Warnings));
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
        foreach (var e in d.FollowUpEmails) emailsNode.Add(BuildEmailNode(d, e, "initial"));
        foreach (var e in d.ResultEmails) emailsNode.Add(BuildEmailNode(d, e, "hidden"));

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
            ["noFindingsEmail"] = BuildNoFindingsEmail(d)
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

    private JsonObject AssembleForMode(
        CaseDraft draft,
        bool graphModeEnabled,
        out PublicContractParityReport parityReport)
    {
        CaseGraphGenerationCoordinator.PrepareCanonicalGraph(draft, graphModeEnabled);
        var legacy = Assemble(draft);
        var graph = CaseGraphPublicContractCompiler.Compile(draft, legacy);
        parityReport = CaseGraphPublicContractCompiler.Compare(legacy, graph);
        return graphModeEnabled ? graph : legacy;
    }

    /// <summary>
    /// Build the asset list passed to the renderer using the FINAL case.json as the source
    /// of truth. The refine pass may add new assets (e.g. when the SmokingGun playbook tells
    /// the LLM to introduce a corroborating evidence asset) or rename existing ones; those
    /// changes only land in <paramref name="finalJson"/>, not in <see cref="CaseDraft"/>.
    /// We reuse the rich Body / BodyDoc fields from the draft whenever an id matches, and
    /// fall back to a minimal EvidenceAsset (built from the JSON fields) for assets that the
    /// refine introduced — the renderer's description-as-body fallback handles the latter.
    /// </summary>
    private List<EvidenceAsset> MaterializeAssetsForRendering(CaseDraft draft, string finalJson)
    {
        var draftById = draft.AssetFull.Concat(draft.ResultAssets)
            .GroupBy(a => a.Id, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var fallback = () => draftById.Values.ToList();

        try
        {
            using var doc = JsonDocument.Parse(finalJson);
            if (!doc.RootElement.TryGetProperty("assets", out var assetsArr)
                || assetsArr.ValueKind != JsonValueKind.Array)
            {
                _logger.LogWarning("MaterializeAssetsForRendering: final JSON has no assets[] array — falling back to draft list");
                return fallback();
            }

            var result = new List<EvidenceAsset>(assetsArr.GetArrayLength());
            var seenIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (var el in assetsArr.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.Object) continue;
                var id = el.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? string.Empty : string.Empty;
                if (string.IsNullOrEmpty(id) || !seenIds.Add(id)) continue;

                var type = el.TryGetProperty("type", out var typeEl) ? typeEl.GetString() ?? "pdf" : "pdf";
                var title = el.TryGetProperty("title", out var titleEl) ? titleEl.GetString() ?? string.Empty : string.Empty;
                var description = el.TryGetProperty("description", out var descEl) ? descEl.GetString() : null;
                var visibility = el.TryGetProperty("visibility", out var visEl) ? visEl.GetString() ?? "initial" : "initial";
                var category = el.TryGetProperty("category", out var catEl) ? catEl.GetString() : null;

                if (draftById.TryGetValue(id, out var draftAsset))
                {
                    // Adopt any title/description/visibility/category edits made during refine,
                    // but keep the rich Body / BodyDoc that earlier stages produced.
                    draftAsset.Type = type;
                    draftAsset.Title = title;
                    if (!string.IsNullOrEmpty(description)) draftAsset.Description = description;
                    draftAsset.Visibility = visibility;
                    if (!string.IsNullOrEmpty(category)) draftAsset.Category = category;
                    result.Add(draftAsset);
                }
                else
                {
                    _logger.LogInformation(
                        "Materialising refine-added asset for rendering: {Id} (type={Type}) — using description as body fallback",
                        id, type);
                    result.Add(new EvidenceAsset
                    {
                        Id = id,
                        Type = type,
                        Title = title,
                        Description = description,
                        Visibility = visibility,
                        Category = category
                    });
                }
            }

            // Surface assets that were in the draft but were dropped by refine — they no
            // longer appear in case.json so we must not render them either.
            var dropped = draftById.Keys.Except(seenIds, StringComparer.Ordinal).ToList();
            if (dropped.Count > 0)
            {
                _logger.LogInformation("Skipping {Count} draft asset(s) dropped during refine: {Ids}",
                    dropped.Count, string.Join(", ", dropped));
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MaterializeAssetsForRendering failed; falling back to draft-only list");
            return fallback();
        }
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
        var investigatorName = string.IsNullOrWhiteSpace(d.Blueprint.Locale.InvestigatorName)
            ? "Alex Morgan"
            : d.Blueprint.Locale.InvestigatorName;
        var investigatorEmail = string.IsNullOrWhiteSpace(d.Blueprint.Locale.InvestigatorEmail)
            ? "detective@citypolice.gov"
            : d.Blueprint.Locale.InvestigatorEmail;
        var agency = string.IsNullOrWhiteSpace(d.Blueprint.Locale.PoliceAgency)
            ? "Police Department"
            : d.Blueprint.Locale.PoliceAgency;
        return new JsonObject
        {
            ["id"] = "email.briefing",
            ["from"] = string.IsNullOrEmpty(d.Briefing.From) ? agency : d.Briefing.From,
            ["to"] = new JsonArray($"{investigatorName} <{investigatorEmail}>"),
            ["subject"] = string.IsNullOrEmpty(d.Briefing.Subject) ? "URGENT: Case Assignment" : d.Briefing.Subject,
            ["body"] = d.Briefing.Body,
            ["sentAt"] = string.IsNullOrEmpty(d.Metadata.OpenedAt) ? DateTime.UtcNow.ToString("o") : d.Metadata.OpenedAt,
            ["priority"] = "urgent",
            ["attachments"] = new JsonArray(initialAttachmentIds.Select(id => (JsonNode?)id).ToArray()),
            ["visibility"] = "initial"
        };
    }

    private static JsonNode BuildEmailNode(CaseDraft d, EvidenceEmail e, string defaultVisibility)
    {
        var investigatorName = string.IsNullOrWhiteSpace(d.Blueprint.Locale.InvestigatorName)
            ? "Alex Morgan"
            : d.Blueprint.Locale.InvestigatorName;
        var investigatorEmail = string.IsNullOrWhiteSpace(d.Blueprint.Locale.InvestigatorEmail)
            ? "detective@citypolice.gov"
            : d.Blueprint.Locale.InvestigatorEmail;
        return new JsonObject
        {
            ["id"] = e.Id,
            ["from"] = e.From,
            ["to"] = new JsonArray($"{investigatorName} <{investigatorEmail}>"),
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

    private static JsonObject BuildNoFindingsEmail(CaseDraft draft)
    {
        var agency = draft.Blueprint.Locale.PoliceAgency;
        var investigator = draft.Blueprint.Locale.InvestigatorName;
        return draft.Request.Language?.ToLowerInvariant() switch
        {
            "pt-br" => new JsonObject
            {
                ["template"] = $"{investigator},\n\nA análise {{{{analysisType}}}} de {{{{assetName}}}} não revelou elementos úteis para a investigação.\n\n{agency}",
                ["from"] = agency,
                ["subject"] = "Resultado da análise — {{analysisType}} — Sem achados"
            },
            "es-es" => new JsonObject
            {
                ["template"] = $"{investigator},\n\nEl análisis {{{{analysisType}}}} de {{{{assetName}}}} no reveló hallazgos útiles para la investigación.\n\n{agency}",
                ["from"] = agency,
                ["subject"] = "Resultado del análisis — {{analysisType}} — Sin hallazgos"
            },
            "fr-fr" => new JsonObject
            {
                ["template"] = $"{investigator},\n\nL'analyse {{{{analysisType}}}} de {{{{assetName}}}} n'a révélé aucun élément utile à l'enquête.\n\n{agency}",
                ["from"] = agency,
                ["subject"] = "Résultat de l'analyse — {{analysisType}} — Aucun résultat"
            },
            _ => new JsonObject
            {
                ["template"] = $"{investigator},\n\nThe {{{{analysisType}}}} analysis of {{{{assetName}}}} did not reveal any actionable findings.\n\n{agency}",
                ["from"] = agency,
                ["subject"] = "Analysis result — {{analysisType}} — No findings"
            }
        };
    }

    private static string EnforceDifficultyContract(string json, bool isRookie)
    {
        if (!isRookie) return json;

        var root = JsonNode.Parse(json)?.AsObject()
            ?? throw new JsonException("Refined case JSON must be an object.");

        if (root["forensicsDefaults"] is JsonObject defaults)
            defaults["analysisTypes"] = new JsonArray();
        root["forensicOutcomes"] = new JsonArray();

        if (root["solution"] is JsonObject solution)
        {
            solution["requiredAnalysisIds"] = new JsonArray();
            solution["partialCreditRules"] = new JsonObject
            {
                ["culpritWeight"] = 0.4,
                ["evidenceWeight"] = 0.2,
                ["analysisWeight"] = 0.2,
                ["questionsWeight"] = 0.2
            };
        }

        var culpritId = root["solution"]?["culpritId"]?.GetValue<string>();
        var culpritName = root["suspects"]?.AsArray()
            .OfType<JsonObject>()
            .FirstOrDefault(s => string.Equals(s["id"]?.GetValue<string>(), culpritId, StringComparison.Ordinal))?["name"]
            ?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(culpritName) && root["timeline"] is JsonArray timeline)
        {
            var nameForms = culpritName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Append(culpritName)
                .Where(form => form.Length >= 4)
                .ToList();
            for (var i = timeline.Count - 1; i >= 0; i--)
            {
                var eventText = timeline[i]?["event"]?.GetValue<string>();
                if (eventText is not null && nameForms.Any(form =>
                        eventText.Contains(form, StringComparison.OrdinalIgnoreCase)))
                {
                    timeline.RemoveAt(i);
                }
            }
        }

        if (root["rules"] is JsonArray rules)
        {
            for (var i = rules.Count - 1; i >= 0; i--)
            {
                if (rules[i]?["trigger"]?["type"]?.GetValue<string>() is { } triggerType
                    && string.Equals(triggerType, "forensics_complete", StringComparison.OrdinalIgnoreCase))
                {
                    rules.RemoveAt(i);
                }
            }
        }

        return root.ToJsonString(JsonOpts);
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
