using Microsoft.Extensions.Logging;
using DocN.Data.Models;
using DocN.Data.Utilities;
using Microsoft.EntityFrameworkCore;
using DocN.Core.Interfaces;
using DocN.Core.AI.Configuration;
using Microsoft.Extensions.Options;

namespace DocN.Data.Services;

/// <summary>
/// Enhanced vector store service with advanced features for optimal RAG
/// Supports metadata filtering, approximate nearest neighbor search, and MMR
/// </summary>
public class EnhancedVectorStoreService : IVectorStoreService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<EnhancedVectorStoreService> _logger;
    private readonly IMMRService _mmrService;
    private readonly EnhancedRAGConfiguration _config;

    public EnhancedVectorStoreService(
        ApplicationDbContext context,
        ILogger<EnhancedVectorStoreService> logger,
        IMMRService mmrService,
        IOptions<EnhancedRAGConfiguration> config)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _mmrService = mmrService ?? throw new ArgumentNullException(nameof(mmrService));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
    }

    /// <inheritdoc/>
    public async Task<bool> StoreVectorAsync(string id, float[] vector, Dictionary<string, object>? metadata = null)
    {
        try
        {
            // For now, this is a placeholder implementation
            // In production, you would store in a dedicated vector database
            _logger.LogInformation("Storing vector with ID: {Id}, Dimension: {Dim}", id, vector.Length);
            return await Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing vector with ID: {Id}", id);
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
            _logger.LogInformation(
                "Searching for similar vectors: topK={TopK}, minSimilarity={MinSimilarity}, filters={FilterCount}",
                topK, minSimilarity, metadataFilter?.Count ?? 0);

            // Start with base query
            IQueryable<DocumentChunk> query = _context.DocumentChunks
                .Include(c => c.Document)
                .Where(c => c.ChunkEmbedding768 != null || c.ChunkEmbedding1536 != null);

            // Apply metadata filters before vector search for efficiency
            query = ApplyMetadataFilters(query, metadataFilter);

            // Load candidates into memory for vector similarity calculation
            // In production with pgvector, this would be done in the database
            var candidates = await query
                .OrderByDescending(c => c.Id)
                .Take(1000) // Reasonable limit
                .ToListAsync();

            // Calculate similarity scores
            var results = new List<VectorSearchResult>();
            foreach (var chunk in candidates)
            {
                if (chunk.ChunkEmbedding == null) continue;

                var similarity = VectorMathHelper.CosineSimilarity(queryVector, chunk.ChunkEmbedding);
                if (similarity >= minSimilarity)
                {
                    results.Add(new VectorSearchResult
                    {
                        Id = chunk.Id.ToString(),
                        Vector = chunk.ChunkEmbedding,
                        SimilarityScore = similarity,
                        Metadata = BuildMetadata(chunk)
                    });
                }
            }

            // Sort by similarity and take topK
            return results
                .OrderByDescending(r => r.SimilarityScore)
                .Take(topK)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching similar vectors");
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
            // Get effective lambda: database config > parameter > appsettings
            var effectiveLambda = await GetEffectiveLambdaAsync(lambda);
            
            _logger.LogInformation(
                "Searching with MMR: topK={TopK}, lambda={Lambda} (configured={ConfiguredLambda}, database={FromDatabase})",
                topK, effectiveLambda, _config.Reranking.MMRLambda, effectiveLambda != lambda && effectiveLambda != _config.Reranking.MMRLambda);

            // First, get more candidates than needed (for better diversity)
            var candidateMultiplier = 3;
            var candidates = await SearchSimilarVectorsAsync(
                queryVector,
                topK * candidateMultiplier,
                metadataFilter,
                minSimilarity: 0.5); // Lower threshold for candidates

            if (!candidates.Any())
            {
                _logger.LogWarning("No candidates found for MMR search");
                return new List<VectorSearchResult>();
            }

            // Convert to MMR candidates
            var mmrCandidates = candidates.Select(c => new CandidateVector
            {
                Id = c.Id,
                Vector = c.Vector,
                InitialScore = c.SimilarityScore,
                Metadata = c.Metadata
            }).ToList();

            // Apply MMR reranking with configured lambda
            var mmrResults = await _mmrService.RerankWithMMRAsync(
                queryVector,
                mmrCandidates,
                topK,
                effectiveLambda);

            // Convert back to VectorSearchResult
            return mmrResults.Select(r => new VectorSearchResult
            {
                Id = r.Id,
                Vector = r.Vector,
                SimilarityScore = r.InitialScore,
                MMRScore = r.MMRScore,
                Metadata = r.Metadata
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in MMR search");
            return new List<VectorSearchResult>();
        }
    }

    /// <inheritdoc/>
    public async Task<bool> CreateOrUpdateIndexAsync(string indexName, VectorIndexType indexType = VectorIndexType.HNSW)
    {
        _logger.LogInformation("Creating/updating index: {IndexName}, Type: {IndexType}", indexName, indexType);
        
        // For SQL Server VECTOR type, indexes are handled differently
        // This is a placeholder for future pgvector implementation
        return await Task.FromResult(true);
    }

    /// <inheritdoc/>
    public async Task<float[]?> GetVectorAsync(string id)
    {
        if (!int.TryParse(id, out var chunkId))
        {
            return null;
        }

        var chunk = await _context.DocumentChunks
            .FirstOrDefaultAsync(c => c.Id == chunkId);

        return chunk?.ChunkEmbedding;
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteVectorAsync(string id)
    {
        if (!int.TryParse(id, out var chunkId))
        {
            return false;
        }

        var chunk = await _context.DocumentChunks
            .FirstOrDefaultAsync(c => c.Id == chunkId);

        if (chunk != null)
        {
            _context.DocumentChunks.Remove(chunk);
            await _context.SaveChangesAsync();
            return true;
        }

        return false;
    }

    /// <inheritdoc/>
    public async Task<int> BatchStoreVectorsAsync(List<VectorEntry> entries)
    {
        _logger.LogInformation("Batch storing {Count} vectors", entries.Count);
        
        // This is a placeholder - in production, implement batch upsert
        var successCount = 0;
        foreach (var entry in entries)
        {
            if (await StoreVectorAsync(entry.Id, entry.Vector, entry.Metadata))
            {
                successCount++;
            }
        }

        return successCount;
    }

    /// <inheritdoc/>
    public async Task<VectorDatabaseStats> GetStatsAsync()
    {
        var totalChunks = await _context.DocumentChunks
            .Where(c => c.ChunkEmbedding768 != null || c.ChunkEmbedding1536 != null)
            .CountAsync();

        var sampleChunk = await _context.DocumentChunks
            .Where(c => c.ChunkEmbedding768 != null || c.ChunkEmbedding1536 != null)
            .FirstOrDefaultAsync();

        var dimension = sampleChunk?.EmbeddingDimension ?? 0;

        return new VectorDatabaseStats
        {
            TotalVectors = totalChunks,
            VectorDimension = dimension,
            StorageSizeBytes = totalChunks * dimension * sizeof(float),
            IndexType = "SQL Server VECTOR",
            IndexExists = true
        };
    }

    // Helper methods

    private IQueryable<DocumentChunk> ApplyMetadataFilters(
        IQueryable<DocumentChunk> query,
        Dictionary<string, object>? metadataFilter)
    {
        if (metadataFilter == null || !metadataFilter.Any())
        {
            return query;
        }

        // Apply common filters
        if (metadataFilter.TryGetValue("userId", out var userId) && userId is string userIdStr)
        {
            query = query.Where(c => c.Document!.OwnerId == userIdStr);
        }

        if (metadataFilter.TryGetValue("category", out var category) && category is string categoryStr)
        {
            query = query.Where(c => c.Document!.ActualCategory == categoryStr || 
                                    c.Document!.SuggestedCategory == categoryStr);
        }

        if (metadataFilter.TryGetValue("tenantId", out var tenantId) && tenantId is int tenantIdInt)
        {
            query = query.Where(c => c.Document!.TenantId == tenantIdInt);
        }

        if (metadataFilter.TryGetValue("startDate", out var startDate) && startDate is DateTime startDt)
        {
            query = query.Where(c => c.Document!.UploadedAt >= startDt);
        }

        if (metadataFilter.TryGetValue("endDate", out var endDate) && endDate is DateTime endDt)
        {
            query = query.Where(c => c.Document!.UploadedAt <= endDt);
        }

        return query;
    }

    private Dictionary<string, object> BuildMetadata(DocumentChunk chunk)
    {
        var metadata = new Dictionary<string, object>
        {
            ["chunkId"] = chunk.Id,
            ["documentId"] = chunk.DocumentId,
            ["chunkIndex"] = chunk.ChunkIndex
        };

        if (chunk.Document != null)
        {
            metadata["fileName"] = chunk.Document.FileName;
            metadata["category"] = chunk.Document.ActualCategory ?? chunk.Document.SuggestedCategory ?? "Unknown";
            metadata["uploadedAt"] = chunk.Document.UploadedAt;
            
            if (!string.IsNullOrEmpty(chunk.Document.OwnerId))
            {
                metadata["userId"] = chunk.Document.OwnerId;
            }

            if (chunk.Document.TenantId.HasValue)
            {
                metadata["tenantId"] = chunk.Document.TenantId.Value;
            }
        }

        return metadata;
    }

    /// <summary>
    /// Get effective lambda value with priority: explicit parameter > database config > appsettings
    /// </summary>
    private async Task<double> GetEffectiveLambdaAsync(double parameterLambda)
    {
        try
        {
            // If explicitly provided (not default 0.5), use it
            if (parameterLambda != 0.5)
            {
                return parameterLambda;
            }

            // Try to get from database (active AIConfiguration)
            var dbConfig = await _context.AIConfigurations
                .Where(c => c.IsActive)
                .OrderByDescending(c => c.Id)
                .FirstOrDefaultAsync();

            if (dbConfig != null && dbConfig.MMRLambda > 0 && dbConfig.MMRLambda <= 1.0)
            {
                _logger.LogDebug("Using MMR Lambda from database: {Lambda}", dbConfig.MMRLambda);
                return dbConfig.MMRLambda;
            }

            // Fallback to appsettings.json config
            _logger.LogDebug("Using MMR Lambda from appsettings: {Lambda}", _config.Reranking.MMRLambda);
            return _config.Reranking.MMRLambda;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error loading MMR Lambda from database, using appsettings");
            return _config.Reranking.MMRLambda;
        }
    }
}
