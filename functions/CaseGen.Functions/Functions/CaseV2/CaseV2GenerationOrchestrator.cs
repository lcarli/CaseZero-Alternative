using System.Net;
using System.Text.Json;
using CaseGen.Functions.Models.CaseV2;
using CaseGen.Functions.Services.CaseV2;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
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

        // Single activity wraps the whole generator. Splitting per-phase would give better
        // crash recovery but requires refactoring CaseV2GeneratorService — deferred (see plan.md).
        var result = await context.CallActivityAsync<CaseV2JobResult>(ActivityName, input);

        logger.LogInformation(
            "[CaseV2] Orchestration {InstanceId} finished jobId={JobId} caseId={CaseId} hasErrors={HasErrors} validationErrors={Errors}",
            context.InstanceId, input.JobId, result.CaseId, result.HasErrors, result.ValidationErrorsCount);

        return result;
    }
}

/// <summary>
/// Input passed from starter → orchestrator → activity.
/// </summary>
public record CaseV2OrchestrationInput(GenerateCaseV2Request Request, string JobId);

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
    string? RedTeamVerdict,
    CaseQualityReport? Quality = null,
    int AssetsSkipped = 0,
    CaseRepairReport? Repair = null,
    CaseBundleReport? Bundle = null)
{
    public static CaseV2JobResult FromResponse(string jobId, GenerateCaseV2Response response)
    {
        var quality = response.Quality;
        var hasErrors = response.ValidationErrors.Count > 0 || response.AssetRenderingErrors.Count > 0
            || !quality.Approved || quality.HasBlockingIssues
            || (response.Bundle is not null && (!response.Bundle.Verified || response.Bundle.Revision != quality.Revision))
            || (quality.PersistenceRequested && !quality.Persisted)
            || (quality.PublicationRequested && !quality.Published);
        var blockers = quality.Issues.Where(issue => issue.Blocking).ToList();
        var messages = blockers
            .Select(issue => $"{issue.Code}: {issue.Message}")
            .ToList();
        var reportedMessages = blockers.Select(issue => issue.Message).ToHashSet(StringComparer.Ordinal);
        messages.AddRange(response.ValidationErrors.Concat(response.AssetRenderingErrors)
            .Where(message => !reportedMessages.Contains(message))
            .Distinct(StringComparer.Ordinal));
        var error = hasErrors
            ? (messages.Count > 0
                ? string.Join(" · ", messages)
                : "Generation did not pass quality checks or complete the requested outputs.")
            : null;
        return new CaseV2JobResult(
            jobId, response.CaseId, response.OutputPath, response.ValidationErrors.Count,
            response.AssetsRenderedPdfs, response.AssetsRenderedImages, response.BlobsPublished,
            hasErrors, error, response.StageLatencyMs, response.AutoFixesApplied,
            response.RefineAttempted, response.RefineErrorsBefore, response.RefineErrorsAfter,
            response.RedTeam?.Verdict, quality, response.AssetsSkipped, response.Repair, response.Bundle);
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

        await reporter.ReportStartedAsync(ct);
        _logger.LogInformation("[CaseV2] Activity start jobId={JobId}", input.JobId);

        try
        {
            var response = await _generator.GenerateAsync(input.Request, reporter, ct);

            var result = CaseV2JobResult.FromResponse(input.JobId, response);
            if (result.HasErrors)
            {
                _logger.LogWarning("[CaseV2] Activity completed with blockers jobId={JobId}: {Error}",
                    input.JobId, result.ErrorMessage);
                await reporter.ReportFailedAsync(result.ErrorMessage!, ct);
            }
            else
            {
                await reporter.ReportCompletedAsync(ct);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CaseV2] Activity failed jobId={JobId}", input.JobId);
            await reporter.ReportFailedAsync(ex.Message, CancellationToken.None);
            // Re-throw so Durable marks the orchestration Failed and the GET endpoint reports it.
            throw;
        }
    }
}

/// <summary>
/// HTTP entrypoint: enqueues a Durable orchestration and returns 202 + jobId.
/// Enforces single-instance concurrency best-effort (see plan.md for race window discussion).
/// </summary>
public class StartCaseV2GenerationFunction
{
    private readonly ILogger<StartCaseV2GenerationFunction> _logger;

    public StartCaseV2GenerationFunction(ILogger<StartCaseV2GenerationFunction> logger)
    {
        _logger = logger;
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
        var input = new CaseV2OrchestrationInput(request, jobId);

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
        string? blobError = null;
        try
        {
            var reporter = _reporterFactory.Create(jobId);
            var blob = await reporter.GetAsync(ct);
            if (blob is not null)
            {
                currentPhase = blob.CurrentPhase;
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
            if (metadata.RuntimeStatus == OrchestrationRuntimeStatus.Completed)
            {
                var completed = ResolveCompletedResult(metadata.SerializedOutput);
                status = completed.Status;
                result = completed.Result;
                error = completed.Error;
                if (status == "failed")
                    _logger.LogWarning("Completed job {JobId} has a logical failure: {Error}", jobId, error);
            }
            else if (status == "failed")
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
