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

/// <summary>Compact status doc persisted to blob.</summary>
public record JobPhaseStatus(string JobId, string Status, string? CurrentPhase, string? Error, DateTimeOffset UpdatedAt);

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

    public bool IsConfigured => true;

    public BlobJobPhaseReporter(BlobClient blob, string jobId, ILogger logger)
    {
        _blob = blob;
        _jobId = jobId;
        _logger = logger;
    }

    public Task ReportStartedAsync(CancellationToken ct = default)
        => WriteAsync(new JobPhaseStatus(_jobId, "running", null, null, DateTimeOffset.UtcNow), ct);

    public Task ReportPhaseAsync(string phase, CancellationToken ct = default)
        => WriteAsync(new JobPhaseStatus(_jobId, "running", phase, null, DateTimeOffset.UtcNow), ct);

    public Task ReportCompletedAsync(CancellationToken ct = default)
        => WriteAsync(new JobPhaseStatus(_jobId, "done", null, null, DateTimeOffset.UtcNow), ct);

    public Task ReportFailedAsync(string error, CancellationToken ct = default)
        => WriteAsync(new JobPhaseStatus(_jobId, "failed", null, error, DateTimeOffset.UtcNow), ct);

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

    private async Task WriteAsync(JobPhaseStatus status, CancellationToken ct)
    {
        try
        {
            var json = JsonSerializer.Serialize(status, JsonOpts);
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
            await _blob.UploadAsync(stream, new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = "application/json" }
            }, ct);
        }
        catch (Exception ex)
        {
            // Phase reporting is best-effort — never fail the generation because of it.
            _logger.LogWarning(ex, "Failed to write job status blob for {JobId} (phase={Phase})", _jobId, status.CurrentPhase ?? status.Status);
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
