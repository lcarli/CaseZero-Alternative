using System.Text.Json;
using Azure.Storage.Blobs;
using CaseZeroApi.Models.CaseV2;
using Microsoft.Extensions.Caching.Memory;

namespace CaseZeroApi.Services;

public class CaseV2StorageService : ICaseV2StorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ICaseV2SanitizerService _sanitizer;
    private readonly ILogger<CaseV2StorageService> _logger;
    private readonly IMemoryCache _cache;
    private readonly IWebHostEnvironment _env;
    private readonly string _bundlesContainer;
    private readonly bool _useBlobStorage;
    private readonly string? _localCasesPathOverride;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public CaseV2StorageService(
        IConfiguration configuration,
        ICaseV2SanitizerService sanitizer,
        ILogger<CaseV2StorageService> logger,
        IMemoryCache cache,
        IWebHostEnvironment env)
    {
        var connectionString = configuration["CaseGeneratorStorage:ConnectionString"]
            ?? configuration["AzureWebJobsStorage"]
            ?? Environment.GetEnvironmentVariable("AzureWebJobsStorage")
            ?? "UseDevelopmentStorage=true";

        _blobServiceClient = new BlobServiceClient(connectionString);
        _sanitizer = sanitizer;
        _logger = logger;
        _cache = cache;
        _env = env;
        _bundlesContainer = configuration["CaseGeneratorStorage:BundlesContainer"] ?? "bundles";
        _localCasesPathOverride = configuration["CaseGenV2:LocalCasesPath"];

        // Opt-out for local dev: when Azurite isn't running, every blob call takes ~20 s
        // before the SDK gives up. The dev override appsettings.Local.json sets this to
        // false so the dashboard just uses the filesystem. Production keeps it on.
        _useBlobStorage = configuration.GetValue("CaseGenV2:UseBlobStorage", true);
        _logger.LogInformation("CaseV2StorageService _useBlobStorage = {Value}", _useBlobStorage);
    }

    public async Task<List<CaseV2Metadata2>> ListCasesAsync(CancellationToken ct = default)
    {
        var result = new List<CaseV2Metadata2>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1) Local cases directory (preferred for dev)
        var localCasesRoot = ResolveLocalCasesRoot();
        if (localCasesRoot is not null && Directory.Exists(localCasesRoot))
        {
            foreach (var dir in Directory.GetDirectories(localCasesRoot))
            {
                var caseId = Path.GetFileName(dir);
                var caseJson = Path.Combine(dir, "case.json");
                if (!File.Exists(caseJson)) continue;
                try
                {
                    var json = await File.ReadAllTextAsync(caseJson, ct);
                    var c = JsonSerializer.Deserialize<CaseV2>(json, JsonOpts);
                    if (c is null) continue;
                    result.Add(ToMeta(c));
                    seen.Add(c.CaseId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed reading local case {Dir}", dir);
                }
            }
        }

        // 2) Blob storage (skip in local-dev when Azurite isn't running)
        if (_useBlobStorage)
        {
            try
            {
                var container = _blobServiceClient.GetBlobContainerClient(_bundlesContainer);
                await container.CreateIfNotExistsAsync(cancellationToken: ct);
                await foreach (var item in container.GetBlobsByHierarchyAsync(delimiter: "/", cancellationToken: ct))
                {
                    if (!item.IsPrefix) continue;
                    var caseId = item.Prefix.TrimEnd('/');
                    if (seen.Contains(caseId)) continue;
                    try
                    {
                        var c = await GetRawAsync(caseId, ct);
                        if (c is null) continue;
                        result.Add(ToMeta(c));
                        seen.Add(c.CaseId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed reading blob case {CaseId}", caseId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to list blob cases");
            }
        }

        return result.OrderBy(c => c.CaseId).ToList();
    }

    public async Task<CaseV2?> GetRawAsync(string caseId, CancellationToken ct = default)
    {
        var cacheKey = $"case_v2_{caseId}";
        if (_cache.TryGetValue<CaseV2>(cacheKey, out var cached)) return cached;

        // 1) Local filesystem
        var localCasesRoot = ResolveLocalCasesRoot();
        if (localCasesRoot is not null)
        {
            var caseJson = Path.Combine(localCasesRoot, caseId, "case.json");
            if (File.Exists(caseJson))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(caseJson, ct);
                    var c = JsonSerializer.Deserialize<CaseV2>(json, JsonOpts);
                    if (c is not null) CacheCase(cacheKey, c);
                    return c;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed reading local case.json for {CaseId}", caseId);
                }
            }
        }

        // 2) Blob storage (skip when disabled — saves 20 s timeouts in local dev)
        if (!_useBlobStorage) return null;
        try
        {
            var container = _blobServiceClient.GetBlobContainerClient(_bundlesContainer);
            var blob = container.GetBlobClient($"{caseId}/case.json");
            if (!await blob.ExistsAsync(ct)) return null;
            var resp = await blob.DownloadContentAsync(ct);
            var c = JsonSerializer.Deserialize<CaseV2>(resp.Value.Content.ToString(), JsonOpts);
            if (c is not null) CacheCase(cacheKey, c);
            return c;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load case {CaseId}", caseId);
            return null;
        }
    }

    public async Task<CaseV2Sanitized?> GetForUserAsync(string caseId, string userId, CancellationToken ct = default)
    {
        var raw = await GetRawAsync(caseId, ct);
        if (raw is null) return null;
        return await _sanitizer.SanitizeForUserAsync(raw, userId, caseId, ct);
    }

    public async Task<Stream?> GetAssetStreamAsync(string caseId, string assetId, CancellationToken ct = default)
    {
        var caseData = await GetRawAsync(caseId, ct);
        if (caseData is null) return null;
        var asset = caseData.Assets.FirstOrDefault(a => a.Id == assetId);
        if (asset is null) return null;

        var relative = ParseCaseUri(asset.Uri, caseId);
        if (relative is null) return null;

        // Local fs
        var localCasesRoot = ResolveLocalCasesRoot();
        if (localCasesRoot is not null)
        {
            var path = Path.Combine(localCasesRoot, caseId, relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path))
                return File.OpenRead(path);
        }

        try
        {
            var container = _blobServiceClient.GetBlobContainerClient(_bundlesContainer);
            var blob = container.GetBlobClient($"{caseId}/{relative}");
            if (!await blob.ExistsAsync(ct)) return null;
            var resp = await blob.DownloadStreamingAsync(cancellationToken: ct);
            return resp.Value.Content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed loading asset blob {CaseId}/{AssetId}", caseId, assetId);
            return null;
        }
    }

    public async Task<string?> GetAssetContentTypeAsync(string caseId, string assetId, CancellationToken ct = default)
    {
        var caseData = await GetRawAsync(caseId, ct);
        if (caseData is null) return null;
        var asset = caseData.Assets.FirstOrDefault(a => a.Id == assetId);
        if (asset is null) return null;

        var relative = ParseCaseUri(asset.Uri, caseId);
        if (relative is null) return "application/octet-stream";

        var localCasesRoot = ResolveLocalCasesRoot();
        if (localCasesRoot is not null)
        {
            var path = Path.Combine(localCasesRoot, caseId, relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path))
                return GuessContentType(path);
        }

        try
        {
            var container = _blobServiceClient.GetBlobContainerClient(_bundlesContainer);
            var blob = container.GetBlobClient($"{caseId}/{relative}");
            if (!await blob.ExistsAsync(ct)) return "application/octet-stream";
            var props = await blob.GetPropertiesAsync(conditions: null, cancellationToken: ct);
            return props.Value.ContentType ?? "application/octet-stream";
        }
        catch
        {
            return "application/octet-stream";
        }
    }

    public async Task<bool> CaseExistsAsync(string caseId, CancellationToken ct = default)
    {
        var c = await GetRawAsync(caseId, ct);
        return c is not null;
    }

    // Helpers

    private void CacheCase(string key, CaseV2 c)
    {
        _cache.Set(key, c, new MemoryCacheEntryOptions()
            .SetSlidingExpiration(TimeSpan.FromMinutes(30))
            .SetAbsoluteExpiration(TimeSpan.FromHours(2))
            .SetSize(1));
    }

    private string? ResolveLocalCasesRoot()
    {
        // 1) Explicit override (used by tests + custom dev layouts)
        if (!string.IsNullOrWhiteSpace(_localCasesPathOverride))
        {
            var overridePath = Path.GetFullPath(_localCasesPathOverride);
            if (Directory.Exists(overridePath)) return overridePath;
        }

        // 2) Walk up from content root looking for "cases" dir
        var candidates = new List<string>
        {
            Path.Combine(_env.ContentRootPath, "cases"),
            Path.Combine(Directory.GetCurrentDirectory(), "cases"),
            Path.Combine(_env.ContentRootPath, "..", "cases"),
            Path.Combine(_env.ContentRootPath, "..", "..", "cases"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "cases"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "cases"),
        };
        foreach (var c in candidates)
        {
            var full = Path.GetFullPath(c);
            if (Directory.Exists(full)) return full;
        }
        return null;
    }

    internal static string? ParseCaseUri(string uri, string caseId)
    {
        if (string.IsNullOrWhiteSpace(uri)) return null;
        const string prefix = "case://";
        if (uri.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            var rest = uri.Substring(prefix.Length);
            var slash = rest.IndexOf('/');
            if (slash < 0) return null;
            return rest.Substring(slash + 1);
        }
        return uri.TrimStart('/');
    }

    private static string GuessContentType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".txt" => "text/plain",
        ".json" => "application/json",
        ".html" or ".htm" => "text/html",
        ".mp3" => "audio/mpeg",
        ".wav" => "audio/wav",
        ".mp4" => "video/mp4",
        _ => "application/octet-stream"
    };

    private static CaseV2Metadata2 ToMeta(CaseV2 c) => new()
    {
        CaseId = c.CaseId,
        Title = c.Metadata.Title,
        Description = c.Metadata.Description,
        Difficulty = c.Metadata.Difficulty,
        Category = c.Metadata.Category,
        EstimatedDurationMinutes = c.Metadata.EstimatedDurationMinutes,
        Briefing = c.Metadata.Briefing,
        Tags = c.Metadata.Tags,
        RequiredRank = c.Metadata.RequiredRank
    };
}
