using System.Net;
using System.Text.Json;
using CaseGen.Functions.Models.CaseV2;
using CaseGen.Functions.Services.CaseV2;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Functions.CaseV2;

/// <summary>
/// Async job pattern for v2 case generation. HTTP starter returns 202 + jobId,
/// orchestrator checkpoints seven coarse stages in private blob storage, and a GET endpoint
/// composes Durable runtime status + per-phase blob status so callers can poll.
/// </summary>
public class CaseV2GenerationOrchestrator
{
    public const string OrchestratorName = "CaseV2GenerationOrchestrator";
    public const string ActivityName = "CaseV2GenerateActivity";
    public const string StageActivityName = "CaseV2GenerateStageActivity";
    public const string JobIdPrefix = "casev2-";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    // ---------------- Orchestrator ----------------

    [Function(OrchestratorName)]
    public async Task<CaseV2JobResult> RunOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var input = context.GetInput<CaseV2OrchestrationInput>()
            ?? throw new InvalidOperationException($"{OrchestratorName} requires CaseV2OrchestrationInput");

        var logger = context.CreateReplaySafeLogger<CaseV2GenerationOrchestrator>();
        logger.LogInformation("[CaseV2] Orchestration {InstanceId} started for jobId={JobId}", context.InstanceId, input.JobId);

        CaseCheckpointReference? checkpoint = null;
        CaseV2JobResult? result = null;
        foreach (var stage in Enum.GetValues<CaseGenerationStage>())
        {
            var output = await context.CallActivityAsync<CaseStageResult>(StageActivityName,
                new CaseStageInput(input.JobId, input.Request, stage, checkpoint));
            checkpoint = output.Reference;
            result = output.Result;
            if (!output.Approved || result is not null) break;
        }

        logger.LogInformation(
            "[CaseV2] Orchestration {InstanceId} finished jobId={JobId} caseId={CaseId} errors={Errors}",
            context.InstanceId, input.JobId, result!.CaseId, result.ValidationErrorsCount);

        return result!;
    }
}

/// <summary>
/// Input passed from starter → orchestrator → activity.
/// </summary>
public record CaseV2OrchestrationInput(
    GenerateCaseV2Request Request,
    string JobId,
    int Attempt = 1,
    int MaxAttempts = 5);

