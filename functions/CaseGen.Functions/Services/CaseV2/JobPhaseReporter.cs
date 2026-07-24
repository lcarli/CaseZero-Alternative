using System.Text.Json;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Services.CaseV2;

/// <summary>
/// Reports the current pipeline phase for a v2 generation job so that
/// <c>GET /api/cases/v2/jobs/{jobId}</c> can show "running:phase" without
/// requiring the orchestrator to be split into one Durable activity per phase.
/// </summary>
public interface IJobPhaseReporter
{
    /// <summary>Whether the reporter will actually persist anything. False when storage isn't configured (local dev with Azurite off).</summary>
    bool IsConfigured { get; }

    /// <summary>Mark the job as having started (status=running, no phase yet).</summary>
    Task ReportStartedAsync(CancellationToken ct = default);

    /// <summary>Update the current phase. Safe to call concurrently with reads; phases form a monotonic log.</summary>
    Task ReportPhaseAsync(string phase, CancellationToken ct = default);

    /// <summary>Mark the job as finished successfully.</summary>
    Task ReportCompletedAsync(CancellationToken ct = default);

    /// <summary>Mark the job as failed (the GET endpoint will still prioritise Durable terminal state for the final verdict).</summary>
    Task ReportFailedAsync(string error, CancellationToken ct = default);

    /// <summary>Fetch the latest status document. Returns null when never written or storage not configured.</summary>
    Task<JobPhaseStatus?> GetAsync(CancellationToken ct = default);
}

public static class GenerationProgressCatalog
{
    public const string PipelineVersion = "casegraph-v1";

    public static readonly IReadOnlyList<string> StageIds =
    [
        "caseDesign",
        "graphConstruction",
        "evidenceProduction",
        "forensicWorkflow",
        "solutionDesign",
        "deterministicValidation",
        "solutionWitness",
        "advisoryReview",
        "targetedRepair",
        "finalValidation",
        "finalization"
    ];

    public static string MapInternalPhase(string phase) =>
        phase switch
        {
            "caseBible" or "plotOutline" or "suspectCards" => "caseDesign",
            "assetPlan" => "graphConstruction",
            "assetsAndTimelineAndBriefing" or "rookieInitialEvidence" => "evidenceProduction",
            "forensicsPlan" or "outcomesAndInitialEmails" => "forensicWorkflow",
            "mechanicalRules" or "rulesAndSolutionSkeleton" or "questionsAndExplanation" => "solutionDesign",
            "consistency" or "autoFixSchema" => "deterministicValidation",
            "solutionWitness" or "redTeamAndSolver" => "solutionWitness",
            "advisoryReview" or "redTeamRerun" => "advisoryReview",
            "targetedRepair" or "refineCase" => "targetedRepair",
            "finalValidation" => "finalValidation",
            "renderAssets" or "publishToBlob" => "finalization",
            _ => phase
        };

    public static int StageIndex(string stageId)
    {
        for (var index = 0; index < StageIds.Count; index++)
            if (string.Equals(StageIds[index], stageId, StringComparison.Ordinal))
                return index;
        return -1;
    }
}

