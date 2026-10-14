using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using CaseGen.Functions.Models.CaseV2;
using CaseGen.Functions.Services.CaseV2.Tasks;
using Json.Schema;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

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
    private readonly ILogger<CaseV2GeneratorService> _logger;
    private readonly JsonSchema _v2Schema;
    private readonly string _v2SchemaJson;

    public CaseV2GeneratorService(
        ILLMProvider llm,
        IConfiguration config,
        IAssetRenderingService renderer,
        ICaseV2BlobPublisher blobPublisher,
        MechanicalRulesBuilder mechanicalRules,
        IConsistencyValidator consistency,
        ILogger<CaseV2GeneratorService> logger)
    {
        _llm = llm;
        _config = config;
        _renderer = renderer;
        _blobPublisher = blobPublisher;
        _mechanicalRules = mechanicalRules;
        _consistency = consistency;
        _logger = logger;

        var schemaPath = ResolveSchemaPath();
        _v2SchemaJson = File.ReadAllText(schemaPath);
        _v2Schema = StructuredJson.ParseSchema(_v2SchemaJson);
        _logger.LogInformation("CaseV2 generator loaded schema from {Path}", schemaPath);
    }

    public Task<GenerateCaseV2Response> GenerateAsync(GenerateCaseV2Request request, CancellationToken ct = default)
        => GenerateAsync(request, NullJobPhaseReporter.Instance, ct);

    public async Task<GenerateCaseV2Response> GenerateAsync(GenerateCaseV2Request request, IJobPhaseReporter reporter, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        GenerationRequestValidator.Validate(request);
        var draft = new CaseDraft
        {
            CaseId = GenerationRequestValidator.NormalizeCaseId(request.CaseId),
            Request = request
        };

        var stageMs = new Dictionary<string, double>();

        _logger.LogInformation("Generating v2 case {CaseId} (difficulty={Difficulty}, theme={Theme})",
            draft.CaseId, request.Difficulty, request.Theme);

        // Local helper: report phase to the job reporter, then time the stage body.
        // The reporter swallows its own exceptions so it never breaks the pipeline.
        async Task Stage(string name, Func<Task> body)
        {
            ct.ThrowIfCancellationRequested();
            await reporter.ReportPhaseAsync(name, ct);
            await TimeStage(name, stageMs, body);
            ct.ThrowIfCancellationRequested();
        }

        // === Phase 1: plot outline (sequential gate)
        await Stage("plotOutline", () => new PlotOutlineTask(_llm, _logger).RunAsync(draft, ct));
        await Stage("caseBible", () => new CaseBibleTask(_llm, _logger).RunAsync(draft, ct));

        // === Phase 2: suspect cards in parallel
        await Stage("suspectCards", async () =>
        {
            var task = new SuspectCardTask(_llm, _logger);
            var results = await Task.WhenAll(draft.SuspectStubs.Select(stub => task.RunAsync(draft, stub, ct)));
            draft.SuspectFull = results.ToList();
        });

        // === Phase 4: asset cards + timeline + briefing in parallel
        await Stage("assetsAndTimelineAndBriefing", async () =>
        {
            var assetTask = new AssetCardTask(_llm, _logger);
            var assetsTask = Task.WhenAll(draft.AssetStubs.Select(stub => assetTask.RunAsync(draft, stub, ct)));

            var timelineTask = new TimelineTask(_llm, _logger).RunAsync(draft, ct);
            var briefingTask = new BriefingEmailTask(_llm, _logger).RunAsync(draft, ct);

            await Task.WhenAll(assetsTask, timelineTask, briefingTask);
            var assets = await assetsTask;
            draft.AssetFull = assets.ToList();
            _renderer.PrepareForReview(draft.AssetFull);
        });

        // === Phase 6: outcome details + initial emails in parallel
        await Stage("outcomesAndInitialEmails", async () =>
        {
            var outcomeTask = new ForensicOutcomeTask(_llm, _logger);
            var detailTasks = draft.ForensicStubs.Select(s => outcomeTask.RunAsync(draft, s, ct));
            var emailsTask = new InitialEmailsTask(_llm, _logger).RunAsync(draft, ct);

            var detailsTask = Task.WhenAll(detailTasks);
            await Task.WhenAll(detailsTask, emailsTask);
            var details = await detailsTask;
            MicrotaskValidation.Unique(draft.AssetFull.Select(a => a.Id)
                .Concat(details.Where(d => d.asset is not null).Select(d => d.asset!.Id)), "assets/results");
            MicrotaskValidation.Unique(draft.FollowUpEmails.Select(e => e.Id).Append("email.briefing")
                .Concat(details.Where(d => d.email is not null).Select(d => d.email!.Id)), "emails/results");

            foreach (var (full, asset, email) in details)
            {
                draft.ForensicFull.Add(full);
                if (asset is not null) draft.ResultAssets.Add(asset);
                if (email is not null) draft.ResultEmails.Add(email);
            }
            _renderer.PrepareForReview(draft.ResultAssets);
        });

        // === Phase 7: deterministic mechanical rules (cuts the most common LLM mistake)
        await Stage("mechanicalRules", () =>
        {
            _mechanicalRules.Build(draft);
            // Mark Rookie cases: result assets/emails are revealed automatically by all_initial,
            // but we also mirror them as 'initial' for downstream review.
            if (string.Equals(draft.Metadata.RequiredRank, "Rookie", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var a in draft.ResultAssets) a.Visibility = "initial";
                foreach (var e in draft.ResultEmails) e.Visibility = "initial";
            }
            return Task.CompletedTask;
        });

        // Keep the historical phase ID; CaseBible already supplies the solution requirements.
        await Stage("rulesAndSolutionSkeleton", async () =>
        {
            await new RulesTask(_llm, _logger).RunAsync(draft, ct);
        });

        // === Phase 9: questions (parallel) + explanation
        await Stage("questionsAndExplanation", async () =>
        {
            var qTask = new QuestionTask(_llm, _logger);
            var qTasks = draft.QuestionTopics.Select(t => qTask.RunAsync(draft, t, ct));
            var explanationTask = new ExplanationTask(_llm, _logger).RunAsync(draft, ct);

            var questionsTask = Task.WhenAll(qTasks);
            await Task.WhenAll(questionsTask, explanationTask);
            var questions = await questionsTask;
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
        errors.AddRange(ValidateCanonical(draft, draft));
        if (consistencyReport is not null)
            errors.AddRange(consistencyReport.Errors);

        // === Phase 12: red-team + solver (semantic validation) — run in parallel
        // Always run these — even on schema-error cases the reports help diagnose what went wrong.
        Tasks.RedTeamTask.Report? redTeam = null;
        Tasks.SolverTask.SolverResult? solver = null;
        await Stage("redTeamAndSolver", async () =>
        {
            var rt = new Tasks.RedTeamTask(_llm, _logger).RunAsync(draft, json, ct);
            var sv = new Tasks.SolverTask(_llm, _logger).RunAsync(draft, ct);
            await Task.WhenAll(rt, sv);
            redTeam = await rt;
            solver = await sv;
        });

        var repair = new CaseRepairReport { BaseRevision = draft.Revision };
        var refineErrorsBefore = errors.Count;
        var initialQuality = CaseQualityGate.Evaluate(request, _blobPublisher.IsConfigured, errors,
            consistencyReport?.Errors ?? new List<string>(), redTeam, draft.Revision);
        var shouldRefine = redTeam?.Succeeded == true && initialQuality.HasBlockingIssues;

        if (shouldRefine)
        {
            RefineCaseTask.Result? result = null;
            await Stage("refineCase", async () =>
            {
                repair.Attempted = true;
                repair.OriginalBlockers = initialQuality.Issues.Where(i => i.Blocking)
                    .Select(i => $"{i.Code}: {i.Message}").ToList();
                result = await new RefineCaseTask(_llm, _logger).RunAsync(draft, errors, redTeam!, _v2SchemaJson, ct);
            });
            repair.ErrorCode = result!.ErrorCode;
            repair.ErrorMessage = result.Error;
            if (result.Candidate is { } candidate)
            {
                repair.CandidateRevision = candidate.Revision;
                await Stage("revalidateRepair", async () =>
                {
                    _renderer.PrepareForReview(candidate.AssetFull.Concat(candidate.ResultAssets));
                    var candidateConsistency = _consistency.Validate(candidate);
                    var candidateJson = Assemble(candidate).ToJsonString(JsonOpts);
                    var candidateErrors = Validate(candidateJson);
                    candidateErrors.AddRange(ValidateCanonical(candidate, draft));
                    candidateErrors.AddRange(candidateConsistency.Errors);
                    var before = draft.GetComponents();
                    repair.ChangedComponents = candidate.GetComponents()
                        .Where(component => !JsonNode.DeepEquals(before[component.Key], component.Value))
                        .Select(component => component.Key).ToList();
                    if (candidateErrors.Count > 0)
                    {
                        RejectRepair(string.Join(" · ", candidateErrors));
                        return;
                    }

                    var reviewTask = new RedTeamTask(_llm, _logger).RunAsync(candidate, candidateJson, ct);
                    var solverTask = new SolverTask(_llm, _logger).RunAsync(candidate, ct);
                    await Task.WhenAll(reviewTask, solverTask);
                    var candidateReview = await reviewTask;
                    var candidateQuality = CaseQualityGate.Evaluate(request, _blobPublisher.IsConfigured,
                        candidateErrors, candidateConsistency.Errors, candidateReview, candidate.Revision);
                    if (!candidateQuality.Approved)
                    {
                        RejectRepair(string.Join(" · ", candidateQuality.Issues.Where(i => i.Blocking)
                            .Select(i => $"{i.Code}: {i.Message}")));
                        return;
                    }

                    draft = candidate;
                    json = candidateJson;
                    errors = candidateErrors;
                    consistencyReport = candidateConsistency;
                    redTeam = candidateReview;
                    solver = await solverTask;
                    repair.Accepted = true;
                    _logger.LogInformation("Accepted canonical revision {Revision}; changed components: {Components}",
                        draft.Revision, string.Join(", ", repair.ChangedComponents));
                });
            }
        }

        void RejectRepair(string message)
        {
            repair.ErrorCode = "repair_rejected";
            repair.ErrorMessage = message;
            _logger.LogWarning("Canonical repair rejected; keeping revision {Revision}: {Errors}", draft.Revision, message);
        }

        var quality = new CaseQualityReport();
        await Stage("qualityGate", () =>
        {
            quality = CaseQualityGate.Evaluate(
                request, _blobPublisher.IsConfigured, errors,
                consistencyReport?.Errors ?? new List<string>(), redTeam, draft.Revision);
            if (repair.Attempted && !repair.Accepted)
                quality.Block(repair.ErrorCode ?? "repair_rejected", "refine",
                    repair.ErrorMessage ?? "The single repair pass did not resolve all blocking issues.");
            return Task.CompletedTask;
        });

        // Render in a nested workspace that local case discovery cannot see.
        var outputPath = string.Empty;
        AssetRenderingReport? renderingReport = null;
        CaseBundle? bundle = null;
        int blobsPublished = 0;
        if (quality.Approved && request.WriteToDisk)
        {
            string? workspace = null;
            var operationCode = "persistence_failed";
            var operationArea = "persistence";
            try
            {
                var basePath = ResolveCasesBasePath();
                workspace = Path.Combine(basePath, ".casegen-staging", Guid.NewGuid().ToString("N"));
                var stagedCaseDir = Path.Combine(workspace, draft.CaseId);
                var assetsDir = Path.Combine(stagedCaseDir, "assets");
                Directory.CreateDirectory(assetsDir);
                var stagedJsonPath = Path.Combine(stagedCaseDir, "case.json");
                await File.WriteAllTextAsync(stagedJsonPath, json, ct);

                operationCode = "asset_render_failed";
                operationArea = "rendering";
                await Stage("renderAssets", async () =>
                {
                    var allAssets = draft.AssetFull.Concat(draft.ResultAssets).ToList();
                    renderingReport = await _renderer.RenderAllAsync(draft.CaseId, workspace, allAssets, ct);
                    CaseQualityGate.CheckRendering(quality, renderingReport);
                });

                if (quality.Approved)
                {
                    operationCode = "bundle_invalid";
                    operationArea = "artifacts";
                    await Stage("verifyBundle", async () =>
                    {
                        bundle = await CaseBundle.CreateAsync(draft.CaseId, draft.Revision, stagedCaseDir, json,
                            draft.AssetFull.Concat(draft.ResultAssets), renderingReport!, ct);
                        json = bundle.CaseJson;
                        var packagingErrors = Validate(json);
                        errors.AddRange(packagingErrors);
                        foreach (var error in packagingErrors) quality.Block("schema_invalid", "artifacts", error);
                    });
                }

                if (quality.Approved && bundle is not null)
                {
                    operationCode = "persistence_failed";
                    operationArea = "persistence";
                    LocalCasePublication? publication = null;
                    await Stage("preparePublication", async () =>
                        publication = await LocalCasePublication.PrepareAsync(bundle, basePath, _logger, ct));
                    using var localPublication = publication!;

                    if (quality.PublicationRequested)
                    {
                        operationCode = "publication_failed";
                        operationArea = "publication";
                        await Stage("publishToBlob", async () =>
                        {
                            blobsPublished = await _blobPublisher.PublishAsync(bundle, ct);
                            if (blobsPublished <= 0)
                                throw new InvalidOperationException("Configured publisher did not publish the case.");
                            quality.Published = true;
                        });
                    }

                    operationCode = "persistence_failed";
                    operationArea = "persistence";
                    await Stage("persistCase", async () =>
                    {
                        outputPath = await localPublication.CommitAsync(ct);
                        quality.Persisted = true;
                        _logger.LogInformation("Persisted approved case {CaseId} to {Path}", draft.CaseId, outputPath);
                    });
                }
            }
            catch (CaseBundleValidationException ex)
            {
                if (bundle is not null) bundle.Report.Verified = false;
                quality.Block(ex.Code, operationArea, ex.Message, ex.Reference);
                _logger.LogError(ex, "Case {CaseId} bundle verification failed during {Area}", draft.CaseId, operationArea);
            }
            catch (CasePublicationConflictException ex)
            {
                quality.Block("publication_conflict", operationArea, ex.Message);
                _logger.LogWarning(ex, "Case {CaseId} publication conflicted during {Area}", draft.CaseId, operationArea);
            }
            catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
            {
                quality.Block(operationCode, operationArea, ex.Message);
                _logger.LogError(ex, "Case {CaseId} failed during {Area}", draft.CaseId, operationArea);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                      or Azure.RequestFailedException or HttpRequestException
                                      or TimeoutException or InvalidOperationException)
            {
                quality.Block(operationCode, operationArea, ex.Message);
                _logger.LogError(ex, "Case {CaseId} failed during {Area}", draft.CaseId, operationArea);
            }
            finally
            {
                CleanupWorkspace(workspace, quality.Persisted);
            }
        }

        if (!quality.Approved)
        {
            _logger.LogWarning("Quality gate blocked case {CaseId}: {Issues}",
                draft.CaseId, string.Join("; ", quality.Issues.Where(issue => issue.Blocking).Select(issue => $"{issue.Code}: {issue.Message}")));
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
            AutoFixesApplied = consistencyReport?.AutoFixes ?? new(),
            RefineAttempted = repair.Attempted,
            RefineErrorsBefore = refineErrorsBefore,
            RefineErrorsAfter = errors.Count,
            RedTeam = redTeam,
            Solver = solver,
            Consistency = consistencyReport,
            Quality = quality,
            Repair = repair,
            Bundle = bundle?.Report
        };
    }

    // ----------------------------------------------------------------------
    // Assembly
    // ----------------------------------------------------------------------

    private List<string> ValidateCanonical(CaseDraft candidate, CaseDraft baseline)
    {
        try
        {
            CaseDraftRepair.Validate(candidate, baseline, _v2SchemaJson);
            return new();
        }
        catch (Exception ex) when (ex is TaskContractException or JsonException or Newtonsoft.Json.JsonException)
        {
            _logger.LogWarning(ex, "Canonical component validation failed for revision {Revision}", candidate.Revision);
            return new() { $"Canonical components: {ex.Message}" };
        }
    }

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
        foreach (var outcome in forensicOutcomes.OfType<JsonObject>()) outcome.Remove("support");
        var questions = JsonSerializer.SerializeToNode(d.Questions, JsonOpts)!.AsArray();
        foreach (var question in questions.OfType<JsonObject>()) question.Remove("support");

        var noFindings = (d.Request.Language ?? "en-US") switch
        {
            "pt-BR" => (From: "Laboratório de Perícia", Subject: "Resultado da análise — {{analysisType}} — Sem achados",
                Body: "Detetive,\n\nA análise {{analysisType}} de {{assetName}} não revelou pistas úteis à investigação.\n\nLaboratório de Perícia"),
            "es-ES" => (From: "Laboratorio Forense", Subject: "Resultado del análisis — {{analysisType}} — Sin hallazgos",
                Body: "Detective,\n\nEl análisis {{analysisType}} de {{assetName}} no reveló pistas útiles para la investigación.\n\nLaboratorio Forense"),
            "fr-FR" => (From: "Laboratoire de police scientifique", Subject: "Résultats — {{analysisType}} — Aucun élément",
                Body: "Détective,\n\nL’analyse {{analysisType}} de {{assetName}} n’a révélé aucune piste exploitable.\n\nLaboratoire de police scientifique"),
            _ => (From: "Forensics Lab", Subject: "Analysis Results — {{analysisType}} — No Findings",
                Body: "Detective,\n\nThe {{analysisType}} analysis on {{assetName}} did not reveal any actionable leads.\n\nForensics Lab")
        };
        var forensicsDefaults = new JsonObject
        {
            ["analysisTypes"] = JsonSerializer.SerializeToNode(d.AnalysisTypes, JsonOpts),
            ["noFindingsEmail"] = new JsonObject
            {
                ["template"] = noFindings.Body,
                ["from"] = $"{noFindings.From} <lab@citypolice.gov>",
                ["subject"] = noFindings.Subject
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
            ["questions"] = questions,
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
        var category = a.Category ?? a.Type switch
        {
            "photo" or "image" => "Document",
            "pdf" or "document" => "Document",
            "digital" => "Digital",
            _ => "Document"
        };
        return new JsonObject
        {
            ["id"] = a.Id,
            ["type"] = a.Type,
            ["title"] = a.Title,
            ["description"] = a.Description,
            // Unrendered previews have provisional references, never guessed file extensions.
            ["uri"] = $"case://{caseId}/assets/{a.Id["asset.".Length..]}",
            ["visibility"] = a.Visibility ?? "initial",
            ["category"] = category
        };
    }

    private static JsonNode BuildBriefingNode(CaseDraft d)
    {
        var initialAttachmentIds = d.Bible?.Sources.Single(s => s.Id == "email.briefing").Attachments ?? d.AssetFull
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
            using var document = StructuredJson.ParseObject(json);
            errors.AddRange(StructuredJson.SchemaErrors(document, _v2Schema));
        }
        catch (JsonException ex)
        {
            errors.Add($"JSON parse error: {ex.Message}");
            _logger.LogWarning(ex, "Compiled case JSON could not be parsed.");
        }
        return errors;
    }

    private void CleanupWorkspace(string? workspace, bool persisted)
    {
        if (workspace is null || !Directory.Exists(workspace)) return;
        if (!persisted && Directory.Exists(Path.Combine(workspace, "previous")))
        {
            _logger.LogError("Previous case retained at {Workspace} after a failed promotion; manual recovery is required", workspace);
            return;
        }
        try
        {
            Directory.Delete(workspace, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Failed to clean generation workspace {Workspace}", workspace);
        }
    }

    private string ResolveCasesBasePath()
    {
        var configured = _config["CaseGenV2:CasesBasePath"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var path = Path.GetFullPath(configured);
            Directory.CreateDirectory(path);
            return path;
        }

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
        var path = Path.Combine(AppContext.BaseDirectory, "Schemas", "case.v2.schema.json");
        if (File.Exists(path)) return path;
        throw new FileNotFoundException("Canonical case schema is missing from the build output. Rebuild or republish CaseGen.Functions.", path);
    }

    private static async Task TimeStage(string name, Dictionary<string, double> sink, Func<Task> body)
    {
        var sw = Stopwatch.StartNew();
        try { await body(); }
        finally { sink[name] = sw.Elapsed.TotalMilliseconds; }
    }
}
