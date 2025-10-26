using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using CaseGen.Functions.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace CaseGen.Functions.Services;

/// <summary>
/// Azure Blob Storage implementation of IContextManager.
/// Provides granular context storage with in-memory caching.
/// </summary>
public class ContextManager : IContextManager
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<ContextManager> _logger;
    private readonly string _containerName;
    
    // In-memory cache: caseId -> (path -> (data, expiry))
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, CacheEntry>> _cache;
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(30);
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public ContextManager(
        BlobServiceClient blobServiceClient,
        ILogger<ContextManager> logger,
        string containerName = "case-context")
    {
        _blobServiceClient = blobServiceClient;
        _logger = logger;
        _containerName = containerName;
        _cache = new ConcurrentDictionary<string, ConcurrentDictionary<string, CacheEntry>>();
    }

    /// <inheritdoc />
    public async Task<string> SaveContextAsync<T>(
        string caseId,
        string contextPath,
        T data,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caseId);
        ArgumentNullException.ThrowIfNull(contextPath);
        ArgumentNullException.ThrowIfNull(data);

        try
        {
            var normalizedPath = NormalizePath(contextPath);
            var blobPath = GetBlobPath(caseId, normalizedPath);
            
            _logger.LogDebug("Saving context to {BlobPath}", blobPath);

            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

            var blobClient = containerClient.GetBlobClient(blobPath);
            
            // Serialize data
            var json = JsonSerializer.Serialize(data, JsonOptions);
            var bytes = Encoding.UTF8.GetBytes(json);

            // Upload to blob
            using var stream = new MemoryStream(bytes);
            await blobClient.UploadAsync(stream, overwrite: true, cancellationToken: cancellationToken);

            // Set metadata
            var metadata = new Dictionary<string, string>
            {
                { "type", typeof(T).Name },
                { "size", bytes.Length.ToString() },
                { "created", DateTime.UtcNow.ToString("o") }
            };
            await blobClient.SetMetadataAsync(metadata, cancellationToken: cancellationToken);

            // Update cache
            UpdateCache(caseId, normalizedPath, data, bytes.Length);

            _logger.LogInformation("Saved context {Path} for case {CaseId} ({Size} bytes)", 
                normalizedPath, caseId, bytes.Length);

            return blobPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save context {Path} for case {CaseId}", contextPath, caseId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<T?> LoadContextAsync<T>(
        string caseId,
        string contextPath,
        CancellationToken cancellationToken = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(caseId);
        ArgumentNullException.ThrowIfNull(contextPath);

        try
        {
            var normalizedPath = NormalizePath(contextPath);

            // Check cache first
            if (TryGetFromCache<T>(caseId, normalizedPath, out var cachedData))
            {
                _logger.LogDebug("Cache hit for {Path} in case {CaseId}", normalizedPath, caseId);
                return cachedData;
            }

            var blobPath = GetBlobPath(caseId, normalizedPath);
            _logger.LogDebug("Loading context from {BlobPath}", blobPath);

            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(blobPath);

            if (!await blobClient.ExistsAsync(cancellationToken))
            {
                _logger.LogWarning("Context not found: {BlobPath}", blobPath);
                return null;
            }

            // Download and deserialize
            var response = await blobClient.DownloadContentAsync(cancellationToken);
            var json = response.Value.Content.ToString();
            var data = JsonSerializer.Deserialize<T>(json, JsonOptions);

            if (data != null)
            {
                // Update cache
                UpdateCache(caseId, normalizedPath, data, json.Length);
                _logger.LogDebug("Loaded and cached context {Path} for case {CaseId}", normalizedPath, caseId);
            }

            return data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load context {Path} for case {CaseId}", contextPath, caseId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ContextQueryResult<T>>> QueryContextAsync<T>(
        string caseId,
        string queryPattern,
        CancellationToken cancellationToken = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(caseId);
        ArgumentNullException.ThrowIfNull(queryPattern);

        try
        {
            var normalizedPattern = NormalizePath(queryPattern);
            var prefix = GetBlobPrefix(caseId, normalizedPattern);
            
            _logger.LogDebug("Querying context with pattern {Pattern} for case {CaseId}", normalizedPattern, caseId);

            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            
            if (!await containerClient.ExistsAsync(cancellationToken))
            {
                return Enumerable.Empty<ContextQueryResult<T>>();
            }

            var results = new List<ContextQueryResult<T>>();

            await foreach (var blobItem in containerClient.GetBlobsAsync(prefix: prefix, cancellationToken: cancellationToken))
            {
                // Check if blob matches the pattern (handle wildcards)
                if (!MatchesPattern(blobItem.Name, prefix, normalizedPattern))
                {
                    continue;
                }

                try
                {
                    var blobClient = containerClient.GetBlobClient(blobItem.Name);
                    var response = await blobClient.DownloadContentAsync(cancellationToken);
                    var json = response.Value.Content.ToString();
                    var data = JsonSerializer.Deserialize<T>(json, JsonOptions);

                    if (data != null)
                    {
                        var relativePath = GetRelativePath(blobItem.Name, caseId);
                        results.Add(new ContextQueryResult<T>
                        {
                            Path = relativePath,
                            Data = data,
                            SizeBytes = blobItem.Properties.ContentLength ?? 0,
                            LastModified = blobItem.Properties.LastModified?.UtcDateTime ?? DateTime.UtcNow
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize blob {BlobName}", blobItem.Name);
                }
            }

            _logger.LogInformation("Query returned {Count} results for pattern {Pattern}", results.Count, normalizedPattern);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to query context with pattern {Pattern} for case {CaseId}", queryPattern, caseId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ContextSnapshot> BuildSnapshotAsync(
        string caseId,
        string[] contextPaths,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caseId);
        ArgumentNullException.ThrowIfNull(contextPaths);

        var startTime = DateTime.UtcNow;
        var snapshot = new ContextSnapshot
        {
            CaseId = caseId,
            Items = new Dictionary<string, object>()
        };

        var loadedPaths = new List<string>();
        var failedPaths = new List<string>();
        long totalSize = 0;

        _logger.LogDebug("Building snapshot with {Count} paths for case {CaseId}", contextPaths.Length, caseId);

        foreach (var path in contextPaths)
        {
            try
            {
                var normalizedPath = NormalizePath(path);
                var data = await LoadContextAsync<object>(caseId, normalizedPath, cancellationToken);

                if (data != null)
                {
                    snapshot.Items[normalizedPath] = data;
                    loadedPaths.Add(normalizedPath);
                    
                    // Estimate size
                    var json = JsonSerializer.Serialize(data, JsonOptions);
                    totalSize += json.Length;
                }
                else
                {
                    failedPaths.Add(normalizedPath);
                    _logger.LogWarning("Context not found for path {Path}", normalizedPath);
                }
            }
            catch (Exception ex)
            {
                failedPaths.Add(path);
                _logger.LogWarning(ex, "Failed to load context for path {Path}", path);
            }
        }

        snapshot.TotalSizeBytes = totalSize;
        snapshot.Metadata = new SnapshotMetadata
        {
            ItemCount = snapshot.Items.Count,
            RequestedPaths = contextPaths,
            LoadedPaths = loadedPaths.ToArray(),
            FailedPaths = failedPaths.ToArray(),
            BuildDuration = DateTime.UtcNow - startTime
        };

        _logger.LogInformation(
            "Built snapshot for case {CaseId}: {LoadedCount}/{TotalCount} items, {Size} bytes, ~{Tokens} tokens",
            caseId, loadedPaths.Count, contextPaths.Length, totalSize, snapshot.EstimatedTokens);

        return snapshot;
    }

    /// <inheritdoc />
    public async Task<int> DeleteContextAsync(
        string caseId,
        string contextPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caseId);
        ArgumentNullException.ThrowIfNull(contextPath);

        try
        {
            var normalizedPath = NormalizePath(contextPath);
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);

            if (!await containerClient.ExistsAsync(cancellationToken))
            {
                return 0;
            }

            int deletedCount = 0;

            // Check if it's a wildcard pattern
            if (normalizedPath.Contains('*'))
            {
                var prefix = GetBlobPrefix(caseId, normalizedPath);
                await foreach (var blobItem in containerClient.GetBlobsAsync(prefix: prefix, cancellationToken: cancellationToken))
                {
                    if (MatchesPattern(blobItem.Name, prefix, normalizedPath))
                    {
                        await containerClient.DeleteBlobIfExistsAsync(blobItem.Name, cancellationToken: cancellationToken);
                        deletedCount++;
                    }
                }
            }
            else
            {
                var blobPath = GetBlobPath(caseId, normalizedPath);
                var deleted = await containerClient.DeleteBlobIfExistsAsync(blobPath, cancellationToken: cancellationToken);
                deletedCount = deleted ? 1 : 0;
            }

            // Clear from cache
            ClearCacheForPath(caseId, normalizedPath);

            _logger.LogInformation("Deleted {Count} context items matching {Path} for case {CaseId}", 
                deletedCount, normalizedPath, caseId);

            return deletedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete context {Path} for case {CaseId}", contextPath, caseId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(
        string caseId,
        string contextPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caseId);
        ArgumentNullException.ThrowIfNull(contextPath);

        try
        {
            var normalizedPath = NormalizePath(contextPath);
            
            // Check cache first
            if (HasInCache(caseId, normalizedPath))
            {
                return true;
            }

            var blobPath = GetBlobPath(caseId, normalizedPath);
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(blobPath);

            return await blobClient.ExistsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check existence of context {Path} for case {CaseId}", contextPath, caseId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<string>> ListContextPathsAsync(
        string caseId,
        string? prefix = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caseId);

        try
        {
            var normalizedPrefix = prefix != null ? NormalizePath(prefix) : string.Empty;
            var blobPrefix = GetBlobPrefix(caseId, normalizedPrefix);
            
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            
            if (!await containerClient.ExistsAsync(cancellationToken))
            {
                return Enumerable.Empty<string>();
            }

            var paths = new List<string>();

            await foreach (var blobItem in containerClient.GetBlobsAsync(prefix: blobPrefix, cancellationToken: cancellationToken))
            {
                var relativePath = GetRelativePath(blobItem.Name, caseId);
                paths.Add(relativePath);
            }

            _logger.LogDebug("Listed {Count} context paths for case {CaseId} with prefix {Prefix}", 
                paths.Count, caseId, normalizedPrefix);

            return paths;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list context paths for case {CaseId}", caseId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ContextMetadata> GetMetadataAsync(
        string caseId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caseId);

        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var prefix = $"{caseId}/context/";

            var metadata = new ContextMetadata
            {
                CaseId = caseId,
                ItemsByType = new Dictionary<string, int>()
            };

            if (!await containerClient.ExistsAsync(cancellationToken))
            {
                return metadata;
            }

            int totalItems = 0;
            long totalSize = 0;
            DateTime? firstCreated = null;
            DateTime? lastModified = null;

            await foreach (var blobItem in containerClient.GetBlobsAsync(prefix: prefix, cancellationToken: cancellationToken))
            {
                totalItems++;
                totalSize += blobItem.Properties.ContentLength ?? 0;

                var created = blobItem.Properties.CreatedOn?.UtcDateTime;
                var modified = blobItem.Properties.LastModified?.UtcDateTime;

                if (created.HasValue && (!firstCreated.HasValue || created < firstCreated))
                {
                    firstCreated = created;
                }

                if (modified.HasValue && (!lastModified.HasValue || modified > lastModified))
                {
                    lastModified = modified;
                }

                // Categorize by type
                var relativePath = GetRelativePath(blobItem.Name, caseId);
                var category = relativePath.Split('/')[0];
                metadata.ItemsByType.TryGetValue(category, out var count);
                metadata.ItemsByType[category] = count + 1;
            }

            metadata.TotalItems = totalItems;
            metadata.TotalSizeBytes = totalSize;
            metadata.CreatedAt = firstCreated ?? DateTime.UtcNow;
            metadata.LastModifiedAt = lastModified ?? DateTime.UtcNow;

            _logger.LogInformation("Retrieved metadata for case {CaseId}: {Items} items, {Size} bytes", 
                caseId, totalItems, totalSize);

            return metadata;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get metadata for case {CaseId}", caseId);
            throw;
        }
    }

    /// <inheritdoc />
    public void ClearCache(string? caseId = null)
    {
        if (caseId == null)
        {
            _cache.Clear();
            _logger.LogInformation("Cleared entire context cache");
        }
        else
        {
            _cache.TryRemove(caseId, out _);
            _logger.LogInformation("Cleared cache for case {CaseId}", caseId);
        }
    }

    #region Private Helpers

    private string GetBlobPath(string caseId, string contextPath)
    {
        return $"{caseId}/context/{contextPath.TrimStart('/')}.json";
    }

    private string GetBlobPrefix(string caseId, string contextPath)
    {
        var normalized = contextPath.TrimStart('/').TrimEnd('*', '/');
        return $"{caseId}/context/{normalized}";
    }

    private string GetRelativePath(string blobPath, string caseId)
    {
        var prefix = $"{caseId}/context/";
        if (blobPath.StartsWith(prefix))
        {
            return blobPath[prefix.Length..].TrimEnd(".json".ToCharArray());
        }
        return blobPath;
    }

    private string NormalizePath(string path)
    {
        return path.TrimStart('@', '/').TrimEnd('/');
    }

    private bool MatchesPattern(string blobName, string prefix, string pattern)
    {
        // Simple wildcard matching for now
        if (!pattern.Contains('*'))
        {
            return blobName.StartsWith(prefix);
        }

        // More sophisticated pattern matching could be implemented here
        return blobName.StartsWith(prefix);
    }

    private void UpdateCache<T>(string caseId, string path, T data, long size)
    {
        var caseCache = _cache.GetOrAdd(caseId, _ => new ConcurrentDictionary<string, CacheEntry>());
        caseCache[path] = new CacheEntry
        {
            Data = data!,
            Size = size,
            Expiry = DateTime.UtcNow.Add(_cacheExpiration)
        };
    }

    private bool TryGetFromCache<T>(string caseId, string path, out T? data) where T : class
    {
        data = null;

        if (!_cache.TryGetValue(caseId, out var caseCache))
        {
            return false;
        }

        if (!caseCache.TryGetValue(path, out var entry))
        {
            return false;
        }

        if (entry.Expiry < DateTime.UtcNow)
        {
            caseCache.TryRemove(path, out _);
            return false;
        }

        data = entry.Data as T;
        return data != null;
    }

    private bool HasInCache(string caseId, string path)
    {
        if (!_cache.TryGetValue(caseId, out var caseCache))
        {
            return false;
        }

        if (!caseCache.TryGetValue(path, out var entry))
        {
            return false;
        }

        if (entry.Expiry < DateTime.UtcNow)
        {
            caseCache.TryRemove(path, out _);
            return false;
        }

        return true;
    }

    private void ClearCacheForPath(string caseId, string path)
    {
        if (_cache.TryGetValue(caseId, out var caseCache))
        {
            // Remove exact match
            caseCache.TryRemove(path, out _);

            // If wildcard, remove all matching paths
            if (path.Contains('*'))
            {
                var prefix = path.TrimEnd('*', '/');
                var keysToRemove = caseCache.Keys.Where(k => k.StartsWith(prefix)).ToList();
                foreach (var key in keysToRemove)
                {
                    caseCache.TryRemove(key, out _);
                }
            }
        }
    }

    private class CacheEntry
    {
        public required object Data { get; set; }
        public long Size { get; set; }
        public DateTime Expiry { get; set; }
    }

    #endregion
}