public sealed class CaseV2GenerateStageActivity(
    ICaseV2GeneratorService generator,
    ICaseGenerationCheckpointStore store,
    IJobPhaseReporterFactory reporterFactory,
    ILogger<CaseV2GenerateStageActivity> logger)
{
    [Function(CaseV2GenerationOrchestrator.StageActivityName)]
    public async Task<CaseStageResult> RunAsync([ActivityTrigger] CaseStageInput input, FunctionContext context)
    {
        CaseGenerationCheckpoint.ValidateInput(input);
        var ct = context.CancellationToken;
        var reporter = reporterFactory.Create(input.JobId);
        CaseGenerationCheckpoint? checkpoint = null;
        var executionStarted = false;
        try
        {
            var existing = await store.FindAsync(input.JobId, input.Stage, ct);
            if (existing is not null)
            {
                var saved = await store.ReadAsync(existing, ct, restoreAssets: false);
                saved.Bind(input, completed: true);
                return Result(existing, saved);
            }
            checkpoint = input.Previous is null ? CaseGenerationCheckpoint.Create(input.Request)
                : await store.ReadAsync(input.Previous, ct);
            checkpoint.Bind(input);
            executionStarted = await store.TryBeginStageAsync(input, checkpoint, ct);
            if (!executionStarted)
            {
                existing = await store.FindAsync(input.JobId, input.Stage, ct);
                if (existing is not null)
                {
                    var saved = await store.ReadAsync(existing, ct, restoreAssets: false);
                    saved.Bind(input, completed: true);
                    return Result(existing, saved);
                }
                throw new TaskContractException("Stage execution is in progress or its paid-call outcome is unconfirmed. Automatic re-execution is blocked.");
            }
            if (input.Previous is null)
                await reporter.ReportAttemptStartedAsync(1, 1, ct);
            checkpoint = await generator.GenerateStageAsync(checkpoint, input.Stage, reporter, ct);
            var reference = await store.SaveAsync(input.JobId, checkpoint, ct);
            var result = Result(reference, checkpoint);
            if (result.Result is { } terminal)
            {
                if (terminal.HasErrors) await reporter.ReportFailedAsync(terminal.ErrorMessage!, ct);
                else await reporter.ReportCompletedAsync(ct);
            }
            return result;
        }
        catch (Exception exception) when (exception is TaskContractException or CaseBibleValidationException
            or CaseGenerationBudgetException or JsonException or IOException or Azure.RequestFailedException
            or HttpRequestException or TimeoutException or OperationCanceledException)
        {
            logger.LogError(exception, "Generation checkpoint {Stage} failed for {JobId}", input.Stage, input.JobId);
            if (executionStarted && checkpoint is not null)
            {
                using var failureTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                try { await store.RecordFailureAsync(input, checkpoint, exception.Message, failureTimeout.Token); }
                catch (Exception reportException) when (reportException is IOException or Azure.RequestFailedException
                    or HttpRequestException or TimeoutException or OperationCanceledException)
                {
                    logger.LogError(reportException, "Could not persist usage failure report; the private stage reservation remains for {JobId}", input.JobId);
                }
            }
            await reporter.ReportFailedAsync(exception.Message, CancellationToken.None);
            throw;
        }
        finally
        {
            try { checkpoint?.CleanupStaging(); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(exception, "Could not clean the named checkpoint workspace for {JobId}", input.JobId);
            }
        }
    }

    private static CaseStageResult Result(CaseCheckpointReference reference, CaseGenerationCheckpoint checkpoint)
    {
        var approved = checkpoint.Response?.Quality.Approved ?? true;
        var terminal = !approved || checkpoint.CompletedStage == CaseGenerationStage.Publication;
        return new(reference, approved, terminal
            ? CaseV2JobResult.FromResponse(reference.JobId, checkpoint.Response
                ?? throw new TaskContractException("Terminal checkpoint has no evaluated response.")) : null);
    }
}

