using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DocN.Core.Interfaces;
using DocN.Core.Configuration;
using DocN.Server.Services;
using System.Collections.Concurrent;

namespace DocN.Data.Services;

/// <summary>
/// Distributed Vector Store Service that coordinates multiple vector store instances
/// Uses SQL Server 2025 as primary storage with Redis for distributed coordination
/// </summary>
public class DistributedVectorStoreService : IVectorStoreService
{
    private readonly IVectorStoreService _primaryVectorStore;
    private readonly IDistributedCacheService _distributedCache;
    private readonly ILogger<DistributedVectorStoreService> _logger;
    private readonly VectorStoreConfiguration _config;
    private readonly ConcurrentDictionary<string, byte> _syncQueue = new();

    public DistributedVectorStoreService(
        IVectorStoreService primaryVectorStore,
        IDistributedCacheService distributedCache,
        ILogger<DistributedVectorStoreService> logger,
        IOptions<VectorStoreConfiguration> config)
    {
        _primaryVectorStore = primaryVectorStore ?? throw new ArgumentNullException(nameof(primaryVectorStore));
        _distributedCache = distributedCache ?? throw new ArgumentNullException(nameof(distributedCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));

        if (_config.Distributed.Enabled)
        {
            _logger.LogInformation(
                "Distributed Vector Store enabled - Replication Factor: {ReplicationFactor}, Sync Interval: {SyncInterval}s",
                _config.Distributed.ReplicationFactor,
                _config.Distributed.SyncIntervalSeconds);
        }
    }

