using System.Threading;
using System.Threading.Tasks;

namespace DocN.Core.Interfaces;

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
