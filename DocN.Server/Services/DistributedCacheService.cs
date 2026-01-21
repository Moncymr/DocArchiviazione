using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using System.Collections.Concurrent;

namespace DocN.Server.Services;

/// <summary>
/// Distributed cache service that works with both Redis and in-memory cache
/// Provides intelligent caching for embeddings, search results, and session data
/// </summary>
public interface IDistributedCacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);
}

public class DistributedCacheService : IDistributedCacheService
{
    private readonly IDistributedCache? _distributedCache;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<DistributedCacheService> _logger;
    private readonly bool _isRedisConfigured;
    private readonly ConcurrentDictionary<string, byte> _cacheKeys = new(); // Thread-safe key tracking

    public DistributedCacheService(
        IServiceProvider serviceProvider,
        IMemoryCache memoryCache,
        ILogger<DistributedCacheService> logger)
    {
        _memoryCache = memoryCache;
        _logger = logger;
        
        // Try to get distributed cache (Redis) if configured
        try
        {
            _distributedCache = serviceProvider.GetService<IDistributedCache>();
            _isRedisConfigured = _distributedCache != null;
            
            if (_isRedisConfigured)
            {
                _logger.LogInformation("Using Redis distributed cache");
            }
            else
            {
                _logger.LogInformation("Redis not configured, using in-memory cache");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize distributed cache, falling back to memory cache");
            _isRedisConfigured = false;
        }
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_isRedisConfigured && _distributedCache != null)
            {
                var bytes = await _distributedCache.GetAsync(key, cancellationToken);
                if (bytes == null)
                {
                    return default;
                }

                var json = System.Text.Encoding.UTF8.GetString(bytes);
                return JsonSerializer.Deserialize<T>(json);
            }
            else
            {
                return _memoryCache.Get<T>(key);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cache key {Key}", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var expirationTime = expiration ?? TimeSpan.FromHours(1); // Default 1 hour

            if (_isRedisConfigured && _distributedCache != null)
            {
                var json = JsonSerializer.Serialize(value);
                var bytes = System.Text.Encoding.UTF8.GetBytes(json);
                
                var options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = expirationTime
                };

                await _distributedCache.SetAsync(key, bytes, options, cancellationToken);
                _cacheKeys.TryAdd(key, 0); // Thread-safe add
            }
            else
            {
                var options = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = expirationTime
                };

                _memoryCache.Set(key, value, options);
                _cacheKeys.TryAdd(key, 0); // Thread-safe add
            }

            _logger.LogDebug("Cached key {Key} with expiration {Expiration}", key, expirationTime);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting cache key {Key}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_isRedisConfigured && _distributedCache != null)
            {
                await _distributedCache.RemoveAsync(key, cancellationToken);
            }
            else
            {
                _memoryCache.Remove(key);
            }

            _cacheKeys.TryRemove(key, out _); // Thread-safe remove

            _logger.LogDebug("Removed cache key {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cache key {Key}", key);
        }
    }

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        try
        {
            // Get all keys with prefix atomically
            var keysToRemove = _cacheKeys.Keys
                .Where(k => k.StartsWith(prefix, StringComparison.Ordinal))
                .ToList();

            // Remove each key
            var removeTasks = keysToRemove.Select(key => RemoveAsync(key, cancellationToken));
            await Task.WhenAll(removeTasks);

            _logger.LogInformation("Removed {Count} cache keys with prefix {Prefix}", keysToRemove.Count, prefix);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cache keys by prefix {Prefix}", prefix);
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_isRedisConfigured && _distributedCache != null)
            {
                var bytes = await _distributedCache.GetAsync(key, cancellationToken);
                return bytes != null;
            }
            else
            {
                return _memoryCache.TryGetValue(key, out _);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking cache key existence {Key}", key);
            return false;
        }
    }
}

/// <summary>
/// Extension methods for cache key generation
/// </summary>
public static class CacheKeyExtensions
{
    public const string EmbeddingPrefix = "embedding:";
    public const string SearchPrefix = "search:";
    public const string DocumentPrefix = "document:";
    public const string SessionPrefix = "session:";

    public static string ToEmbeddingCacheKey(this string text)
    {
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)));
        return $"{EmbeddingPrefix}{hash}";
    }

    public static string ToSearchCacheKey(this string query, string? filters = null)
    {
        var combined = filters != null ? $"{query}|{filters}" : query;
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(combined)));
        return $"{SearchPrefix}{hash}";
    }

    public static string ToDocumentCacheKey(this int documentId)
    {
        return $"{DocumentPrefix}{documentId}";
    }

    public static string ToSessionCacheKey(this string sessionId)
    {
        return $"{SessionPrefix}{sessionId}";
    }
}