/// <summary>
/// Compact job result — large payload (full case.json, asset bytes) is in blob storage, not Durable state.
/// </summary>
public record CaseV2JobResult(
    string JobId,
    string CaseId,
    string OutputPath,
    int ValidationErrorsCount,
    int AssetsRenderedPdfs,
    int AssetsRenderedImages,
    int BlobsPublished,
    bool HasErrors,
    string? ErrorMessage,
    Dictionary<string, double> StageLatencyMs,
    List<string> AutoFixesApplied,
    bool RefineAttempted,
    int RefineErrorsBefore,
    int RefineErrorsAfter,
    int RefineIterations,
    long InputTokens,
    long OutputTokens,
    bool CaseGraphEnabled,
    FinalValidationReport? FinalValidation,
    bool GraphValidationPassed,
    bool FirstPassSuccess,
    int RepairPlateauCount,
    int RepairOperationCount,
    bool SolverSucceeded,
    int EvidenceLayoutDiversity,
    List<string> EvidenceLayouts,
    Dictionary<string, int> SpecialistFindingsByCategory,
    string? RedTeamVerdict,
    string? RedTeamVerdictInitial,
    List<string> RedTeamVerdictTrajectory,
    bool RedTeamRerun,
    int AttemptCount,
    int MaxAttempts,
    double SolverScore,
    CaseQualityReport? Quality = null,
    int AssetsSkipped = 0,
    CaseRepairReport? Repair = null,
    CaseBundleReport? Bundle = null)
{
    public static CaseV2JobResult FromResponse(string jobId, GenerateCaseV2Response response, int attempt = 1, int maxAttempts = 1)
    {
        var quality = response.Quality;
        var hasErrors = response.ValidationErrors.Count > 0 || response.AssetRenderingErrors.Count > 0
            || !quality.Approved || quality.HasBlockingIssues
            || (response.Bundle is not null && (!response.Bundle.Verified || response.Bundle.Revision != quality.Revision))
            || (quality.PersistenceRequested && !quality.Persisted)
            || (quality.PublicationRequested && !quality.Published);
        var messages = quality.Issues.Where(issue => issue.Blocking)
            .Select(issue => $"{issue.Code}: {issue.Message}")
            .Concat(response.ValidationErrors)
            .Concat(response.AssetRenderingErrors)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var error = hasErrors
            ? messages.Count > 0 ? string.Join(" · ", messages)
                : "Generation did not pass quality checks or complete the requested outputs."
            : null;
        return new(
            jobId, response.CaseId, response.OutputPath, response.ValidationErrors.Count,
            response.AssetsRenderedPdfs, response.AssetsRenderedImages, response.BlobsPublished,
            hasErrors, error, response.StageLatencyMs, response.AutoFixesApplied,
            response.RefineAttempted, response.RefineErrorsBefore, response.RefineErrorsAfter,
            response.RefineIterations, response.InputTokens, response.OutputTokens, response.CaseGraphEnabled,
            response.FinalValidation,
            response.FinalValidation?.Issues.All(issue => issue.Gate != FinalValidationGate.Graph || !issue.Blocking) == true,
            !response.RefineAttempted && quality.Approved,
            response.RepairPlateauCount, response.RepairOperationCount, response.Solver?.Correct == true,
            response.EvidenceLayoutDiversity, response.EvidenceLayouts, response.SpecialistFindingsByCategory,
            response.RedTeam?.Verdict, response.RedTeamInitial?.Verdict, response.RedTeamVerdictTrajectory,
            response.RedTeamRerun, attempt, maxAttempts, response.Solver?.Score ?? 0,
            quality, response.AssetsSkipped, response.Repair, response.Bundle);
    }
}

/// <summary>
/// The single long-running activity. Runs the v2 generator end-to-end and reports the current phase
/// to a per-job blob (best-effort) so the GET status endpoint can show progress.
/// </summary>
public class CaseV2GenerateActivity
{
    private readonly ICaseV2GeneratorService _generator;
    private readonly IJobPhaseReporterFactory _reporterFactory;
    private readonly ILogger<CaseV2GenerateActivity> _logger;

    public CaseV2GenerateActivity(
        ICaseV2GeneratorService generator,
        IJobPhaseReporterFactory reporterFactory,
        ILogger<CaseV2GenerateActivity> logger)
    {
        _generator = generator;
        _reporterFactory = reporterFactory;
        _logger = logger;
    }

    [Function(CaseV2GenerationOrchestrator.ActivityName)]
    public async Task<CaseV2JobResult> RunAsync(
        [ActivityTrigger] CaseV2OrchestrationInput input,
        FunctionContext ctx)
    {
        var ct = ctx.CancellationToken;
        var reporter = _reporterFactory.Create(input.JobId);

        await reporter.ReportAttemptStartedAsync(input.Attempt, input.MaxAttempts, ct);
        _logger.LogInformation(
            "[CaseV2] Activity start jobId={JobId} attempt={Attempt}/{MaxAttempts}",
            input.JobId,
            input.Attempt,
            input.MaxAttempts);

        try
        {
            var response = await _generator.GenerateAsync(input.Request, reporter, ct);

            var result = CaseV2JobResult.FromResponse(input.JobId, response, input.Attempt, input.MaxAttempts);
            if (result.HasErrors)
            {
                _logger.LogWarning("[CaseV2] Activity completed with validation errors jobId={JobId} errors={Count}",
                    input.JobId, response.ValidationErrors.Count);
                var error = result.ErrorMessage!;
                if (input.Attempt < input.MaxAttempts)
                    await reporter.ReportRetryScheduledAsync(error, RetryAt(input.Attempt), ct);
                else
                    await reporter.ReportFailedAsync(error, ct);
            }
            else
            {
                await reporter.ReportCompletedAsync(ct);
            }

            return result;
        }
        catch (Exception ex) when (input.Attempt < input.MaxAttempts && IsRetryable(ex))
        {
            _logger.LogWarning(
                ex,
                "[CaseV2] Retryable activity failure jobId={JobId} attempt={Attempt}/{MaxAttempts}",
                input.JobId,
                input.Attempt,
                input.MaxAttempts);
            await reporter.ReportRetryScheduledAsync(ex.Message, RetryAt(input.Attempt), CancellationToken.None);
            return FailedAttempt(input, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CaseV2] Activity failed jobId={JobId}", input.JobId);
            await reporter.ReportFailedAsync(ex.Message, CancellationToken.None);
            // Re-throw so Durable marks the orchestration Failed and the GET endpoint reports it.
            throw;
        }
    }

