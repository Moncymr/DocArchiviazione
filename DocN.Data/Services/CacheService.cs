using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;
using System.Text;

namespace DocN.Data.Services;

/// <summary>
/// Service for caching embeddings and search results
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Get cached embedding for text
    /// </summary>
    Task<float[]?> GetCachedEmbeddingAsync(string text);

    /// <summary>
    /// Cache an embedding
    /// </summary>
    Task SetCachedEmbeddingAsync(string text, float[] embedding, TimeSpan? expiration = null);

    /// <summary>
    /// Get cached search results
    /// </summary>
    Task<List<T>?> GetCachedSearchResultsAsync<T>(string query) where T : class;

    /// <summary>
    /// Cache search results
    /// </summary>
    Task SetCachedSearchResultsAsync<T>(string query, List<T> results, TimeSpan? expiration = null) where T : class;

    /// <summary>
    /// Clear all caches
    /// </summary>
    Task ClearAllAsync();

    /// <summary>
    /// Remove specific cache entry
    /// </summary>
    Task RemoveAsync(string key);
}

public class CacheService : ICacheService
{
    private readonly IMemoryCache _memoryCache;
    private static readonly TimeSpan DefaultEmbeddingExpiration = TimeSpan.FromDays(30);
    private static readonly TimeSpan DefaultSearchExpiration = TimeSpan.FromMinutes(15);

    public CacheService(IMemoryCache memoryCache)
    {
        _memoryCache = memoryCache;
    }

    public Task<float[]?> GetCachedEmbeddingAsync(string text)
    {
        var key = GetEmbeddingCacheKey(text);
        _memoryCache.TryGetValue(key, out float[]? embedding);
        return Task.FromResult(embedding);
    }

    public Task SetCachedEmbeddingAsync(string text, float[] embedding, TimeSpan? expiration = null)
    {
        var key = GetEmbeddingCacheKey(text);
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiration ?? DefaultEmbeddingExpiration,
            Size = embedding.Length * sizeof(float) // Track memory usage
        };

        _memoryCache.Set(key, embedding, options);
        return Task.CompletedTask;
    }

    public Task<List<T>?> GetCachedSearchResultsAsync<T>(string query) where T : class
    {
        var key = GetSearchCacheKey(query, typeof(T).Name);
        _memoryCache.TryGetValue(key, out List<T>? results);
        return Task.FromResult(results);
    }

    public Task SetCachedSearchResultsAsync<T>(string query, List<T> results, TimeSpan? expiration = null) where T : class
    {
        var key = GetSearchCacheKey(query, typeof(T).Name);
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiration ?? DefaultSearchExpiration,
            SlidingExpiration = TimeSpan.FromMinutes(5) // Extend if accessed frequently
        };

        _memoryCache.Set(key, results, options);
        return Task.CompletedTask;
    }

    public Task ClearAllAsync()
    {
        // Note: IMemoryCache doesn't have a built-in Clear method
        // In production, consider using a distributed cache like Redis
        // For now, we can only remove individual keys
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key)
    {
        _memoryCache.Remove(key);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Generate a cache key for an embedding based on text hash
    /// </summary>
    private string GetEmbeddingCacheKey(string text)
    {
        var hash = ComputeHash(text);
        return $"emb:{hash}";
    }

    /// <summary>
    /// Generate a cache key for search results
    /// </summary>
    private string GetSearchCacheKey(string query, string resultType)
    {
        var hash = ComputeHash(query);
        return $"search:{resultType}:{hash}";
    }

    /// <summary>
    /// Compute SHA256 hash of text for cache key
    /// </summary>
    private string ComputeHash(string text)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(text);
        var hashBytes = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hashBytes).Replace("/", "_").Replace("+", "-").TrimEnd('=');
    }
}