public sealed class GenerationStageProgress
{
    public string Id { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public int Attempt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public double? DurationMs { get; set; }
}

/// <summary>Versioned progress document persisted to blob.</summary>
public sealed class JobPhaseStatus
{
    public string JobId { get; set; } = string.Empty;
    public string Status { get; set; } = "queued";
    public string? CurrentPhase { get; set; }
    public string? CurrentStageId { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string PipelineVersion { get; set; } = GenerationProgressCatalog.PipelineVersion;
    public double ProgressPercent { get; set; }
    public List<GenerationStageProgress> Stages { get; set; } = new();

    public static JobPhaseStatus Started(string jobId, DateTimeOffset now) => new()
    {
        JobId = jobId,
        Status = "running",
        UpdatedAt = now,
        Stages = GenerationProgressCatalog.StageIds
            .Select(id => new GenerationStageProgress { Id = id })
            .ToList()
    };

    public void StartStage(string internalPhase, DateTimeOffset now, bool retry)
    {
        var stageId = GenerationProgressCatalog.MapInternalPhase(internalPhase);
        if (string.Equals(CurrentStageId, stageId, StringComparison.Ordinal))
        {
            var active = FindOrAdd(stageId);
            if (retry)
                active.Attempt++;
            CurrentPhase = internalPhase;
            UpdatedAt = now;
            return;
        }
        CompleteCurrentStage(now);
        SkipStagesBefore(stageId, now);
        var stage = FindOrAdd(stageId);
        if (stage.Status != "running")
        {
            stage.Attempt = Math.Max(1, stage.Attempt + 1);
            stage.StartedAt = now;
            stage.CompletedAt = null;
            stage.Status = "running";
        }
        else if (retry)
        {
            stage.Attempt++;
        }
        CurrentPhase = internalPhase;
        CurrentStageId = stageId;
        Status = "running";
        Error = null;
        UpdatedAt = now;
        RecalculateProgress();
    }

    public void Complete(DateTimeOffset now)
    {
        CompleteCurrentStage(now);
        foreach (var stage in Stages.Where(stage => stage.Status == "pending"))
        {
            stage.Status = "skipped";
            stage.CompletedAt = now;
            stage.DurationMs = 0;
        }
        CurrentPhase = null;
        CurrentStageId = null;
        Status = "done";
        ProgressPercent = 100;
        UpdatedAt = now;
    }

    public void Fail(string error, DateTimeOffset now)
    {
        var stage = CurrentStageId is null ? null : Stages.FirstOrDefault(item => item.Id == CurrentStageId);
        if (stage is not null && stage.Status == "running")
        {
            stage.Status = "failed";
            stage.CompletedAt = now;
            stage.DurationMs = Elapsed(stage, now);
        }
        Status = "failed";
        Error = error;
        UpdatedAt = now;
        RecalculateProgress();
    }

    private void CompleteCurrentStage(DateTimeOffset now)
    {
        if (CurrentStageId is null)
            return;
        var current = Stages.FirstOrDefault(stage => stage.Id == CurrentStageId);
        if (current is null || current.Status != "running")
            return;
        current.Status = "completed";
        current.CompletedAt = now;
        current.DurationMs = Elapsed(current, now);
    }

    private void SkipStagesBefore(string stageId, DateTimeOffset now)
    {
        var targetIndex = GenerationProgressCatalog.StageIndex(stageId);
        if (targetIndex < 0)
            return;
        foreach (var stage in Stages.Where(stage =>
                     stage.Status == "pending"
                     && GenerationProgressCatalog.StageIndex(stage.Id) < targetIndex))
        {
            stage.Status = "skipped";
            stage.CompletedAt = now;
            stage.DurationMs = 0;
        }
    }

    private GenerationStageProgress FindOrAdd(string stageId)
    {
        var stage = Stages.FirstOrDefault(item => item.Id == stageId);
        if (stage is not null)
            return stage;
        stage = new GenerationStageProgress { Id = stageId };
        Stages.Add(stage);
        return stage;
    }

    private void RecalculateProgress()
    {
        var completed = Stages.Count(stage => stage.Status is "completed" or "skipped");
        var calculated = Stages.Count == 0 ? 0 : Math.Round(completed * 100d / Stages.Count, 1);
        ProgressPercent = Math.Max(ProgressPercent, calculated);
    }

    private static double Elapsed(GenerationStageProgress stage, DateTimeOffset now) =>
        stage.StartedAt is null ? 0 : Math.Round((now - stage.StartedAt.Value).TotalMilliseconds, 1);
}

/// <summary>Factory so the orchestrator / activity can create a reporter scoped to a specific jobId.</summary>
public interface IJobPhaseReporterFactory
{
    IJobPhaseReporter Create(string jobId);
}

internal sealed class NullJobPhaseReporter : IJobPhaseReporter
{
    public static readonly NullJobPhaseReporter Instance = new();
    public bool IsConfigured => false;
    public Task ReportStartedAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task ReportPhaseAsync(string phase, CancellationToken ct = default) => Task.CompletedTask;
    public Task ReportCompletedAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task ReportFailedAsync(string error, CancellationToken ct = default) => Task.CompletedTask;
    public Task<JobPhaseStatus?> GetAsync(CancellationToken ct = default) => Task.FromResult<JobPhaseStatus?>(null);
}

internal sealed class BlobJobPhaseReporter : IJobPhaseReporter
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };
    private readonly BlobClient _blob;
    private readonly string _jobId;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private JobPhaseStatus _status;
    private string? _lastInternalPhase;