    private static DateTimeOffset RetryAt(int attempt) =>
        DateTimeOffset.UtcNow.AddSeconds(Math.Min(300, 15 * (1 << Math.Min(attempt - 1, 4))));

    private static bool IsRetryable(Exception exception) =>
        exception is CaseBibleValidationException
        or TimeoutException
        or HttpRequestException
        || exception is Azure.RequestFailedException requestFailed
        && (requestFailed.Status is 408 or 429 || requestFailed.Status >= 500)
        || exception.InnerException is not null && IsRetryable(exception.InnerException);

    private static CaseV2JobResult FailedAttempt(CaseV2OrchestrationInput input, string error) => new(
        JobId: input.JobId,
        CaseId: input.Request.CaseId ?? string.Empty,
        OutputPath: string.Empty,
        ValidationErrorsCount: 1,
        AssetsRenderedPdfs: 0,
        AssetsRenderedImages: 0,
        BlobsPublished: 0,
        HasErrors: true,
        ErrorMessage: error,
        StageLatencyMs: new Dictionary<string, double>(),
        AutoFixesApplied: new List<string>(),
        RefineAttempted: false,
        RefineErrorsBefore: 0,
        RefineErrorsAfter: 1,
        RefineIterations: 0,
        InputTokens: 0,
        OutputTokens: 0,
        CaseGraphEnabled: false,
        FinalValidation: null,
        GraphValidationPassed: false,
        FirstPassSuccess: false,
        RepairPlateauCount: 0,
        RepairOperationCount: 0,
        SolverSucceeded: false,
        EvidenceLayoutDiversity: 0,
        EvidenceLayouts: new List<string>(),
        SpecialistFindingsByCategory: new Dictionary<string, int>(),
        RedTeamVerdict: null,
        RedTeamVerdictInitial: null,
        RedTeamVerdictTrajectory: new List<string>(),
        RedTeamRerun: false,
        AttemptCount: input.Attempt,
        MaxAttempts: input.MaxAttempts,
        SolverScore: 0);
}

/// <summary>
/// HTTP entrypoint: enqueues a Durable orchestration and returns 202 + jobId.
/// Enforces single-instance concurrency best-effort (see plan.md for race window discussion).
/// </summary>
public class StartCaseV2GenerationFunction
{
    private readonly ILogger<StartCaseV2GenerationFunction> _logger;
    private readonly IConfiguration _configuration;

