namespace DocN.Core.Configuration;

/// <summary>
/// Configuration for vector store (SQL Server based with distributed support)
/// </summary>
public class VectorStoreConfiguration
{
    /// <summary>
    /// Vector store provider: "SqlServer" (default), "PgVector" (optional)
    /// </summary>
    public string Provider { get; set; } = "SqlServer";

    /// <summary>
    /// Distributed vector store configuration
    /// </summary>
    public DistributedVectorStoreConfiguration Distributed { get; set; } = new();

    /// <summary>
    /// Optional PostgreSQL pgvector configuration for distributed scenarios
    /// </summary>
    public PgVectorConfiguration PgVector { get; set; } = new();
}

/// <summary>
/// Configuration for distributed vector store operations
/// </summary>
public class DistributedVectorStoreConfiguration
{
    /// <summary>
    /// Enable distributed vector store (uses Redis for coordination)
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Number of replicas for vector data
    /// </summary>
    public int ReplicationFactor { get; set; } = 3;

    /// <summary>
    /// Sync interval between instances in seconds
    /// </summary>
    public int SyncIntervalSeconds { get; set; } = 60;
}

/// <summary>
/// Optional PostgreSQL pgvector configuration
/// </summary>
public class PgVectorConfiguration
{
    /// <summary>
    /// Whether to use pgvector (optional, for advanced distributed scenarios)
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// PostgreSQL connection string for pgvector
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Table name for vector storage
    /// </summary>
    public string TableName { get; set; } = "document_vectors";

    /// <summary>
    /// Default vector dimension
    /// </summary>
    public int DefaultDimension { get; set; } = 1536;
}
