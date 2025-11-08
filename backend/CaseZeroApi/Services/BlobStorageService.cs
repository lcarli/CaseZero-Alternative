using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System.Text.Json;

namespace CaseZeroApi.Services;

public class BlobStorageService : IBlobStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BlobStorageService> _logger;
    private readonly string _bundlesContainer;
    private readonly string _storageAccountUrl;

    public BlobStorageService(
        IConfiguration configuration,
        ILogger<BlobStorageService> logger)
    {
        // Try multiple sources for connection string, with Azurite as fallback for local development
        var connectionString = configuration["CaseGeneratorStorage:ConnectionString"]
            ?? configuration["AzureWebJobsStorage"]
            ?? Environment.GetEnvironmentVariable("AzureWebJobsStorage")
            ?? "UseDevelopmentStorage=true"; // Azurite default connection string

        _blobServiceClient = new BlobServiceClient(connectionString);
        _configuration = configuration;
        _logger = logger;
        _bundlesContainer = configuration["CaseGeneratorStorage:BundlesContainer"] ?? "bundles";
        
        _logger.LogInformation("BlobStorageService initialized with container: {Container}", _bundlesContainer);
        
        // Extract storage account URL from connection string for generating media URLs
        _storageAccountUrl = ExtractStorageAccountUrl(connectionString);
    }

    public async Task<List<CaseManifest>> ListCasesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_bundlesContainer);
            var cases = new List<CaseManifest>();

            // List all case folders (they follow pattern: CASE-YYYYMMDD-xxxxxxxx/)
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

            // Sort by generated date, newest first
            return cases.OrderByDescending(c => c.GeneratedAt).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list cases from blob storage");
            throw;
        }
    }

    public async Task<Models.NormalizedCaseBundle?> GetCaseBundleAsync(
        string caseId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_bundlesContainer);
            var bundlePath = $"{caseId}/normalized_case.json";
            var blobClient = containerClient.GetBlobClient(bundlePath);

            if (!await blobClient.ExistsAsync(cancellationToken))
            {
                _logger.LogWarning("Case bundle not found: {CaseId}", caseId);
                return null;
            }

            var response = await blobClient.DownloadContentAsync(cancellationToken);
            var bundle = JsonSerializer.Deserialize<Models.NormalizedCaseBundle>(
                response.Value.Content.ToString(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return bundle;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get case bundle for {CaseId}", caseId);
            throw;
        }
    }

    public async Task<string?> GetCaseDocumentAsync(
        string caseId,
        string documentId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_bundlesContainer);
            var documentPath = $"{caseId}/documents/{documentId}.json";
            var blobClient = containerClient.GetBlobClient(documentPath);

            if (!await blobClient.ExistsAsync(cancellationToken))
            {
                _logger.LogWarning("Document not found: {CaseId}/{DocumentId}", caseId, documentId);
                return null;
            }

            var response = await blobClient.DownloadContentAsync(cancellationToken);
            return response.Value.Content.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get document {DocumentId} for case {CaseId}", documentId, caseId);
            throw;
        }
    }

    public string GetMediaUrl(string caseId, string fileName)
    {
        // Generate URL for media file in format: {storageUrl}/bundles/{caseId}/media/{fileName}
        return $"{_storageAccountUrl}/{_bundlesContainer}/{caseId}/media/{fileName}";
    }

    private string ExtractStorageAccountUrl(string connectionString)
    {
        // For development (Azurite), use http://127.0.0.1:10000/devstoreaccount1
        if (connectionString.Contains("UseDevelopmentStorage=true") || 
            connectionString.Contains("127.0.0.1:10000"))
        {
            return "http://127.0.0.1:10000/devstoreaccount1";
        }

        // For Azure Storage, extract from connection string
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