    public StartCaseV2GenerationFunction(
        ILogger<StartCaseV2GenerationFunction> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    [Function("StartCaseV2Generation")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "cases/v2/generate")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        FunctionContext ctx)
    {
        var ct = ctx.CancellationToken;

        // 1) parse body
        GenerateCaseV2Request request;
        try
        {
            request = await GenerationRequestValidator.ReadAsync(req.Body, ct);
        }
        catch (GenerationRequestException ex)
        {
            _logger.LogWarning("Invalid case generation request: {Errors}", ex.Message);
            var invalid = req.CreateResponse(ex.TooLarge ? HttpStatusCode.RequestEntityTooLarge : HttpStatusCode.BadRequest);
            await invalid.WriteAsJsonAsync(new
            {
                error = ex.TooLarge ? "request_too_large" : "invalid_request",
                message = ex.Message,
                errors = ex.Errors
            }, ct);
            return invalid;
        }

        // 2) singleton enforcement (best-effort). Query by InstanceIdPrefix so we hit the server-side
        // narrow path, then client-side check on orchestrator name. CreatedFrom limits the scan window.
        try
        {
            var query = new OrchestrationQuery(
                CreatedFrom: DateTimeOffset.UtcNow.AddHours(-24),
                Statuses: new[]
                {
                    OrchestrationRuntimeStatus.Pending,
                    OrchestrationRuntimeStatus.Running
                },
                InstanceIdPrefix: CaseV2GenerationOrchestrator.JobIdPrefix,
                PageSize: 20);

            await foreach (var existing in client.GetAllInstancesAsync(query).WithCancellation(ct))
            {
                if (string.Equals(existing.Name, CaseV2GenerationOrchestrator.OrchestratorName, StringComparison.Ordinal))
                {
                    var conflict = req.CreateResponse(HttpStatusCode.Conflict);
                    await conflict.WriteAsJsonAsync(new
                    {
                        message = "A v2 case generation is already in progress. Wait for it to finish or query its status.",
                        runningJobId = existing.InstanceId,
                        statusUri = $"/api/cases/v2/jobs/{existing.InstanceId}"
                    }, ct);
                    return conflict;
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Failure to enforce singleton is non-fatal — log and let the request proceed.
            _logger.LogWarning(ex, "Singleton check failed — proceeding without enforcement");
        }

        // 3) schedule the orchestration with an explicit, unique instanceId (== jobId)
        ct.ThrowIfCancellationRequested();
        var jobId = $"{CaseV2GenerationOrchestrator.JobIdPrefix}{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6]}";
        var maxAttempts = int.TryParse(_configuration["CaseGenV2:JobMaxAttempts"], out var configuredMaxAttempts)
                          && configuredMaxAttempts > 0
            ? Math.Min(configuredMaxAttempts, 10)
            : 5;
        var input = new CaseV2OrchestrationInput(request, jobId, MaxAttempts: maxAttempts);

        try
        {
            await client.ScheduleNewOrchestrationInstanceAsync(
                CaseV2GenerationOrchestrator.OrchestratorName,
                input,
                new StartOrchestrationOptions { InstanceId = jobId },
                ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to schedule {OrchestratorName} jobId={JobId}",
                CaseV2GenerationOrchestrator.OrchestratorName, jobId);
            var err = req.CreateResponse(HttpStatusCode.InternalServerError);
            await err.WriteAsJsonAsync(new { error = ex.Message, type = ex.GetType().Name }, ct);
            return err;
        }

        _logger.LogInformation("Scheduled v2 generation jobId={JobId} title={Title}", jobId, request.Title);

        var accepted = req.CreateResponse(HttpStatusCode.Accepted);
        accepted.Headers.Add("Location", $"/api/cases/v2/jobs/{jobId}");
        await accepted.WriteAsJsonAsync(new
        {
            jobId,
            status = "queued",
            statusUri = $"/api/cases/v2/jobs/{jobId}"
        }, ct);
        return accepted;
    }
}

/// <summary>
/// GET status endpoint. Composes Durable runtime status + per-phase blob status.
/// Durable terminal states (Completed, Failed, Terminated, Suspended) always win — the blob
/// might still show the last running phase from a crash.
/// </summary>
public class GetCaseV2JobStatusFunction
{
    private static readonly JsonSerializerOptions ResultJsonOpts = new() { PropertyNameCaseInsensitive = true };
    private readonly IJobPhaseReporterFactory _reporterFactory;
    private readonly ILogger<GetCaseV2JobStatusFunction> _logger;

    public GetCaseV2JobStatusFunction(IJobPhaseReporterFactory reporterFactory, ILogger<GetCaseV2JobStatusFunction> logger)
    {
        _reporterFactory = reporterFactory;
        _logger = logger;
    }

    [Function("GetCaseV2JobStatus")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "cases/v2/jobs/{jobId}")] HttpRequestData req,
        string jobId,
        [DurableClient] DurableTaskClient client,
        FunctionContext ctx)
    {
        var ct = ctx.CancellationToken;
        var metadata = await client.GetInstanceAsync(jobId, getInputsAndOutputs: true, ct);

        if (metadata is null)
        {
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            await notFound.WriteAsJsonAsync(new { jobId, error = "Job not found" }, ct);
            return notFound;
        }

        // Map Durable runtime to the public status enum
        var (status, isTerminal) = MapStatus(metadata.RuntimeStatus);

        // Pull blob phase (best-effort)
        string? currentPhase = null;
        string? currentStageId = null;
        string? pipelineVersion = null;
        double? progressPercent = null;
        object? stages = null;
        object? attempts = null;
        int? currentAttempt = null;
        int? maxAttempts = null;
        DateTimeOffset? nextRetryAt = null;
        string? retryReason = null;
        string? blobError = null;
        try
        {
            var reporter = _reporterFactory.Create(jobId);
            var blob = await reporter.GetAsync(ct);
            if (blob is not null)
            {
                currentPhase = blob.CurrentPhase;
                currentStageId = blob.CurrentStageId;
                pipelineVersion = blob.PipelineVersion;
                progressPercent = blob.ProgressPercent;
                stages = blob.Stages.Select(stage => new
                {
                    id = stage.Id,
                    status = stage.Status,
                    attempt = stage.Attempt,
                    startedAt = stage.StartedAt,
                    completedAt = stage.CompletedAt,
                    durationMs = stage.DurationMs
                }).ToArray();
                attempts = blob.Attempts.Select(attempt => new
                {
                    number = attempt.Number,
                    status = attempt.Status,
                    startedAt = attempt.StartedAt,
                    completedAt = attempt.CompletedAt,
                    error = attempt.Error,
                    stages = attempt.Stages.Select(stage => new
                    {
                        id = stage.Id,
                        status = stage.Status,
                        attempt = stage.Attempt,
                        startedAt = stage.StartedAt,
                        completedAt = stage.CompletedAt,
                        durationMs = stage.DurationMs
                    }).ToArray()
                }).ToArray();
                currentAttempt = blob.CurrentAttempt;
                maxAttempts = blob.MaxAttempts;
                nextRetryAt = blob.NextRetryAt;
                retryReason = blob.RetryReason;
                blobError = blob.Error;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read job phase blob for {JobId}", jobId);
        }

        // Build result. Durable terminal state wins over stale blob phase.
        object? result = null;
        string? error = null;
        if (isTerminal)
        {
            currentPhase = null; // suppress stale phase on terminal states
            currentStageId = null;
            if (status == "done")
            {
                var resolved = ResolveCompletedResult(metadata.SerializedOutput);
                status = resolved.Status;
                result = resolved.Result;
                error = resolved.Error;
            }
            if (status == "done")
                progressPercent = 100;
            if (status == "failed")
            {
                error ??= ExtractFailureMessage(metadata, blobError);
                _logger.LogWarning(
                    "Job {JobId} failed. FailureDetails present: {HasDetails}. ErrorType: {ErrorType}. Resolved error: {Error}",
                    jobId,
                    metadata.FailureDetails is not null,
                    metadata.FailureDetails?.ErrorType,
                    error);
            }
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            jobId,
            status,
            currentPhase,
            currentStageId,
            pipelineVersion,
            progressPercent,
            stages,
            attempts,
            currentAttempt,
            maxAttempts,
            nextRetryAt,
            retryReason,
            runtimeStatus = metadata.RuntimeStatus.ToString(),
            createdAt = metadata.CreatedAt,
            lastUpdatedAt = metadata.LastUpdatedAt,
            result,
            error
        }, ct);
        return response;
    }

