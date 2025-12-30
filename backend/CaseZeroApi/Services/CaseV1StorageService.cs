using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System.Text.Json;
using CaseZeroApi.Models.CaseV1;
using Microsoft.Extensions.Caching.Memory;

namespace CaseZeroApi.Services;

public class CaseV1StorageService : ICaseV1StorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ICaseV1SanitizerService _sanitizer;
    private readonly ILogger<CaseV1StorageService> _logger;
    private readonly IMemoryCache _cache;
    private readonly string _casesContainer;

    public CaseV1StorageService(
        IConfiguration configuration,
        ICaseV1SanitizerService sanitizer,
        ILogger<CaseV1StorageService> logger,
        IMemoryCache cache)
    {
        // Try multiple sources for connection string, with Azurite as fallback for local development
        var connectionString = configuration["CaseGeneratorStorage:ConnectionString"]
            ?? configuration["AzureWebJobsStorage"]
            ?? Environment.GetEnvironmentVariable("AzureWebJobsStorage")
            ?? "UseDevelopmentStorage=true"; // Azurite default connection string

        _blobServiceClient = new BlobServiceClient(connectionString);
        _sanitizer = sanitizer;
        _logger = logger;
        _cache = cache;
        _casesContainer = configuration["CaseGeneratorStorage:CasesContainer"] ?? "cases";
        
        _logger.LogInformation("CaseV1StorageService initialized with container: {Container}", _casesContainer);
    }

    public async Task<List<CaseV1Metadata>> ListCasesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_casesContainer);
            
            // Ensure container exists
            await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            
            var cases = new List<CaseV1Metadata>();

            // List all case folders (pattern: case_*/case.json)
            await foreach (var item in containerClient.GetBlobsByHierarchyAsync(
                delimiter: "/",
                cancellationToken: cancellationToken))
            {
                if (item.IsPrefix && item.Prefix.StartsWith("case_"))
                {
                    var caseId = item.Prefix.TrimEnd('/');
                    var caseJsonPath = $"{caseId}/case.json";
                    
                    try
                    {
                        var caseData = await GetCaseAsync(caseId, cancellationToken);
                        if (caseData != null)
                        {
                            cases.Add(new CaseV1Metadata
                            {
                                CaseId = caseData.CaseId,
                                Title = caseData.Metadata.Title,
                                Description = caseData.Metadata.Description,
                                Difficulty = caseData.Metadata.Difficulty,
                                Category = caseData.Metadata.Category,
                                EstimatedTimeMinutes = caseData.Metadata.EstimatedTimeMinutes
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to load case metadata for {CaseId}", caseId);
                    }
                }
            }

            return cases.OrderBy(c => c.CaseId).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list cases from blob storage");
            throw;
        }
    }

    public async Task<CaseV1?> GetCaseAsync(string caseId, CancellationToken cancellationToken = default)
    {
        var rawCase = await GetCaseRawAsync(caseId, cancellationToken);
        if (rawCase == null) return null;

        // 🔒 Sanitize before returning to client
        return _sanitizer.Sanitize(rawCase);
    }

    public async Task<CaseV1?> GetCaseRawAsync(string caseId, CancellationToken cancellationToken = default)
    {
        // 🚀 Cache key for this case
        var cacheKey = $"case_v1_{caseId}";

        // Try to get from cache first
        if (_cache.TryGetValue<CaseV1>(cacheKey, out var cachedCase))
        {
            _logger.LogDebug("Cache hit for case {CaseId}", caseId);
            return cachedCase;
        }

        _logger.LogDebug("Cache miss for case {CaseId}, loading from blob storage", caseId);

        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_casesContainer);
            var caseJsonPath = $"{caseId}/case.json";
            var blobClient = containerClient.GetBlobClient(caseJsonPath);

            if (!await blobClient.ExistsAsync(cancellationToken))
            {
                _logger.LogWarning("Case not found: {CaseId}", caseId);
                return null;
            }

            var response = await blobClient.DownloadContentAsync(cancellationToken);
            var caseData = JsonSerializer.Deserialize<CaseV1>(
                response.Value.Content.ToString(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (caseData != null)
            {
                // Store in cache with sliding expiration (30 minutes)
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetSlidingExpiration(TimeSpan.FromMinutes(30))
                    .SetAbsoluteExpiration(TimeSpan.FromHours(2))
                    .SetSize(1); // Size for cache size limit

                _cache.Set(cacheKey, caseData, cacheOptions);
                _logger.LogInformation("Cached case {CaseId} for 30 minutes sliding / 2 hours absolute", caseId);
            }

            return caseData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load case {CaseId}", caseId);
            throw;
        }
    }

    public async Task<Stream?> GetAssetAsync(string caseId, string assetId, CancellationToken cancellationToken = default)
    {
        try
        {
            // First load case.json to get asset filePath
            var caseData = await GetCaseRawAsync(caseId, cancellationToken);
            if (caseData == null)
            {
                _logger.LogWarning("Case not found when loading asset: {CaseId}", caseId);
                return null;
            }

            var asset = caseData.Assets.FirstOrDefault(a => a.AssetId == assetId);
            if (asset == null)
            {
                _logger.LogWarning("Asset not found: {CaseId}/{AssetId}", caseId, assetId);
                return null;
            }

            // Extract blob path from filePath
            // filePath format: "/cases/case_001/assets/briefing.pdf"
            // Need: "case_001/assets/briefing.pdf"
            var blobPath = asset.FilePath.TrimStart('/');
            if (blobPath.StartsWith("cases/"))
            {
                blobPath = blobPath.Substring("cases/".Length);
            }

            var containerClient = _blobServiceClient.GetBlobContainerClient(_casesContainer);
            var blobClient = containerClient.GetBlobClient(blobPath);

            if (!await blobClient.ExistsAsync(cancellationToken))
            {
                _logger.LogWarning("Asset blob not found: {BlobPath}", blobPath);
                return null;
            }

            var response = await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken);
            return response.Value.Content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load asset {CaseId}/{AssetId}", caseId, assetId);
            throw;
        }
    }

    public async Task<string?> GetAssetContentTypeAsync(string caseId, string assetId, CancellationToken cancellationToken = default)
    {
        try
        {
            var caseData = await GetCaseRawAsync(caseId, cancellationToken);
            if (caseData == null) return null;

            var asset = caseData.Assets.FirstOrDefault(a => a.AssetId == assetId);
            if (asset == null) return null;

            // Extract blob path
            var blobPath = asset.FilePath.TrimStart('/');
            if (blobPath.StartsWith("cases/"))
            {
                blobPath = blobPath.Substring("cases/".Length);
            }

            var containerClient = _blobServiceClient.GetBlobContainerClient(_casesContainer);
            var blobClient = containerClient.GetBlobClient(blobPath);

            if (!await blobClient.ExistsAsync(cancellationToken))
            {
                return null;
            }

            var properties = await blobClient.GetPropertiesAsync(conditions: null, cancellationToken: cancellationToken);
            return properties.Value.ContentType;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get content type for asset {CaseId}/{AssetId}", caseId, assetId);
            return "application/octet-stream"; // Fallback
        }
    }

    public async Task<bool> CaseExistsAsync(string caseId, CancellationToken cancellationToken = default)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_casesContainer);
            var caseJsonPath = $"{caseId}/case.json";
            var blobClient = containerClient.GetBlobClient(caseJsonPath);

            var response = await blobClient.ExistsAsync(cancellationToken);
            return response.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check if case exists: {CaseId}", caseId);
            return false;
        }
    }

    public async Task<string> GetCaseJsonAsync(string caseId, CancellationToken cancellationToken = default)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_casesContainer);
            var caseJsonPath = $"{caseId}/case.json";
            var blobClient = containerClient.GetBlobClient(caseJsonPath);

            if (!await blobClient.ExistsAsync(cancellationToken))
            {
                return string.Empty;
            }

            var downloadResponse = await blobClient.DownloadContentAsync(cancellationToken);
            return downloadResponse.Value.Content.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get case.json for {CaseId}", caseId);
            return string.Empty;
        }
    }

    public async Task<string> GetEmailAsync(string caseId, string emailId, CancellationToken cancellationToken = default)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_casesContainer);
            var emailPath = $"{caseId}/emails/{emailId}.json";
            var blobClient = containerClient.GetBlobClient(emailPath);

            if (!await blobClient.ExistsAsync(cancellationToken))
            {
                _logger.LogWarning("Email not found: {CaseId}/{EmailId}", caseId, emailId);
                return string.Empty;
            }

            var downloadResponse = await blobClient.DownloadContentAsync(cancellationToken);
            return downloadResponse.Value.Content.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get email {CaseId}/{EmailId}", caseId, emailId);
            return string.Empty;
        }
    }

    public async Task SaveEmailAsync(string caseId, string emailId, string content, CancellationToken cancellationToken = default)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_casesContainer);
            var emailPath = $"{caseId}/emails/{emailId}.json";
            var blobClient = containerClient.GetBlobClient(emailPath);

            await blobClient.UploadAsync(
                BinaryData.FromString(content),
                overwrite: true,
                cancellationToken: cancellationToken);

            _logger.LogInformation("Email saved: {CaseId}/{EmailId}", caseId, emailId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save email {CaseId}/{EmailId}", caseId, emailId);
            throw;
        }
    }
}
