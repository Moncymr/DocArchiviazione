using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Pgvector;
using DocN.Core.Interfaces;
using DocN.Core.AI.Configuration;
using DocN.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace DocN.Data.Services;

/// <summary>
/// PostgreSQL pgvector implementation for optimal vector search
/// Supports HNSW index, approximate nearest neighbor (ANN) search, and metadata filtering
/// </summary>
public class PgVectorStoreService : IVectorStoreService
{
    private readonly ILogger<PgVectorStoreService> _logger;
    private readonly PgVectorConfiguration _config;
    private readonly IMMRService _mmrService;
    private readonly EnhancedRAGConfiguration _ragConfig;
    private readonly ApplicationDbContext _context;
    private readonly string _connectionString;

    public PgVectorStoreService(
        ILogger<PgVectorStoreService> logger,
        IOptions<PgVectorConfiguration> config,
        IMMRService mmrService,
        IOptions<EnhancedRAGConfiguration> ragConfig,
        ApplicationDbContext context)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config.Value ?? throw new ArgumentNullException(nameof(config));
        _mmrService = mmrService ?? throw new ArgumentNullException(nameof(mmrService));
        _ragConfig = ragConfig?.Value ?? throw new ArgumentNullException(nameof(ragConfig));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _connectionString = _config.ConnectionString;