    public static (string Status, JsonElement? Result, string? Error) ResolveCompletedResult(string? serializedOutput)
    {
        if (string.IsNullOrWhiteSpace(serializedOutput))
            return ("failed", null, "Generation completed without a result.");
        try
        {
            var json = JsonSerializer.Deserialize<JsonElement>(serializedOutput);
            var result = json.Deserialize<CaseV2JobResult>(ResultJsonOpts);
            if (result is null || string.IsNullOrWhiteSpace(result.CaseId))
                return ("failed", json, "Generation completed with an invalid result.");

            var quality = result.Quality;
            if (quality is not null && (quality.Issues is null || quality.Issues.Any(issue => issue is null)))
                return ("failed", json, "Generation completed with an invalid quality report.");
            var failed = result.HasErrors || result.ValidationErrorsCount > 0
                || (result.Bundle is not null && (!result.Bundle.Verified || (quality is not null && result.Bundle.Revision != quality.Revision)))
                || (quality is not null && (!quality.Approved || quality.HasBlockingIssues
                    || (quality.PersistenceRequested && !quality.Persisted)
                    || (quality.PublicationRequested && !quality.Published)));
            return (
                failed ? "failed" : "done",
                json,
                failed
                    ? string.IsNullOrWhiteSpace(result.ErrorMessage) ? "Generation finished with blocking quality issues." : result.ErrorMessage
                    : null);
        }
        catch (JsonException ex)
        {
            return ("failed", null, $"Generation result could not be read: {ex.Message}");
        }
    }