    /// <inheritdoc/>
    public async Task<bool> StoreVectorAsync(string id, float[] vector, Dictionary<string, object>? metadata = null)
    {
        try
        {
            // Store in primary vector store (SQL Server 2025)
            var result = await _primaryVectorStore.StoreVectorAsync(id, vector, metadata);

            if (result && _config.Distributed.Enabled)
            {
                // Cache vector in distributed cache (Redis) for fast access across instances
                var cacheKey = $"vector:{id}";
                var vectorData = new CachedVectorData
                {
                    Id = id,
                    Vector = vector,
                    Metadata = metadata,
                    Timestamp = DateTime.UtcNow
                };

                await _distributedCache.SetAsync(
                    cacheKey,
                    vectorData,
                    TimeSpan.FromHours(24)); // Cache for 24 hours

                // Add to sync queue for replication
                _syncQueue.TryAdd(id, 0);

                _logger.LogDebug("Vector {Id} stored and cached in distributed system", id);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing vector {Id} in distributed system", id);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<List<VectorSearchResult>> SearchSimilarVectorsAsync(
        float[] queryVector,
        int topK = 10,
        Dictionary<string, object>? metadataFilter = null,
        double minSimilarity = 0.7)
    {
        try
        {
            // Try to get from distributed cache first
            var cacheKey = GenerateSearchCacheKey(queryVector, topK, metadataFilter, minSimilarity);
            var cachedResults = await _distributedCache.GetAsync<List<VectorSearchResult>>(cacheKey);

            if (cachedResults != null && cachedResults.Any())
            {
                _logger.LogDebug("Returning {Count} cached search results", cachedResults.Count);
                return cachedResults;
            }

            // If not in cache, search in primary vector store
            var results = await _primaryVectorStore.SearchSimilarVectorsAsync(
                queryVector, topK, metadataFilter, minSimilarity);

            // Cache results in distributed cache
            if (results.Any() && _config.Distributed.Enabled)
            {
                await _distributedCache.SetAsync(
                    cacheKey,
                    results,
                    TimeSpan.FromMinutes(15)); // Cache search results for 15 minutes
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching similar vectors in distributed system");
            return new List<VectorSearchResult>();
        }
    }

    /// <inheritdoc/>
    public async Task<List<VectorSearchResult>> SearchWithMMRAsync(
        float[] queryVector,
        int topK = 10,
        double lambda = 0.5,
        Dictionary<string, object>? metadataFilter = null)
    {
        try
        {
            // Try to get from distributed cache first
            var cacheKey = GenerateMMRCacheKey(queryVector, topK, lambda, metadataFilter);
            var cachedResults = await _distributedCache.GetAsync<List<VectorSearchResult>>(cacheKey);

            if (cachedResults != null && cachedResults.Any())
            {
                _logger.LogDebug("Returning {Count} cached MMR results", cachedResults.Count);
                return cachedResults;
            }

            // If not in cache, search in primary vector store
            var results = await _primaryVectorStore.SearchWithMMRAsync(
                queryVector, topK, lambda, metadataFilter);

            // Cache results in distributed cache
            if (results.Any() && _config.Distributed.Enabled)
            {
                await _distributedCache.SetAsync(
                    cacheKey,
                    results,
                    TimeSpan.FromMinutes(15)); // Cache MMR results for 15 minutes
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching with MMR in distributed system");
            return new List<VectorSearchResult>();
        }
    }

    /// <inheritdoc/>
    public async Task<bool> CreateOrUpdateIndexAsync(string indexName, VectorIndexType indexType = VectorIndexType.HNSW)
    {
        try
        {
            var result = await _primaryVectorStore.CreateOrUpdateIndexAsync(indexName, indexType);

            if (result && _config.Distributed.Enabled)
            {
                // Broadcast index update to all instances via cache
                var cacheKey = $"index:update:{indexName}";
                await _distributedCache.SetAsync(
                    cacheKey,
                    new { IndexName = indexName, IndexType = indexType, Timestamp = DateTime.UtcNow },
                    TimeSpan.FromMinutes(5));

                _logger.LogInformation("Index {IndexName} update broadcasted to distributed system", indexName);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating/updating index {IndexName}", indexName);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<float[]?> GetVectorAsync(string id)
    {
        try
        {
            // Try distributed cache first
            if (_config.Distributed.Enabled)
            {
                var cacheKey = $"vector:{id}";
                var cachedData = await _distributedCache.GetAsync<CachedVectorData>(cacheKey);
                if (cachedData != null)
                {
                    _logger.LogDebug("Vector {Id} retrieved from distributed cache", id);
                    return cachedData.Vector;
                }
            }

            // Fallback to primary store
            return await _primaryVectorStore.GetVectorAsync(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting vector {Id}", id);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteVectorAsync(string id)
    {
        try
        {
            var result = await _primaryVectorStore.DeleteVectorAsync(id);

            if (result && _config.Distributed.Enabled)
            {
                // Remove from distributed cache
                var cacheKey = $"vector:{id}";
                await _distributedCache.RemoveAsync(cacheKey);

                // Remove from sync queue
                _syncQueue.TryRemove(id, out _);

                _logger.LogDebug("Vector {Id} deleted from distributed system", id);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting vector {Id}", id);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<int> BatchStoreVectorsAsync(List<VectorEntry> entries)
    {
        try
        {
            var result = await _primaryVectorStore.BatchStoreVectorsAsync(entries);

            if (result > 0 && _config.Distributed.Enabled)
            {
                // Cache all vectors in distributed cache
                var cacheTasks = entries.Select(async entry =>
                {
                    var cacheKey = $"vector:{entry.Id}";
                    var vectorData = new CachedVectorData
                    {
                        Id = entry.Id,
                        Vector = entry.Vector,
                        Metadata = entry.Metadata,
                        Timestamp = DateTime.UtcNow
                    };

                    await _distributedCache.SetAsync(cacheKey, vectorData, TimeSpan.FromHours(24));
                    _syncQueue.TryAdd(entry.Id, 0);
                });

                await Task.WhenAll(cacheTasks);

                _logger.LogInformation("Batch stored {Count} vectors in distributed system", result);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error batch storing vectors");
            return 0;
        }
    }

    /// <inheritdoc/>
    public async Task<VectorDatabaseStats> GetStatsAsync()
    {
        return await _primaryVectorStore.GetStatsAsync();
    }

    // Helper methods

    private string GenerateSearchCacheKey(
        float[] queryVector,
        int topK,
        Dictionary<string, object>? metadataFilter,
        double minSimilarity)
    {
        // Create a hash of the query parameters
        var queryHash = ComputeVectorHash(queryVector);
        var filterHash = metadataFilter != null
            ? string.Join("|", metadataFilter.Select(kv => $"{kv.Key}={kv.Value}"))
            : "none";

        return $"search:{queryHash}:{topK}:{minSimilarity:F2}:{filterHash}";
    }

    private string GenerateMMRCacheKey(
        float[] queryVector,
        int topK,
        double lambda,
        Dictionary<string, object>? metadataFilter)
    {
        var queryHash = ComputeVectorHash(queryVector);
        var filterHash = metadataFilter != null
            ? string.Join("|", metadataFilter.Select(kv => $"{kv.Key}={kv.Value}"))
            : "none";

        return $"mmr:{queryHash}:{topK}:{lambda:F2}:{filterHash}";
    }

    private string ComputeVectorHash(float[] vector)
    {
        // Simple hash of first and last few elements for cache key
        if (vector.Length == 0) return "empty";

        var elements = new List<float>();
        elements.AddRange(vector.Take(5));
        if (vector.Length > 10)
        {
            elements.AddRange(vector.Skip(vector.Length - 5));
        }

        return string.Join("_", elements.Select(v => v.ToString("F4")));
    }
}

/// <summary>
/// Cached vector data structure
/// </summary>
internal class CachedVectorData
{
    public string Id { get; set; } = string.Empty;
    public float[] Vector { get; set; } = Array.Empty<float>();
    public Dictionary<string, object>? Metadata { get; set; }
    public DateTime Timestamp { get; set; }
}
