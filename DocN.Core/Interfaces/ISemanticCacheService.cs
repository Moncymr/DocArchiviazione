namespace DocN.Core.Interfaces;

/// <summary>
/// Service for semantic caching using embedding similarity
/// Allows retrieving cached results for similar queries, not just exact matches
/// </summary>
public interface ISemanticCacheService
{
    /// <summary>
    /// Try to get cached results for a query or similar queries
    /// </summary>
    /// <param name="query">Search query</param>
    /// <param name="queryEmbedding">Embedding of the query</param>
    /// <param name="similarityThreshold">Minimum similarity to consider a cache hit (default: 0.95)</param>
    /// <returns>Cached results if found, null otherwise</returns>
    Task<T?> GetCachedResultsAsync<T>(string query, float[] queryEmbedding, double similarityThreshold = 0.95) where T : class;
    
    /// <summary>
    /// Cache search results with their query embedding
    /// </summary>
    /// <param name="query">Original query</param>
    /// <param name="queryEmbedding">Embedding of the query</param>
    /// <param name="results">Results to cache</param>
    /// <param name="expiration">Cache expiration time</param>
    Task SetCachedResultsAsync<T>(string query, float[] queryEmbedding, T results, TimeSpan? expiration = null) where T : class;
    
    /// <summary>
    /// Clear all cached entries
    /// </summary>
    Task ClearCacheAsync();
    
    /// <summary>
    /// Get cache statistics
    /// </summary>
    Task<SemanticCacheStats> GetStatsAsync();
}

/// <summary>
/// Statistics about semantic cache performance
/// </summary>
public class SemanticCacheStats
{
    public int TotalEntries { get; set; }
    public int TotalHits { get; set; }
    public int TotalMisses { get; set; }
    public double HitRate => TotalHits + TotalMisses > 0 ? (double)TotalHits / (TotalHits + TotalMisses) : 0;
    public int SemanticHits { get; set; } // Hits from similar (not exact) queries
}