        // Initialize pgvector
        InitializePgVector();
    }

    private void InitializePgVector()
    {
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            // Enable pgvector extension
            using var cmd = new NpgsqlCommand("CREATE EXTENSION IF NOT EXISTS vector", connection);
            cmd.ExecuteNonQuery();

            _logger.LogInformation("pgvector extension enabled successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize pgvector extension");
        }
    }

    /// <inheritdoc/>
    public async Task<bool> StoreVectorAsync(string id, float[] vector, Dictionary<string, object>? metadata = null)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            var metadataJson = metadata != null 
                ? System.Text.Json.JsonSerializer.Serialize(metadata) 
                : "{}";

            var sql = @"
                INSERT INTO document_vectors (id, embedding, metadata, created_at)
                VALUES (@id, @embedding, @metadata::jsonb, @createdAt)
                ON CONFLICT (id) DO UPDATE 
                SET embedding = @embedding, 
                    metadata = @metadata::jsonb,
                    updated_at = @updatedAt";

            await using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("embedding", new Vector(vector));
            cmd.Parameters.AddWithValue("metadata", metadataJson);
            cmd.Parameters.AddWithValue("createdAt", DateTime.UtcNow);
            cmd.Parameters.AddWithValue("updatedAt", DateTime.UtcNow);

            await cmd.ExecuteNonQueryAsync();
            _logger.LogDebug("Stored vector with ID: {Id}", id);
            return true;
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
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            // Build WHERE clause for metadata filtering
            var whereClause = BuildMetadataFilter(metadataFilter);

            // Use cosine distance (1 - cosine similarity) with pgvector
            // HNSW index will automatically be used if it exists
            var sql = $@"
                SELECT 
                    id,
                    embedding,
                    metadata,
                    1 - (embedding <=> @queryVector) as similarity
                FROM document_vectors
                {whereClause}
                ORDER BY embedding <=> @queryVector
                LIMIT @limit";

            await using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("queryVector", new Vector(queryVector));
            cmd.Parameters.AddWithValue("limit", topK * 2); // Get more for filtering

            var results = new List<VectorSearchResult>();

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var similarity = reader.GetDouble(3);
                
                // Filter by minimum similarity
                if (similarity < minSimilarity)
                    continue;

                var vectorData = reader.GetFieldValue<Vector>(1);
                var metadataJson = reader.GetString(2);
                var metadata = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(metadataJson) 
                              ?? new Dictionary<string, object>();

                results.Add(new VectorSearchResult
                {
                    Id = reader.GetString(0),
                    Vector = vectorData.ToArray(),
                    SimilarityScore = similarity,
                    Metadata = metadata
                });

                if (results.Count >= topK)
                    break;
            }

            _logger.LogInformation("Found {Count} similar vectors (threshold: {Threshold})", results.Count, minSimilarity);
            return results;
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
            // Get more candidates for MMR
            var candidates = await SearchSimilarVectorsAsync(
                queryVector,
                topK * 3,
                metadataFilter,
                minSimilarity: 0.5);

            if (!candidates.Any())
            {
                return new List<VectorSearchResult>();
            }

            // Get effective lambda: database config > parameter > appsettings
            var effectiveLambda = await GetEffectiveLambdaAsync(lambda);
            
            _logger.LogInformation(
                "MMR reranking with lambda={Lambda} (configured={ConfiguredLambda}, database={FromDatabase})",
                effectiveLambda, _ragConfig.Reranking.MMRLambda, effectiveLambda != lambda && effectiveLambda != _ragConfig.Reranking.MMRLambda);

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
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            // Drop existing index if exists
            var dropSql = $"DROP INDEX IF EXISTS {indexName}";
            await using var dropCmd = new NpgsqlCommand(dropSql, connection);
            await dropCmd.ExecuteNonQueryAsync();

            // Create index based on type
            string createSql = indexType switch
            {
                VectorIndexType.HNSW => $@"
                    CREATE INDEX {indexName} ON document_vectors 
                    USING hnsw (embedding vector_cosine_ops)
                    WITH (m = 16, ef_construction = 64)",
                
                VectorIndexType.IVFFlat => $@"
                    CREATE INDEX {indexName} ON document_vectors 
                    USING ivfflat (embedding vector_cosine_ops)
                    WITH (lists = 100)",
                
                VectorIndexType.Flat => $@"
                    CREATE INDEX {indexName} ON document_vectors 
                    USING btree (id)",
                
                _ => throw new ArgumentException($"Unsupported index type: {indexType}")
            };

            await using var createCmd = new NpgsqlCommand(createSql, connection);
            await createCmd.ExecuteNonQueryAsync();

            _logger.LogInformation("Created {IndexType} index: {IndexName}", indexType, indexName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating index: {IndexName}", indexName);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<float[]?> GetVectorAsync(string id)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = "SELECT embedding FROM document_vectors WHERE id = @id";
            await using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("id", id);

            var result = await cmd.ExecuteScalarAsync();
            if (result is Vector vector)
            {
                return vector.ToArray();
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting vector with ID: {Id}", id);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteVectorAsync(string id)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = "DELETE FROM document_vectors WHERE id = @id";
            await using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("id", id);

            var rowsAffected = await cmd.ExecuteNonQueryAsync();
            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting vector with ID: {Id}", id);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<int> BatchStoreVectorsAsync(List<VectorEntry> entries)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var transaction = await connection.BeginTransactionAsync();

            var successCount = 0;
            foreach (var entry in entries)
            {
                var metadataJson = entry.Metadata != null 
                    ? System.Text.Json.JsonSerializer.Serialize(entry.Metadata) 
                    : "{}";

                var sql = @"
                    INSERT INTO document_vectors (id, embedding, metadata, created_at)
                    VALUES (@id, @embedding, @metadata::jsonb, @createdAt)
                    ON CONFLICT (id) DO UPDATE 
                    SET embedding = @embedding, 
                        metadata = @metadata::jsonb,
                        updated_at = @updatedAt";

                await using var cmd = new NpgsqlCommand(sql, connection, transaction);
                cmd.Parameters.AddWithValue("id", entry.Id);
                cmd.Parameters.AddWithValue("embedding", new Vector(entry.Vector));
                cmd.Parameters.AddWithValue("metadata", metadataJson);
                cmd.Parameters.AddWithValue("createdAt", DateTime.UtcNow);
                cmd.Parameters.AddWithValue("updatedAt", DateTime.UtcNow);

                await cmd.ExecuteNonQueryAsync();
                successCount++;
            }

            await transaction.CommitAsync();
            _logger.LogInformation("Batch stored {Count} vectors", successCount);
            return successCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in batch store vectors");
            return 0;
        }
    }

    /// <inheritdoc/>
    public async Task<VectorDatabaseStats> GetStatsAsync()
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT 
                    COUNT(*) as total_vectors,
                    pg_size_pretty(pg_total_relation_size('document_vectors')) as storage_size
                FROM document_vectors";

            await using var cmd = new NpgsqlCommand(sql, connection);
            await using var reader = await cmd.ExecuteReaderAsync();
            
            if (await reader.ReadAsync())
            {
                var totalVectors = reader.GetInt64(0);
                var storageSize = reader.GetString(1);

                // Get dimension from first vector
                reader.Close();
                var dimSql = "SELECT embedding FROM document_vectors LIMIT 1";
                await using var dimCmd = new NpgsqlCommand(dimSql, connection);
                var firstVector = await dimCmd.ExecuteScalarAsync() as Vector;

                return new VectorDatabaseStats
                {
                    TotalVectors = totalVectors,
                    VectorDimension = firstVector?.ToArray().Length ?? 0,
                    StorageSizeBytes = 0, // Would need to parse pg_size_pretty
                    IndexType = "pgvector HNSW",
                    IndexExists = true
                };
            }

            return new VectorDatabaseStats();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting stats");
            return new VectorDatabaseStats();
        }
    }

    // Helper methods

    private string BuildMetadataFilter(Dictionary<string, object>? metadataFilter)
    {
        if (metadataFilter == null || !metadataFilter.Any())
        {
            return "";
        }

        var conditions = new List<string>();
        foreach (var filter in metadataFilter)
        {
            // Build JSONB filter conditions
            conditions.Add($"metadata->'{filter.Key}' = '\"{filter.Value}\"'");
        }

        return conditions.Any() ? "WHERE " + string.Join(" AND ", conditions) : "";
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
            _logger.LogDebug("Using MMR Lambda from appsettings: {Lambda}", _ragConfig.Reranking.MMRLambda);
            return _ragConfig.Reranking.MMRLambda;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error loading MMR Lambda from database, using appsettings");
            return _ragConfig.Reranking.MMRLambda;
        }
    }
}

/// <summary>
/// Configuration for pgvector
/// </summary>
public class PgVectorConfiguration
{
    public string ConnectionString { get; set; } = string.Empty;
    public string TableName { get; set; } = "document_vectors";
    public int DefaultDimension { get; set; } = 1536;
    public bool AutoCreateTable { get; set; } = true;
}
