namespace DocN.Data.Constants;

/// <summary>
/// Constants for document chunk embedding status values
/// </summary>
public static class ChunkEmbeddingStatus
{
    /// <summary>
    /// Chunks are created but embeddings not yet generated (waiting for background processing)
    /// </summary>
    public const string Pending = "Pending";
    
    /// <summary>
    /// Embeddings are currently being generated
    /// </summary>
    public const string Processing = "Processing";
    
    /// <summary>
    /// All chunk embeddings have been successfully generated
    /// </summary>
    public const string Completed = "Completed";
    
    /// <summary>
    /// Document doesn't have text content or chunking not needed
    /// </summary>
    public const string NotRequired = "NotRequired";
}
