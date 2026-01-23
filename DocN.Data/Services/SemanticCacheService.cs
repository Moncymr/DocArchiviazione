using System.Text.Json;

namespace DocN.Data.Services;

/// <summary>
/// Service for semantic caching - caches search results for similar queries
/// Uses embedding similarity to find cached results for semantically similar queries
/// </summary>
public interface ISemanticCacheService
{
    /// <summary>
    /// Try to get cached results for a query
    /// </summary>
    /// <param name="queryEmbedding">Query embedding vector</param>
    /// <param name="similarityThreshold">Minimum similarity to consider a cache hit</param>
    /// <returns>Cached results if found, null otherwise</returns>
    Task<CachedSearchResult?> TryGetCachedResultAsync(float[] queryEmbedding, double similarityThreshold = 0.95);

    /// <summary>
    /// Cache search results for a query
    /// </summary>
    /// <param name="query">Original query text</param>
    /// <param name="queryEmbedding">Query embedding vector</param>
    /// <param name="results">Search results to cache</param>
    /// <param name="expirationMinutes">Cache expiration in minutes</param>
    Task CacheResultAsync(string query, float[] queryEmbedding, object results, int expirationMinutes = 60);

    /// <summary>
    /// Invalidate cache entries for updated documents
    /// </summary>
    /// <param name="documentIds">IDs of documents that were updated</param>
    Task InvalidateCacheForDocumentsAsync(IEnumerable<int> documentIds);

    /// <summary>
    /// Clear all cached results
    /// </summary>
    Task ClearCacheAsync();

    /// <summary>
    /// Get cache statistics
    /// </summary>
    Task<CacheStatistics> GetStatisticsAsync();
}

/// <summary>
/// Cached search result
/// </summary>
public class CachedSearchResult
{
    public string OriginalQuery { get; set; } = string.Empty;
    public float[] QueryEmbedding { get; set; } = Array.Empty<float>();
    public string ResultsJson { get; set; } = string.Empty;
    public DateTime CachedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public double SimilarityScore { get; set; }
}

/// <summary>
/// Cache statistics
/// </summary>
public class CacheStatistics
{
    public int TotalEntries { get; set; }
    public int Hits { get; set; }
    public int Misses { get; set; }
    public double HitRate => Misses + Hits > 0 ? (double)Hits / (Hits + Misses) : 0.0;
    public long TotalMemoryBytes { get; set; }
}

/// <summary>
/// Implementation of semantic cache service using in-memory storage
/// In production, this should use Redis or similar distributed cache
/// </summary>
public class SemanticCacheService : ISemanticCacheService
{
    private readonly List<CachedSearchResult> _cache = new();
    private readonly object _cacheLock = new();
    private int _hits = 0;
    private int _misses = 0;
    private const int MaxCacheSize = 1000;

    public SemanticCacheService()
    {
    }

    /// <summary>
    /// Try to get cached results for a query using embedding similarity
    /// </summary>
    public async Task<CachedSearchResult?> TryGetCachedResultAsync(float[] queryEmbedding, double similarityThreshold = 0.95)
    {
        lock (_cacheLock)
        {
            // Remove expired entries
            _cache.RemoveAll(c => c.ExpiresAt < DateTime.UtcNow);

            // Find most similar cached query
            CachedSearchResult? bestMatch = null;
            double bestSimilarity = 0.0;

            foreach (var cached in _cache)
            {
                var similarity = CalculateCosineSimilarity(queryEmbedding, cached.QueryEmbedding);
                
                if (similarity >= similarityThreshold && similarity > bestSimilarity)
                {
                    bestSimilarity = similarity;
                    bestMatch = cached;
                }
            }

            if (bestMatch != null)
            {
                _hits++;
                bestMatch.SimilarityScore = bestSimilarity;
                return bestMatch;
            }
            else
            {
                _misses++;
                return null;
            }
        }
    }

    /// <summary>
    /// Cache search results for a query
    /// </summary>
    public async Task CacheResultAsync(string query, float[] queryEmbedding, object results, int expirationMinutes = 60)
    {
        var resultsJson = JsonSerializer.Serialize(results);
        var now = DateTime.UtcNow;

        var cachedResult = new CachedSearchResult
        {
            OriginalQuery = query,
            QueryEmbedding = queryEmbedding,
            ResultsJson = resultsJson,
            CachedAt = now,
            ExpiresAt = now.AddMinutes(expirationMinutes)
        };

        lock (_cacheLock)
        {
            // Limit cache size (keep only last entries)
            if (_cache.Count >= MaxCacheSize)
            {
                // Remove oldest entry (first in list since we add to end)
                _cache.RemoveAt(0);
            }

            _cache.Add(cachedResult);
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Invalidate cache entries that might contain updated documents
    /// </summary>
    public async Task InvalidateCacheForDocumentsAsync(IEnumerable<int> documentIds)
    {
        // For simplicity, clear all cache when documents are updated
        // In production, would parse ResultsJson and check document IDs
        lock (_cacheLock)
        {
            _cache.Clear();
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Clear all cached results
    /// </summary>
    public async Task ClearCacheAsync()
    {
        lock (_cacheLock)
        {
            _cache.Clear();
            _hits = 0;
            _misses = 0;
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Get cache statistics
    /// </summary>
    public async Task<CacheStatistics> GetStatisticsAsync()
    {
        lock (_cacheLock)
        {
            // Remove expired entries
            _cache.RemoveAll(c => c.ExpiresAt < DateTime.UtcNow);

            var totalMemory = _cache.Sum(c => 
                c.OriginalQuery.Length * 2 + 
                c.QueryEmbedding.Length * 4 + 
                c.ResultsJson.Length * 2
            );

            return new CacheStatistics
            {
                TotalEntries = _cache.Count,
                Hits = _hits,
                Misses = _misses,
                TotalMemoryBytes = totalMemory
            };
        }
    }

    /// <summary>
    /// Calculate cosine similarity between two vectors
    /// </summary>
    private double CalculateCosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            return 0.0;

        double dotProduct = 0.0;
        double magnitudeA = 0.0;
        double magnitudeB = 0.0;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            magnitudeA += a[i] * a[i];
            magnitudeB += b[i] * b[i];
        }

        magnitudeA = Math.Sqrt(magnitudeA);
        magnitudeB = Math.Sqrt(magnitudeB);

        if (magnitudeA == 0 || magnitudeB == 0)
            return 0.0;

        return dotProduct / (magnitudeA * magnitudeB);
    }
}
