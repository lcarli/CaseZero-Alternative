using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using CaseGen.Functions.Models.CaseV2;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Services.CaseV2;

public interface ICaseV2BlobPublisher
{
    /// <summary>Whether the publisher has a connection string and a container configured.</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Uploads case.json + every rendered asset under <c>cases/&lt;caseId&gt;/</c> (filesystem) to the
    /// blob container the website's <c>CaseV2StorageService</c> reads from, so that production
    /// users see the case immediately after generation. Returns the number of blobs uploaded.
    /// </summary>
    Task<int> PublishAsync(string caseId, string caseJsonLocalPath, string assetsLocalDir, CancellationToken ct = default);
}

/// <summary>
/// Mirrors the freshly-generated case (case.json + assets/*) to Azure Blob Storage so the
/// site's CaseV2StorageService (running on a different host in production) can pick it up
/// from the shared `bundles` container. In local dev it can be disabled by leaving the
/// connection string empty or pointing it at `UseDevelopmentStorage=true` while Azurite
/// is off — the publisher returns 0 and logs a warning instead of failing the pipeline.
/// </summary>
public class CaseV2BlobPublisher : ICaseV2BlobPublisher
{
    private readonly BlobServiceClient? _client;
    private readonly string _container;
    private readonly ILogger<CaseV2BlobPublisher> _logger;
    public bool IsConfigured { get; }

    public CaseV2BlobPublisher(IConfiguration configuration, ILogger<CaseV2BlobPublisher> logger)
    {
        _logger = logger;
        _container = configuration["CaseGeneratorStorage:BundlesContainer"] ?? "bundles";
        try
        {
            _client = BlobServiceClientFactory.Create(configuration);
            IsConfigured = true;
        }
        catch (InvalidOperationException ex)
        {
            IsConfigured = false;
            _logger.LogInformation(ex, "CaseV2BlobPublisher disabled — no storage configuration (MI or connection string) found");
        }
    }

    public async Task<int> PublishAsync(string caseId, string caseJsonLocalPath, string assetsLocalDir, CancellationToken ct = default)
    {
        if (!IsConfigured || _client is null) return 0;

        try
        {
            var container = _client.GetBlobContainerClient(_container);
            await container.CreateIfNotExistsAsync(cancellationToken: ct);

            var uploaded = 0;

            // 1) case.json
            if (File.Exists(caseJsonLocalPath))
            {
                await UploadFileAsync(container, $"{caseId}/case.json", caseJsonLocalPath, "application/json", ct);
                uploaded++;
            }

            // 2) every asset under assets/*
            if (Directory.Exists(assetsLocalDir))
            {
                foreach (var file in Directory.EnumerateFiles(assetsLocalDir, "*", SearchOption.TopDirectoryOnly))
                {
                    var name = Path.GetFileName(file);
                    var blobPath = $"{caseId}/assets/{name}";
                    var contentType = GuessContentType(file);
                    await UploadFileAsync(container, blobPath, file, contentType, ct);
                    uploaded++;
                }
            }

            _logger.LogInformation("Published case {CaseId} to blob container {Container} ({N} blobs)", caseId, _container, uploaded);
            return uploaded;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish case {CaseId} to blob storage — case stays in local filesystem only", caseId);
            return 0;
        }
    }

    private static async Task UploadFileAsync(BlobContainerClient container, string blobPath, string localFile, string contentType, CancellationToken ct)
    {
        var blob = container.GetBlobClient(blobPath);
        await using var stream = File.OpenRead(localFile);
        await blob.UploadAsync(stream, new BlobHttpHeaders { ContentType = contentType }, cancellationToken: ct);
    }

    private static string GuessContentType(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".mp3" => "audio/mpeg",
            ".mp4" => "video/mp4",
            ".wav" => "audio/wav",
            ".json" => "application/json",
            ".txt" or ".md" => "text/plain; charset=utf-8",
            _ => "application/octet-stream"
        };
    }
}
