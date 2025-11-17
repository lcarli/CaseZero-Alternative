using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CaseGen.Functions.Models;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Services;

public class RedTeamCacheService : IRedTeamCacheService
{
    private readonly ILogger<RedTeamCacheService> _logger;
    private readonly Dictionary<string, RedTeamCacheEntry> _cache;
    private readonly SemaphoreSlim _cacheLock;

    public RedTeamCacheService(ILogger<RedTeamCacheService> logger)
    {
        _logger = logger;
        _cache = new Dictionary<string, RedTeamCacheEntry>();
        _cacheLock = new SemaphoreSlim(1, 1);
    }

    public string ComputeContentHash(string content)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public async Task<string?> GetCachedAnalysisAsync(string contentHash, string analysisType, string[]? focusAreas = null, CancellationToken cancellationToken = default)
    {
        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            var cacheKey = BuildCacheKey(contentHash, analysisType, focusAreas);
            
            if (_cache.TryGetValue(cacheKey, out var cacheEntry))
            {
                _logger.LogInformation("RedTeam cache HIT for {AnalysisType} analysis with hash {Hash}", 
                    analysisType, contentHash[..8]);
                
                return cacheEntry.Analysis;
            }

            _logger.LogDebug("RedTeam cache MISS for {AnalysisType} analysis with hash {Hash}", 
                analysisType, contentHash[..8]);
            
            return null;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    public async Task CacheAnalysisAsync(string contentHash, string analysis, string analysisType, string[]? focusAreas = null, CancellationToken cancellationToken = default)
    {
        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            var cacheKey = BuildCacheKey(contentHash, analysisType, focusAreas);
            var cacheEntry = new RedTeamCacheEntry
            {
                ContentHash = contentHash,
                Analysis = analysis,
                CreatedAt = DateTime.UtcNow,
                AnalysisType = analysisType,
                FocusAreas = focusAreas
            };

            _cache[cacheKey] = cacheEntry;

            _logger.LogInformation("Cached {AnalysisType} RedTeam analysis for hash {Hash} (cache size: {Size})", 
                analysisType, contentHash[..8], _cache.Count);
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    public async Task ClearExpiredEntriesAsync(TimeSpan maxAge, CancellationToken cancellationToken = default)
    {
        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            var cutoffTime = DateTime.UtcNow - maxAge;
            var expiredKeys = _cache
                .Where(kvp => kvp.Value.CreatedAt < cutoffTime)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                _cache.Remove(key);
            }

            if (expiredKeys.Count > 0)
            {
                _logger.LogInformation("Cleared {Count} expired RedTeam cache entries (cache size: {Size})", 
                    expiredKeys.Count, _cache.Count);
            }
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    private static string BuildCacheKey(string contentHash, string analysisType, string[]? focusAreas)
    {
        var key = $"{contentHash}:{analysisType}";
        
        if (focusAreas?.Length > 0)
        {
            var focusAreasHash = string.Join(",", focusAreas.OrderBy(x => x));
            key += $":{focusAreasHash}";
        }
        
        return key;
    }
}