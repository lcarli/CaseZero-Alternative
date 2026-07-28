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
/// orchestrator delegates to a single long-running activity, and a GET endpoint
/// composes Durable runtime status + per-phase blob status so callers can poll.
/// </summary>
public class CaseV2GenerationOrchestrator
{
    public const string OrchestratorName = "CaseV2GenerationOrchestrator";
    public const string ActivityName = "CaseV2GenerateActivity";
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

        CaseV2JobResult? result = null;
        for (var attempt = 1; attempt <= input.MaxAttempts; attempt++)
        {
            var attemptInput = input with { Attempt = attempt };
            result = await context.CallActivityAsync<CaseV2JobResult>(ActivityName, attemptInput);
            if (!result.HasErrors)
                break;
            if (attempt == input.MaxAttempts)
                break;

            var backoffSeconds = Math.Min(300, 15 * (1 << Math.Min(attempt - 1, 4)));
            await context.CreateTimer(
                context.CurrentUtcDateTime.AddSeconds(backoffSeconds),
                CancellationToken.None);
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
    double SolverScore);

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

            var hasErrors = response.ValidationErrors.Count > 0;
            if (hasErrors)
            {
                _logger.LogWarning("[CaseV2] Activity completed with validation errors jobId={JobId} errors={Count}",
                    input.JobId, response.ValidationErrors.Count);
                var error = $"Validation failed with {response.ValidationErrors.Count} error(s): "
                            + string.Join(" · ", response.ValidationErrors.Take(3));
                if (input.Attempt < input.MaxAttempts)
                    await reporter.ReportRetryScheduledAsync(error, RetryAt(input.Attempt), ct);
                else
                    await reporter.ReportFailedAsync(error, ct);
            }
            else
            {
                await reporter.ReportCompletedAsync(ct);
            }

            return new CaseV2JobResult(
                JobId: input.JobId,
                CaseId: response.CaseId,
                OutputPath: response.OutputPath,
                ValidationErrorsCount: response.ValidationErrors.Count,
                AssetsRenderedPdfs: response.AssetsRenderedPdfs,
                AssetsRenderedImages: response.AssetsRenderedImages,
                BlobsPublished: response.BlobsPublished,
                HasErrors: hasErrors,
                ErrorMessage: hasErrors ? string.Join(" · ", response.ValidationErrors) : null,
                StageLatencyMs: response.StageLatencyMs,
                AutoFixesApplied: response.AutoFixesApplied,
                RefineAttempted: response.RefineAttempted,
                RefineErrorsBefore: response.RefineErrorsBefore,
                RefineErrorsAfter: response.RefineErrorsAfter,
                RefineIterations: response.RefineIterations,
                InputTokens: response.InputTokens,
                OutputTokens: response.OutputTokens,
                CaseGraphEnabled: response.CaseGraphEnabled,
                FinalValidation: response.FinalValidation,
                GraphValidationPassed: response.FinalValidation?.Issues.All(issue =>
                    issue.Gate != FinalValidationGate.Graph || !issue.Blocking) == true,
                FirstPassSuccess: !response.RefineAttempted && response.ValidationErrors.Count == 0,
                RepairPlateauCount: response.RepairPlateauCount,
                RepairOperationCount: response.RepairOperationCount,
                SolverSucceeded: response.Solver?.Correct == true,
                EvidenceLayoutDiversity: response.EvidenceLayoutDiversity,
                EvidenceLayouts: response.EvidenceLayouts,
                SpecialistFindingsByCategory: response.SpecialistFindingsByCategory,
                RedTeamVerdict: response.RedTeam?.Verdict,
                RedTeamVerdictInitial: response.RedTeamInitial?.Verdict,
                RedTeamVerdictTrajectory: response.RedTeamVerdictTrajectory,
                RedTeamRerun: response.RedTeamRerun,
                AttemptCount: input.Attempt,
                MaxAttempts: input.MaxAttempts,
                SolverScore: response.Solver?.Score ?? 0);
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
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

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
            using var body = req.Body;
            if (body.CanSeek && body.Length == 0)
            {
                request = new GenerateCaseV2Request();
            }
            else
            {
                request = await JsonSerializer.DeserializeAsync<GenerateCaseV2Request>(body, JsonOpts, ct)
                          ?? new GenerateCaseV2Request();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse GenerateCaseV2Request body — falling back to defaults");
            request = new GenerateCaseV2Request();
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
        catch (Exception ex)
        {
            // Failure to enforce singleton is non-fatal — log and let the request proceed.
            _logger.LogWarning(ex, "Singleton check failed — proceeding without enforcement");
        }

        // 3) schedule the orchestration with an explicit, unique instanceId (== jobId)
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
        catch (Exception ex)
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
            if (status == "done" && !string.IsNullOrEmpty(metadata.SerializedOutput))
            {
                try
                {
                    var output = JsonSerializer.Deserialize<JsonElement>(metadata.SerializedOutput);
                    result = output;
                    var hasErrorsProperty =
                        output.TryGetProperty(nameof(CaseV2JobResult.HasErrors), out var hasErrors)
                        || output.TryGetProperty("hasErrors", out hasErrors);
                    if (hasErrorsProperty && hasErrors.ValueKind == JsonValueKind.True)
                    {
                        status = "failed";
                    }
                }
                catch
                {
                    result = metadata.SerializedOutput;
                }
            }
            if (status == "done")
                progressPercent = 100;
            if (status == "failed")
            {
                error = ExtractFailureMessage(metadata, blobError);
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
