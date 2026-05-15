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
                if (asset is not null) draft.ResultAssets.Add(asset);
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
        var errors = Validate(json);
        if (consistencyReport is not null)
            errors.AddRange(consistencyReport.Errors);

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
                    var rev = Validate(json);
                    if (consistencyReport is not null) rev.AddRange(consistencyReport.Errors);
                    _logger.LogInformation("AutoFixer cleared {Before}→{After} validation errors",
                        errors.Count, rev.Count);
                    errors = rev;
                }
                return Task.CompletedTask;
            });
        }

        // === Phase 12: red-team + solver (semantic validation) — run in parallel
        // Always run these — even on schema-error cases the reports help diagnose what went wrong.
        Tasks.RedTeamTask.Report? redTeam = null;
        Tasks.SolverTask.SolverResult? solver = null;
        await Stage("redTeamAndSolver", async () =>
        {
            var rt = new Tasks.RedTeamTask(_llm, _logger).RunAsync(draft, json, ct);
            var sv = new Tasks.SolverTask(_llm, _logger).RunAsync(draft, ct);
            redTeam = await rt;
            solver = await sv;
        });

        // === Phase 12b: refine via LLM if schema errors persist OR red-team rejected.
        var refineAttempted = false;
        var refineErrorsBefore = errors.Count;
        var highFindings = redTeam?.Findings.Where(f =>
            string.Equals(f.Severity, "high", StringComparison.OrdinalIgnoreCase)).ToList()
            ?? new List<Tasks.RedTeamTask.Finding>();
        var shouldRefine = errors.Count > 0
            || (string.Equals(redTeam?.Verdict, "reject", StringComparison.OrdinalIgnoreCase) && highFindings.Count > 0);

        if (shouldRefine)
        {
            await Stage("refineCase", async () =>
            {
                refineAttempted = true;
                var result = await new Tasks.RefineCaseTask(_llm, _logger)
                    .RunAsync(json, errors, highFindings, _v2SchemaJson, ct);

                if (result.Succeeded)
                {
                    var refined = result.RefinedJson!;
                    var revalidated = Validate(refined);
                    if (revalidated.Count < errors.Count)
                    {
                        _logger.LogInformation("Refine improved errors {Before}→{After} — accepting refined JSON",
                            errors.Count, revalidated.Count);
                        json = refined;
                        errors = revalidated;
                    }
                    else
                    {
                        _logger.LogWarning("Refine did NOT reduce error count ({Before}→{After}) — keeping pre-refine JSON",
                            errors.Count, revalidated.Count);
                    }
                }
                else
                {
                    _logger.LogWarning("RefineCaseTask returned no document: {Error}", result.Error);
                }
            });
        }

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
            RedTeam = redTeam,
            Solver = solver,
            Consistency = consistencyReport
        };
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

        // Fallback: create alongside the binary.
        var fallback = Path.Combine(AppContext.BaseDirectory, "cases");
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
        finally { sink[name] = sw.Elapsed.TotalMilliseconds; }
    }
}
