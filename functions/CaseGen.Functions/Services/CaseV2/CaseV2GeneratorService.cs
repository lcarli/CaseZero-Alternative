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
    Task<CaseGenerationCheckpoint> GenerateStageAsync(CaseGenerationCheckpoint checkpoint, CaseGenerationStage stage,
        IJobPhaseReporter reporter, CancellationToken ct = default);
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
        var checkpoint = CaseGenerationCheckpoint.Create(request);
        try
        {
            foreach (var stage in Enum.GetValues<CaseGenerationStage>())
            {
                checkpoint = await GenerateStageAsync(checkpoint, stage, reporter, ct);
                if (checkpoint.Response is { Quality.Approved: false }) break;
            }
            return checkpoint.Response ?? throw new InvalidOperationException("Generation ended without an evaluated result.");
        }
        finally
        {
            try { checkpoint.CleanupStaging(); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(exception, "Could not clean the named generation workspace for {CaseId}", checkpoint.Draft.CaseId);
            }
        }
    }

    public async Task<CaseGenerationCheckpoint> GenerateStageAsync(CaseGenerationCheckpoint checkpoint, CaseGenerationStage stage,
        IJobPhaseReporter reporter, CancellationToken ct = default)
    {
        if ((int)(checkpoint.CompletedStage ?? 0) + 1 != (int)stage)
            throw new TaskContractException("Generation stages must resume immediately after the last confirmed checkpoint.");
        var request = checkpoint.Draft.Request;
        GenerationRequestValidator.Validate(request);
        using var usageScope = CaseV2TokenUsageTracker.Begin();
        using var runtime = CaseGenerationRuntime.Begin(request, _llm, ct, previous: checkpoint.Harness);
        checkpoint.Harness = runtime.Report;
        ct = runtime.CancellationToken;
        var draft = checkpoint.Draft;
        var graphModeEnabled = CaseGraphFeatureOptions.From(_config).Enabled;

        var stageMs = checkpoint.StageLatencyMs;
        string? canonicalTruth = draft.CaseBible is null ? null : JsonSerializer.Serialize(draft.CaseBible, CaseDraft.CanonicalJsonOptions);

        CaseGenerationCheckpoint Confirm()
        {
            checkpoint.Draft = draft;
            checkpoint.CompletedStage = stage;
            checkpoint.Harness = runtime.Report;
            if (checkpoint.Response is { } response)
            {
                response.Harness = runtime.Report;
                response.InputTokens = runtime.Report.InputTokens;
                response.OutputTokens = runtime.Report.OutputTokens;
            }
            checkpoint.CapturePreparation();
            return checkpoint;
        }

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
            if (canonicalTruth is not null
                && JsonSerializer.Serialize(draft.CaseBible, CaseDraft.CanonicalJsonOptions) != canonicalTruth)
                throw new TaskContractException($"Stage {name} changed the immutable CaseBible.");
        }

        if (stage == CaseGenerationStage.Planning)
        {
        await Stage("caseBible", () => new CaseBibleTask(_llm, _logger).RunAsync(draft, ct));
        canonicalTruth = JsonSerializer.Serialize(draft.CaseBible, CaseDraft.CanonicalJsonOptions);

        // === Phase 2: project the Bible into the plot outline
        await Stage("plotOutline", () => new PlotOutlineTask(_llm, _logger).RunAsync(draft, ct));
        var caseBibleValidation = CaseBibleValidator.Validate(draft, required: true);
        LogStageValidation("caseBible", PipelineStageValidator.ValidateCaseBible(draft, required: true));
        if (!caseBibleValidation.IsValid)
            throw new CaseBibleValidationException(caseBibleValidation);
        LogStageValidation("blueprint", PipelineStageValidator.ValidateBlueprint(draft));
        return Confirm();
        }

        var isRookie = string.Equals(draft.Metadata.Difficulty, "Rookie", StringComparison.OrdinalIgnoreCase);
        var difficultyProfile = DifficultyProfileCatalog.Get(draft);
        if (stage == CaseGenerationStage.Authorship)
        {
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

            await Task.WhenAll(assetsTask, timelineTask, briefingTask);
            var assets = await assetsTask;
            draft.AssetFull = assets.ToList();

            // Propagate the request language onto every structured EvidenceDocument
            // so the renderer's chrome (classification band, page numbering,
            // letterhead labels, signature captions) localises correctly.
            var language = string.IsNullOrWhiteSpace(draft.Request.Language) ? "en-US" : draft.Request.Language!;
            foreach (var a in draft.AssetFull)
                if (a.BodyDoc is not null) a.BodyDoc.Language = language;
        });
        LogStageValidation("evidenceContent", PipelineStageValidator.ValidateEvidenceContent(draft));
        return Confirm();
        }

        if (stage == CaseGenerationStage.Forensics)
        {
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
                ForensicResultIdNormalizer.EnsureUnique(
                    details,
                    draft.AssetFull.Select(asset => asset.Id),
                    draft.FollowUpEmails.Concat(draft.ResultEmails).Select(email => email.Id));

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
        return Confirm();
        }

        if (stage == CaseGenerationStage.Solution)
        {
        await Stage("rulesAndSolutionSkeleton", async () =>
        {
            // SolutionSkeleton needs the pre-built reveal rules already in the draft so it knows
            // which hidden assets/emails are reachable. Run it first, then narrative rules in parallel
            // with Questions in the next phase.
            await new SolutionSkeletonTask(_llm, _logger).RunAsync(draft, ct);
            if (!isRookie)
                await new RulesTask(_llm, _logger).RunAsync(draft, ct);
            CaseGraphProjection.RefreshGeneratedGraph(draft);
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
        return Confirm();
        }

        if (stage is CaseGenerationStage.Rendering or CaseGenerationStage.Publication)
        {
            if (stage == CaseGenerationStage.Rendering)
                await RenderCheckpointAsync(checkpoint, Stage, ct);
            else
                await PublishCheckpointAsync(checkpoint, Stage, ct);
            return Confirm();
        }

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
        List<string> ValidateAll(CaseDraft candidate, string j, PublicContractParityReport candidateParity,
            out List<RepairIssue> repairIssues)
        {
            var all = Validate(j);
            repairIssues = all
                .Select(error => new RepairIssue("schema", error))
                .ToList();
            var candidateConsistency = _consistency.Validate(candidate);
            all.AddRange(candidateConsistency.Errors);
            repairIssues.AddRange(candidateConsistency.Errors.Select(error => new RepairIssue("consistency", error)));
            var bibleReport = CaseBibleValidator.Validate(candidate, required: true);
            foreach (var issue in bibleReport.Issues)
            {
                var message = $"{issue.Code} [{issue.NodeId}]: {issue.Message}";
                all.Add(message);
                repairIssues.Add(new RepairIssue(
                    issue.Code.StartsWith("projection_", StringComparison.Ordinal)
                        ? "blueprint"
                        : "caseBible",
                    message)
                {
                    NodeId = issue.NodeId,
                    ConstraintId = issue.Code
                });
            }
            AddStageIssues("blueprint", PipelineStageValidator.ValidateBlueprint(candidate), all, repairIssues);
            AddStageIssues("evidencePlan", PipelineStageValidator.ValidateEvidencePlan(candidate), all, repairIssues);
            AddStageIssues("evidenceContent", PipelineStageValidator.ValidateEvidenceContent(candidate), all, repairIssues);
            AddStageIssues("forensics", PipelineStageValidator.ValidateForensics(candidate), all, repairIssues);
            AddStageIssues("locale", PipelineStageValidator.ValidateLocale(candidate), all, repairIssues);
            AddStageIssues("difficulty", PipelineStageValidator.ValidateDifficultyTopology(candidate), all, repairIssues);
            if (graphModeEnabled)
            {
                foreach (var issue in candidateParity.Issues)
                {
                    var message = $"CaseGraph public parity failed for '{issue.Surface}': {issue.Message}";
                    all.Add(message);
                    repairIssues.Add(new RepairIssue("graphProjection", message)
                    {
                        NodeId = $"public.{issue.Surface}",
                        ConstraintId = $"parity.{issue.Surface}"
                    });
                }
            }
            return all.Distinct(StringComparer.Ordinal).ToList();
        }

        await Stage("prepareMaterials", () =>
        {
            _renderer.PrepareForReview(draft.AssetFull.Concat(draft.ResultAssets));
            return Task.CompletedTask;
        });
        var errors = ValidateAll(draft, json, parityReport, out var currentRepairIssues);
        IReadOnlyDictionary<string, string> readableTexts = new Dictionary<string, string>();
        await Stage("readableMaterials", async () =>
        {
            try { readableTexts = await _renderer.ReadPreparedDocumentsAsync(draft.AssetFull.Concat(draft.ResultAssets), ct); }
            catch (Exception exception) when (exception is TaskContractException or IOException or InvalidOperationException or ArgumentException)
            {
                errors.Add($"Rendered evidence: {exception.Message}");
                currentRepairIssues.Add(new("evidenceContent", exception.Message));
                _logger.LogWarning(exception, "Readable material verification failed for {CaseId}", draft.CaseId);
            }
        });
        Tasks.RedTeamTask.Report? redTeam = null;
        Tasks.SolverTask.SolverResult? solver = null;
        await Stage("playerInvestigation", async () =>
        {
            solver = await new Tasks.SolverTask(_llm, _logger).RunAsync(draft, ct, readableTexts);
        });
        await Stage("blockingReview", async () =>
        {
            redTeam = await new Tasks.RedTeamTask(_llm, _logger).RunOnAssembledAsync(json, draft, ct);
        });
        var quality = CaseQualityGate.Evaluate(request, _blobPublisher.IsConfigured, errors,
            consistencyReport?.Errors ?? [], redTeam, draft.Revision);
        if (solver?.Correct != true)
            quality.Block("player_investigation_failed", "solver", solver?.Notes ?? "Player investigation did not establish the solution.");
        var repair = new CaseRepairReport { BaseRevision = draft.Revision };
        var maxRepairIterations = int.TryParse(_config["CaseGenV2:RepairMaxIterations"], out var cfgN) && cfgN >= 0
            ? Math.Min(cfgN, difficultyProfile.MaxRepairIterations) : difficultyProfile.MaxRepairIterations;
        var refineIterations = 0; // response field retained for public API compatibility
        var refineErrorsBefore = errors.Count;
        Tasks.RedTeamTask.Report? redTeamInitial = null;
        var verdictTrajectory = new List<string>();
        if (!string.IsNullOrEmpty(redTeam?.Verdict))
            verdictTrajectory.Add(redTeam!.Verdict);
        var repairCoordinator = new RepairCoordinator(_llm, _mechanicalRules, _logger);
        var totalRepairOperations = 0;
        var totalRepairPlateaus = 0;
        while (!quality.Approved && refineIterations < maxRepairIterations && redTeam?.Succeeded == true
            && (currentRepairIssues.Count > 0 || redTeam.Findings.Any(finding => finding.Blocking) || solver?.Correct != true))
        {
            var actionableFindings = redTeam?.Findings.Where(f => f.Blocking).ToList()
                ?? new List<Tasks.RedTeamTask.Finding>();
            refineIterations++;
            repair.Attempted = true;
            repair.OriginalBlockers = quality.Issues.Where(issue => issue.Blocking).Select(issue => issue.Message).ToList();
            if (redTeamInitial is null) redTeamInitial = redTeam;
            var candidate = draft.Copy();
            candidate.Revision = draft.Revision + 1;
            repair.CandidateRevision = candidate.Revision;
            try
            {
                await Stage("targetedRepair", async () =>
                {
                    await repairCoordinator.RepairAsync(
                        candidate,
                        actionableFindings,
                        currentRepairIssues,
                        solver?.Correct != true,
                        ct);
                    totalRepairOperations += repairCoordinator.LastGranularReport.Operations.Count;
                    totalRepairPlateaus += repairCoordinator.LastGranularReport.Plateaus.Count;
                    _renderer.PrepareForReview(candidate.AssetFull.Concat(candidate.ResultAssets));
                    EvidenceGraphCompiler.Compile(candidate);
                    var candidateJson = AssembleForMode(candidate, graphModeEnabled, out var candidateParity).ToJsonString(JsonOpts);
                    var candidateErrors = ValidateAll(candidate, candidateJson, candidateParity, out var candidateRepairIssues);
                    candidateErrors.AddRange(ValidateCanonical(candidate, draft));
                    if (JsonSerializer.Serialize(candidate.CaseBible, CaseDraft.CanonicalJsonOptions) != canonicalTruth)
                        throw new TaskContractException("Repair changed immutable canonical truth.");
                    var candidateTexts = await _renderer.ReadPreparedDocumentsAsync(candidate.AssetFull.Concat(candidate.ResultAssets), ct);
                    var redTeamTask = new Tasks.RedTeamTask(_llm, _logger)
                        .RunOnAssembledAsync(candidateJson, candidate, ct);
                    var solverTask = new Tasks.SolverTask(_llm, _logger).RunAsync(candidate, ct, candidateTexts);
                    var candidateReview = await redTeamTask;
                    var candidateSolver = await solverTask;
                    verdictTrajectory.Add(candidateReview.Verdict);
                    var candidateFinal = new CaseV2FinalValidator(_v2SchemaJson).Validate(
                        candidate, candidateJson, candidateSolver, candidateReview, graphModeEnabled ? candidateParity : null);
                    candidateErrors.AddRange(candidateFinal.Issues.Where(issue => issue.Blocking).Select(issue => issue.Message));
                    var candidateQuality = CaseQualityGate.Evaluate(request, _blobPublisher.IsConfigured,
                        candidateErrors, [], candidateReview, candidate.Revision);
                    if (!candidateSolver.Correct)
                        candidateQuality.Block("player_investigation_failed", "solver",
                            candidateSolver.Notes ?? "Repaired case could not be solved from player-visible materials.");
                    if (!candidateQuality.Approved)
                        throw new TaskContractException(string.Join("; ", candidateQuality.Issues.Where(issue => issue.Blocking).Select(issue => issue.Message)));

                    repair.ChangedComponents = candidate.GetComponents()
                        .Where(pair => pair.Value?.ToJsonString() != draft.GetComponents()[pair.Key]?.ToJsonString())
                        .Select(pair => pair.Key).ToList();
                    draft = candidate;
                    json = candidateJson;
                    parityReport = candidateParity;
                    errors = candidateErrors;
                    currentRepairIssues = candidateRepairIssues;
                    readableTexts = candidateTexts;
                    redTeam = candidateReview;
                    solver = candidateSolver;
                    quality = candidateQuality;
                    repair.Accepted = true;
                });
            }
            catch (Exception exception) when (exception is TaskContractException or JsonException
                or Azure.RequestFailedException or System.ClientModel.ClientResultException or HttpRequestException or TimeoutException)
            {
                repair.ErrorCode = exception is TaskContractException or JsonException ? "repair_rejected" : "repair_unavailable";
                repair.ErrorMessage = exception.Message;
                totalRepairPlateaus++;
                _logger.LogWarning(exception, "Rejected repair candidate {Revision}; original revision {OriginalRevision} is unchanged",
                    candidate.Revision, draft.Revision);
                break;
            }
        }

        FinalValidationReport? finalValidation = null;
        await Stage("finalValidation", () =>
        {
            finalValidation = new CaseV2FinalValidator(_v2SchemaJson).Validate(
                draft,
                json,
                solver,
                redTeam,
                graphModeEnabled ? parityReport : null);
            errors.AddRange(finalValidation.Issues
                .Where(issue => issue.Blocking)
                .Select(issue => $"final {issue.Gate}/{issue.Code} [{issue.NodeId}]: {issue.Message}"));
            return Task.CompletedTask;
        });
        errors = errors.Distinct(StringComparer.Ordinal).ToList();
        if (repair.Accepted) consistencyReport = _consistency.Validate(draft);
        quality = CaseQualityGate.Evaluate(request, _blobPublisher.IsConfigured, errors,
            consistencyReport?.Errors ?? [], redTeam, draft.Revision);
        if (!repair.Accepted && repair.ErrorCode is not null)
            quality.Block(repair.ErrorCode, "repair", repair.ErrorMessage!);

        var refineAttempted = refineIterations > 0;
        var redTeamRerun = (redTeamInitial is not null
                && !string.Equals(redTeamInitial.Verdict, redTeam?.Verdict, StringComparison.OrdinalIgnoreCase))
            || verdictTrajectory.Count > 1;

        if (!quality.Approved)
        {
            _logger.LogWarning("Quality gate blocked case {CaseId}: {Issues}",
                draft.CaseId, string.Join("; ", quality.Issues.Where(issue => issue.Blocking).Select(issue => $"{issue.Code}: {issue.Message}")));
        }

        checkpoint.ReadableTexts = readableTexts.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        checkpoint.Response = new GenerateCaseV2Response
        {
            CaseId = draft.CaseId,
            CaseJson = json,
            ValidationErrors = errors,
            StageLatencyMs = stageMs,
            AutoFixesApplied = consistencyReport?.AutoFixes ?? new(),
            RefineAttempted = repair.Attempted,
            RefineErrorsBefore = refineErrorsBefore,
            RefineErrorsAfter = errors.Count,
            RefineIterations = refineIterations,
            RedTeamRerun = redTeamRerun,
            RedTeam = redTeam,
            RedTeamInitial = redTeamInitial,
            RedTeamVerdictTrajectory = verdictTrajectory,
            Solver = solver,
            Quality = quality,
            Repair = repair,
            CaseGraphEnabled = graphModeEnabled,
            PublicContractParity = parityReport,
            FinalValidation = finalValidation,
            RepairPlateauCount = totalRepairPlateaus,
            RepairOperationCount = totalRepairOperations,
            InputTokens = runtime.Report.InputTokens,
            OutputTokens = runtime.Report.OutputTokens,
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
            Consistency = consistencyReport,
            Harness = runtime.Report
        };
        return Confirm();
    }

    private async Task RenderCheckpointAsync(CaseGenerationCheckpoint checkpoint, Func<string, Func<Task>, Task> stage, CancellationToken ct)
    {
        var response = checkpoint.Response ?? throw new TaskContractException("Rendering requires a completed evaluation.");
        if (!checkpoint.Draft.Request.WriteToDisk || !response.Quality.Approved) return;
        var draft = checkpoint.Draft;
        var workspace = Path.Combine(ResolveCasesBasePath(), ".casegen-staging", Guid.NewGuid().ToString("N"));
        checkpoint.StagedCaseDirectory = Path.Combine(workspace, draft.CaseId);
        var assets = draft.AssetFull.Concat(draft.ResultAssets).ToList();
        AssetRenderingReport? rendering = null;
        try
        {
            await stage("renderAssets", async () =>
            {
                Directory.CreateDirectory(checkpoint.StagedCaseDirectory);
                await File.WriteAllTextAsync(Path.Combine(checkpoint.StagedCaseDirectory, "case.json"), response.CaseJson, ct);
                rendering = await _renderer.RenderAllAsync(draft.CaseId, workspace, assets, ct);
            });
            response.AssetsRenderedPdfs = rendering!.PdfsWritten;
            response.AssetsRenderedImages = rendering.ImagesWritten;
            response.AssetsSkipped = rendering.Skipped;
            response.AssetRenderingErrors = rendering.Errors;
            CaseQualityGate.CheckRendering(response.Quality, rendering);
            foreach (var pair in checkpoint.ReadableTexts)
                if (!rendering.ReadableTexts.TryGetValue(pair.Key, out var text)
                    || RenderedEvidenceText.Normalize(text) != RenderedEvidenceText.Normalize(pair.Value))
                    response.Quality.Block("rendered_text_changed", "rendering",
                        "Final evidence differs from the readable material investigated and reviewed.", pair.Key);
            if (!response.Quality.Approved) return;
            CaseBundle? bundle = null;
            await stage("verifyBundle", async () =>
            {
                bundle = await CaseBundle.CreateAsync(draft.CaseId, draft.Revision, checkpoint.StagedCaseDirectory,
                    response.CaseJson, assets, rendering, ct);
            });
            response.CaseJson = bundle!.CaseJson;
            response.Bundle = bundle.Report;
            foreach (var error in Validate(response.CaseJson))
                response.Quality.Block("bundle_schema_invalid", "bundle", error);
            if (!response.Quality.Approved) return;
            var previousETag = await _blobPublisher.GetPublicationETagAsync(draft.CaseId, ct);
            checkpoint.StagedBundle = new(bundle.Manifest, bundle.ManifestSha256, previousETag);
        }
        catch (Exception exception) when (exception is CaseBundleValidationException or IOException or UnauthorizedAccessException
            or Azure.RequestFailedException)
        {
            var invalid = exception as CaseBundleValidationException;
            response.Quality.Block(invalid?.Code ?? "rendering_failed",
                "rendering", exception.Message, invalid?.Reference);
            _logger.LogError(exception, "Case {CaseId} rendering checkpoint failed", draft.CaseId);
        }
    }

    private async Task PublishCheckpointAsync(CaseGenerationCheckpoint checkpoint, Func<string, Func<Task>, Task> stage, CancellationToken ct)
    {
        var response = checkpoint.Response ?? throw new TaskContractException("Publication requires an evaluated response.");
        if (!checkpoint.Draft.Request.WriteToDisk || !response.Quality.Approved) return;
        var staged = checkpoint.StagedBundle ?? throw new TaskContractException("Publication requires a verified rendering checkpoint.");
        var directory = checkpoint.StagedCaseDirectory ?? throw new TaskContractException("Checkpoint assets were not restored.");
        var failureArea = "persistence";
        try
        {
            var bundle = await CaseBundle.RestoreAsync(staged.Manifest, directory, response.CaseJson, staged.ManifestSha256, ct);
            bundle.RememberPublicationSnapshot(staged.PreviousETag);
            using var local = await LocalCasePublication.PrepareAsync(bundle, ResolveCasesBasePath(), _logger, ct);
            if (_blobPublisher.IsConfigured)
            {
                failureArea = "publication";
                await stage("publishToBlob", async () =>
                {
                    response.BlobsPublished = await _blobPublisher.PublishAsync(bundle, ct);
                    response.Quality.Published = response.BlobsPublished > 0;
                    if (!response.Quality.Published)
                        response.Quality.Block("publication_failed", "publication", "No immutable bundle was committed.");
                });
            }
            failureArea = "persistence";
            if (response.Quality.Approved)
                await stage("persist", async () =>
                {
                    response.OutputPath = await local.CommitAsync(ct);
                    response.Quality.Persisted = true;
                });
        }
        catch (Exception exception) when (exception is CaseBundleValidationException or IOException or UnauthorizedAccessException
            or Azure.RequestFailedException)
        {
            var invalid = exception as CaseBundleValidationException;
            response.Quality.Block(invalid?.Code
                ?? (exception is CasePublicationConflictException ? "publication_conflict" : $"{failureArea}_failed"),
                invalid is null ? failureArea : "bundle", exception.Message, invalid?.Reference);
            _logger.LogError(exception, "Case {CaseId} conditional publication failed", checkpoint.Draft.CaseId);
        }
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
        AssociateSuspectAssets(d);
        var assetsNode = new JsonArray();
        foreach (var a in d.AssetFull) assetsNode.Add(BuildAssetNode(d.CaseId, a));
        foreach (var a in d.ResultAssets) assetsNode.Add(BuildAssetNode(d.CaseId, a));

        var emailsNode = new JsonArray { BuildBriefingNode(d) };
        foreach (var e in d.FollowUpEmails) emailsNode.Add(BuildEmailNode(d, e, "initial"));
        foreach (var e in d.ResultEmails) emailsNode.Add(BuildEmailNode(d, e, "hidden"));

        var suspectsNode = new JsonArray();
        foreach (var s in d.SuspectFull)
        {
            var suspectNode = new JsonObject
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
                ["relatedAssets"] = JsonSerializer.SerializeToNode(s.RelatedAssets, JsonOpts),
                ["visibility"] = "initial"
            };
            if (!string.IsNullOrWhiteSpace(s.Photo))
                suspectNode["photo"] = s.Photo;
            foreach (var field in new[] { "age", "occupation", "relationship", "motive", "alibi", "background" })
                if (suspectNode[field] is null) suspectNode.Remove(field);
            suspectsNode.Add(suspectNode);
        }

        var timeline = JsonSerializer.SerializeToNode(d.Timeline, JsonOpts)!.AsArray();
        var temporalEvents = JsonSerializer.SerializeToNode(d.TemporalEvents, JsonOpts)!.AsArray();
        var rules = JsonSerializer.SerializeToNode(d.Rules, JsonOpts)!.AsArray();
        var forensicOutcomes = JsonSerializer.SerializeToNode(d.ForensicFull, JsonOpts)!.AsArray();
        foreach (var outcome in forensicOutcomes.OfType<JsonObject>()) outcome.Remove("support");
        var questions = JsonSerializer.SerializeToNode(d.Questions, JsonOpts)!.AsArray();
        foreach (var question in questions.OfType<JsonObject>()) question.Remove("support");

        var forensicsDefaults = new JsonObject
        {
            ["analysisTypes"] = JsonSerializer.SerializeToNode(d.AnalysisTypes, JsonOpts),
            ["noFindingsEmail"] = JsonSerializer.SerializeToNode(CaseEmailProjection.NoFindingsTemplate(d), JsonOpts)
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

        var node = new JsonObject
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
        return node;
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

    private static JsonNode BuildAssetNode(string caseId, EvidenceAsset a)
    {
        var category = a.Category ?? a.Type switch
        {
            "photo" or "image" => "Document",
            "pdf" or "document" => "Document",
            "digital" => "Digital",
            _ => "Document"
        };
        var node = new JsonObject
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
        if (!string.IsNullOrWhiteSpace(a.EvidenceRole))
            node["evidenceRole"] = a.EvidenceRole;
        if (!string.IsNullOrWhiteSpace(a.SubjectSuspectId))
            node["subjectSuspectId"] = a.SubjectSuspectId;
        if (!string.IsNullOrWhiteSpace(a.ImagePurpose))
            node["imagePurpose"] = a.ImagePurpose;
        if (a.Description is null) node.Remove("description");
        return node;
    }

    private static void AssociateSuspectAssets(CaseDraft draft)
    {
        foreach (var suspect in draft.SuspectFull)
        {
            var related = draft.AssetStubs
                .Where(asset => asset.SubjectSuspectId == suspect.Id)
                .Select(asset => asset.Id)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToList();
            suspect.RelatedAssets = related;
            suspect.Photo = draft.AssetStubs
                .SingleOrDefault(asset =>
                    asset.SubjectSuspectId == suspect.Id
                    && asset.ImagePurpose == ImagePurposes.SuspectPortrait)
                ?.Id;
        }
    }

    private static JsonNode BuildBriefingNode(CaseDraft d)
    {
        var initialAttachmentIds = CaseEmailProjection.BriefingAttachments(d);
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
