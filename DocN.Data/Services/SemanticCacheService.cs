using DocN.Core.Interfaces;
using DocN.Data.Utilities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace DocN.Data.Services;

/// <summary>
/// Semantic cache implementation using embedding similarity
/// Allows cache hits for similar queries, not just exact matches
/// </summary>
public class SemanticCacheService : ISemanticCacheService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<SemanticCacheService> _logger;
    private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(30);
    
    // Store query embeddings for similarity comparison
    private readonly ConcurrentDictionary<string, CachedQueryEntry> _queryEmbeddings = new();
    
    // Statistics
    private int _totalHits = 0;
    private int _totalMisses = 0;
    private int _semanticHits = 0;
    
    private class CachedQueryEntry
    {
        public string Query { get; set; } = string.Empty;
        public float[] Embedding { get; set; } = Array.Empty<float>();
        public string CacheKey { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
    
    public SemanticCacheService(IMemoryCache cache, ILogger<SemanticCacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }
    
    /// <inheritdoc/>
    public async Task<T?> GetCachedResultsAsync<T>(string query, float[] queryEmbedding, double similarityThreshold = 0.95) where T : class
    {
        // First try exact match
        var exactKey = GetCacheKey(query, typeof(T).Name);
        if (_cache.TryGetValue<T>(exactKey, out var exactResult))
        {
            Interlocked.Increment(ref _totalHits);
            _logger.LogDebug("Exact cache hit for query: {Query}", TruncateQuery(query));
            return exactResult;
        }
        
        // Try semantic similarity match
        var now = DateTime.UtcNow;
        var validEntries = _queryEmbeddings.Values
            .Where(e => e.ExpiresAt > now)
            .ToList();
        
        foreach (var entry in validEntries)
        {
            var similarity = VectorMathHelper.CosineSimilarity(queryEmbedding, entry.Embedding);
            
            if (similarity >= similarityThreshold)
            {
                // Try to get cached results using the similar query's cache key
                if (_cache.TryGetValue<T>(entry.CacheKey, out var cachedResult))
                {
                    Interlocked.Increment(ref _totalHits);
                    Interlocked.Increment(ref _semanticHits);
                    _logger.LogInformation(
                        "Semantic cache hit: '{Query}' matched '{CachedQuery}' (similarity: {Similarity:F3})",
                        TruncateQuery(query), TruncateQuery(entry.Query), similarity);
                    return cachedResult;
                }
            }
        }
        
        Interlocked.Increment(ref _totalMisses);
        _logger.LogDebug("Cache miss for query: {Query}", TruncateQuery(query));
        return null;
    }
    
    /// <inheritdoc/>
    public async Task SetCachedResultsAsync<T>(string query, float[] queryEmbedding, T results, TimeSpan? expiration = null) where T : class
    {
        var cacheKey = GetCacheKey(query, typeof(T).Name);
        var expirationTime = expiration ?? DefaultExpiration;
        
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expirationTime,
            SlidingExpiration = TimeSpan.FromMinutes(10)
        };
        
        // Register callback to clean up query embedding when cache entry expires
        options.RegisterPostEvictionCallback((key, value, reason, state) =>
        {
            _queryEmbeddings.TryRemove(cacheKey, out _);
        });
        
        _cache.Set(cacheKey, results, options);
        
        // Store query embedding for similarity search
        _queryEmbeddings[cacheKey] = new CachedQueryEntry
        {
            Query = query,
            Embedding = queryEmbedding,
            CacheKey = cacheKey,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(expirationTime)
        };
        
        _logger.LogDebug("Cached results for query: {Query}", TruncateQuery(query));
        
        await Task.CompletedTask;
    }
    
    /// <inheritdoc/>
    public Task ClearCacheAsync()
    {
        _queryEmbeddings.Clear();
        _totalHits = 0;
        _totalMisses = 0;
        _semanticHits = 0;
        
        _logger.LogInformation("Semantic cache cleared");
        return Task.CompletedTask;
    }
    
    /// <inheritdoc/>
    public Task<SemanticCacheStats> GetStatsAsync()
    {
        var stats = new SemanticCacheStats
        {
            TotalEntries = _queryEmbeddings.Count,
            TotalHits = _totalHits,
            TotalMisses = _totalMisses,
            SemanticHits = _semanticHits
        };
        
        return Task.FromResult(stats);
    }
    
    private string GetCacheKey(string query, string typeName)
    {
        return $"semantic_cache:{typeName}:{query.ToLowerInvariant()}";
    }
    
    private string TruncateQuery(string query)
    {
        return query.Length > 50 ? query.Substring(0, 47) + "..." : query;
    }
}
