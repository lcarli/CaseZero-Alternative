using Azure.Storage.Blobs;
using System.Text.Json;

namespace CaseZeroApi.Services;

/// <summary>
/// Lightweight blob storage service used by the site to enumerate generated case
/// manifests and resolve media URLs. v0/v1 bundle handling has been removed —
/// the site reads case content via <see cref="ICaseV1StorageService"/> (case.json v2).
/// </summary>
public class BlobStorageService : IBlobStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<BlobStorageService> _logger;
    private readonly string _bundlesContainer;
    private readonly string _storageAccountUrl;

    public BlobStorageService(IConfiguration configuration, ILogger<BlobStorageService> logger)
    {
        var connectionString = configuration["CaseGeneratorStorage:ConnectionString"]
            ?? configuration["AzureWebJobsStorage"]
            ?? Environment.GetEnvironmentVariable("AzureWebJobsStorage")
            ?? "UseDevelopmentStorage=true";

        _blobServiceClient = new BlobServiceClient(connectionString);
        _logger = logger;
        _bundlesContainer = configuration["CaseGeneratorStorage:BundlesContainer"] ?? "bundles";
        _storageAccountUrl = ExtractStorageAccountUrl(connectionString);

        _logger.LogInformation("BlobStorageService initialized with container: {Container}", _bundlesContainer);
    }

    public async Task<List<CaseManifest>> ListCasesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_bundlesContainer);
            var cases = new List<CaseManifest>();

            await foreach (var item in containerClient.GetBlobsByHierarchyAsync(
                delimiter: "/",
                cancellationToken: cancellationToken))
            {
                if (item.IsPrefix && item.Prefix.StartsWith("CASE-"))
                {
                    var caseId = item.Prefix.TrimEnd('/');
                    var manifestPath = $"{caseId}/{caseId}.json";

                    try
                    {
                        var manifestBlob = containerClient.GetBlobClient(manifestPath);
                        var response = await manifestBlob.DownloadContentAsync(cancellationToken);
                        var manifest = JsonSerializer.Deserialize<CaseManifest>(
                            response.Value.Content.ToString(),
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                        if (manifest != null)
                        {
                            cases.Add(manifest);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to load manifest for case {CaseId}", caseId);
                    }
                }
            }

            return cases.OrderByDescending(c => c.GeneratedAt).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list cases from blob storage");
            throw;
        }
    }

    public async Task<CaseManifest?> GetCaseManifestAsync(string caseId, CancellationToken cancellationToken = default)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_bundlesContainer);
            var manifestPath = $"{caseId}/{caseId}.json";
            var blobClient = containerClient.GetBlobClient(manifestPath);

            if (!await blobClient.ExistsAsync(cancellationToken))
            {
                _logger.LogWarning("Case manifest not found: {CaseId}", caseId);
                return null;
            }

            var response = await blobClient.DownloadContentAsync(cancellationToken);
            return JsonSerializer.Deserialize<CaseManifest>(
                response.Value.Content.ToString(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get case manifest for {CaseId}", caseId);
            throw;
        }
    }

    public string GetMediaUrl(string caseId, string fileName)
    {
        return $"{_storageAccountUrl}/{_bundlesContainer}/{caseId}/media/{fileName}";
    }

    private string ExtractStorageAccountUrl(string connectionString)
    {
        if (connectionString.Contains("UseDevelopmentStorage=true") ||
            connectionString.Contains("127.0.0.1:10000"))
        {
            return "http://127.0.0.1:10000/devstoreaccount1";
        }

        var parts = connectionString.Split(';');
        var accountName = parts.FirstOrDefault(p => p.StartsWith("AccountName="))?.Split('=')[1];

        if (!string.IsNullOrEmpty(accountName))
        {
            return $"https://{accountName}.blob.core.windows.net";
        }

        _logger.LogWarning("Could not extract storage account URL from connection string");
        return string.Empty;
    }
}