    /// <summary>
    /// Build a human-readable failure message from the Durable orchestration metadata.
    /// Walks <see cref="TaskFailureDetails.InnerFailure"/> so the actual root-cause exception
    /// is surfaced even when Durable wrapped it in a <c>TaskFailedException</c>. Falls back to
    /// the blob-reported error and finally to a generic message so the GET response is never empty.
    /// </summary>
    private static string ExtractFailureMessage(OrchestrationMetadata metadata, string? blobError)
    {
        var details = metadata.FailureDetails;
        if (details is not null)
        {
            // Walk to the deepest InnerFailure that still has a message — that's the true root cause.
            var deepest = details;
            while (deepest.InnerFailure is not null
                   && !string.IsNullOrWhiteSpace(deepest.InnerFailure.ErrorMessage))
            {
                deepest = deepest.InnerFailure;
            }

            if (!string.IsNullOrWhiteSpace(deepest.ErrorMessage))
            {
                return !string.IsNullOrWhiteSpace(deepest.ErrorType)
                    ? $"{deepest.ErrorType}: {deepest.ErrorMessage}"
                    : deepest.ErrorMessage;
            }

            // Last resort within FailureDetails: surface ErrorType alone.
            if (!string.IsNullOrWhiteSpace(details.ErrorType))
            {
                return $"Orchestration failed: {details.ErrorType}";
            }
        }

        if (!string.IsNullOrWhiteSpace(blobError)) return blobError;

        // Some failure modes (worker crash, DI failure inside activity ctor) end up in SerializedOutput
        // rather than FailureDetails. Surface a trimmed version so the caller has *something* to debug with.
        if (!string.IsNullOrWhiteSpace(metadata.SerializedOutput))
        {
            var snippet = metadata.SerializedOutput;
            if (snippet.Length > 600) snippet = snippet.Substring(0, 600) + "…";
            return $"Orchestration failed (raw output): {snippet}";
        }

        return "Orchestration failed without details";
    }

    private static (string Status, bool IsTerminal) MapStatus(OrchestrationRuntimeStatus s) => s switch
    {
        OrchestrationRuntimeStatus.Pending => ("queued", false),
        OrchestrationRuntimeStatus.Running => ("running", false),
        OrchestrationRuntimeStatus.Suspended => ("queued", false),
        OrchestrationRuntimeStatus.Completed => ("done", true),
        OrchestrationRuntimeStatus.Failed => ("failed", true),
        OrchestrationRuntimeStatus.Terminated => ("failed", true),
        _ => (s.ToString().ToLowerInvariant(), false)
    };
}
