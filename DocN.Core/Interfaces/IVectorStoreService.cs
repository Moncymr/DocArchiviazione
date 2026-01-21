namespace DocN.Core.Interfaces;

/// <summary>
/// Interface for vector database operations supporting multiple backends (SQL Server, PostgreSQL with pgvector)
/// </summary>
public interface IVectorStoreService
{
    /// <summary>
    /// Store a vector embedding with metadata
    /// </summary>
    Task<bool> StoreVectorAsync(string id, float[] vector, Dictionary<string, object>? metadata = null);

    /// <summary>
    /// Search for similar vectors using approximate nearest neighbor (ANN) search
    /// Supports HNSW index for fast similarity search
    /// </summary>
    Task<List<VectorSearchResult>> SearchSimilarVectorsAsync(
        float[] queryVector,
        int topK = 10,
        Dictionary<string, object>? metadataFilter = null,
        double minSimilarity = 0.7);

    /// <summary>
    /// Search with Maximal Marginal Relevance (MMR) for diversity
    /// </summary>
    Task<List<VectorSearchResult>> SearchWithMMRAsync(
        float[] queryVector,
        int topK = 10,
        double lambda = 0.5,
        Dictionary<string, object>? metadataFilter = null);

    /// <summary>
    /// Create or update vector index for fast ANN search
    /// </summary>
    Task<bool> CreateOrUpdateIndexAsync(string indexName, VectorIndexType indexType = VectorIndexType.HNSW);

    /// <summary>
    /// Get vector by ID
    /// </summary>
    Task<float[]?> GetVectorAsync(string id);

    /// <summary>
    /// Delete vector by ID
    /// </summary>
    Task<bool> DeleteVectorAsync(string id);

    /// <summary>
    /// Batch store vectors for efficiency
    /// </summary>
    Task<int> BatchStoreVectorsAsync(List<VectorEntry> entries);

    /// <summary>
    /// Get database statistics
    /// </summary>
    Task<VectorDatabaseStats> GetStatsAsync();
}

/// <summary>
/// Result from vector similarity search
/// </summary>
public class VectorSearchResult
{
    public string Id { get; set; } = string.Empty;
    public float[] Vector { get; set; } = Array.Empty<float>();
    public double SimilarityScore { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
    public double? MMRScore { get; set; }
}

/// <summary>
/// Entry for batch vector storage
/// </summary>
public class VectorEntry
{
    public string Id { get; set; } = string.Empty;
    public float[] Vector { get; set; } = Array.Empty<float>();
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Vector index types
/// </summary>
public enum VectorIndexType
{
    /// <summary>
    /// Hierarchical Navigable Small World - best for most use cases
    /// </summary>
    HNSW,
    
    /// <summary>
    /// Inverted File with Product Quantization - good for large datasets
    /// </summary>
    IVFFlat,
    
    /// <summary>
    /// Flat index - exact search, slower but accurate
    /// </summary>
    Flat
}

/// <summary>
/// Vector database statistics
/// </summary>
public class VectorDatabaseStats
{
    public long TotalVectors { get; set; }
    public int VectorDimension { get; set; }
    public long StorageSizeBytes { get; set; }
    public string IndexType { get; set; } = string.Empty;
    public bool IndexExists { get; set; }
}