    public bool IsConfigured => true;

    public BlobJobPhaseReporter(BlobClient blob, string jobId, ILogger logger)
    {
        _blob = blob;
        _jobId = jobId;
        _logger = logger;
        _status = JobPhaseStatus.Started(jobId, DateTimeOffset.UtcNow);
    }

    public async Task ReportStartedAsync(CancellationToken ct = default)
    {
        _status = JobPhaseStatus.Started(_jobId, DateTimeOffset.UtcNow);
        _lastInternalPhase = null;
        await WriteAsync(ct);
    }

    public async Task ReportPhaseAsync(string phase, CancellationToken ct = default)
    {
        var retry = string.Equals(phase, "targetedRepair", StringComparison.Ordinal)
                    && string.Equals(_lastInternalPhase, phase, StringComparison.Ordinal);
        _status.StartStage(phase, DateTimeOffset.UtcNow, retry);
        _lastInternalPhase = phase;
        await WriteAsync(ct);
    }

    public async Task ReportCompletedAsync(CancellationToken ct = default)
    {
        _status.Complete(DateTimeOffset.UtcNow);
        await WriteAsync(ct);
    }

    public async Task ReportFailedAsync(string error, CancellationToken ct = default)
    {
        _status.Fail(error, DateTimeOffset.UtcNow);
        await WriteAsync(ct);
    }

    public async Task<JobPhaseStatus?> GetAsync(CancellationToken ct = default)
    {
        try
        {
            if (!await _blob.ExistsAsync(ct)) return null;
            var download = await _blob.DownloadContentAsync(ct);
            return JsonSerializer.Deserialize<JobPhaseStatus>(download.Value.Content.ToString(), JsonOpts);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read job status blob for {JobId}", _jobId);
            return null;
        }
    }

    private async Task WriteAsync(CancellationToken ct)
    {
        await _writeLock.WaitAsync(ct);
        try
        {
            var json = JsonSerializer.Serialize(_status, JsonOpts);
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
            await _blob.UploadAsync(stream, new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = "application/json" }
            }, ct);
        }
        catch (Exception ex)
        {
            // Phase reporting is best-effort — never fail the generation because of it.
            _logger.LogWarning(ex, "Failed to write job status blob for {JobId} (phase={Phase})", _jobId, _status.CurrentPhase ?? _status.Status);
        }
        finally
        {
            _writeLock.Release();
        }
    }
}

/// <summary>
/// Default factory: creates <see cref="BlobJobPhaseReporter"/> when storage is configured,
/// otherwise returns a no-op reporter so local dev without Azurite still works.
/// </summary>
public sealed class JobPhaseReporterFactory : IJobPhaseReporterFactory
{
    private const string ContainerName = "jobs";
    private readonly BlobContainerClient? _container;
    private readonly ILogger<JobPhaseReporterFactory> _logger;
    private bool _containerEnsured;
    private readonly SemaphoreSlim _ensureLock = new(1, 1);

    public JobPhaseReporterFactory(IConfiguration configuration, ILogger<JobPhaseReporterFactory> logger)
    {
        _logger = logger;
        try
        {
            var client = BlobServiceClientFactory.Create(configuration);
            _container = client.GetBlobContainerClient(ContainerName);
        }
        catch (InvalidOperationException ex)
        {
            _container = null;
            _logger.LogInformation(ex, "JobPhaseReporterFactory disabled — no storage configuration (MI or connection string) found");
        }
    }

    public IJobPhaseReporter Create(string jobId)
    {
        if (_container is null) return NullJobPhaseReporter.Instance;
        EnsureContainerExists();
        var blob = _container.GetBlobClient($"{jobId}/status.json");
        return new BlobJobPhaseReporter(blob, jobId, _logger);
    }

    private void EnsureContainerExists()
    {
        if (_containerEnsured || _container is null) return;
        _ensureLock.Wait();
        try
        {
            if (_containerEnsured) return;
            _container.CreateIfNotExists();
            _containerEnsured = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to ensure 'jobs' container exists — status reporting will likely fail silently");
        }
        finally
        {
            _ensureLock.Release();
        }
    }
}
