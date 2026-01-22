namespace DocN.Data.Models;

/// <summary>
/// Represents a chunk of a document for more granular vector search
/// Documents are split into smaller chunks to improve retrieval accuracy
/// </summary>
public class DocumentChunk
{
    /// <summary>
    /// Unique identifier for the chunk
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// ID of the parent document
    /// </summary>
    public int DocumentId { get; set; }

    /// <summary>
    /// Parent document reference
    /// </summary>
    public virtual Document? Document { get; set; }

    /// <summary>
    /// Index of this chunk within the document (0-based)
    /// </summary>
    public int ChunkIndex { get; set; }

    /// <summary>
    /// Text content of this chunk
    /// </summary>
    public string ChunkText { get; set; } = string.Empty;

    // Vector embeddings for semantic search - separate fields for different dimensions
    // Using native SQL Server VECTOR type for optimal performance
    
    /// <summary>
    /// 768-dimensional embedding vector (for Gemini and similar providers)
    /// Stored as VECTOR(768) in SQL Server 2025
    /// </summary>
    public float[]? ChunkEmbedding768 { get; set; }
    
    /// <summary>
    /// 1536-dimensional embedding vector (for OpenAI ada-002 and similar providers)
    /// Stored as VECTOR(1536) in SQL Server 2025
    /// </summary>
    public float[]? ChunkEmbedding1536 { get; set; }
    
    /// <summary>
    /// The actual dimension of the embedding vector stored.
    /// Indicates which vector field is populated: 768 or 1536
    /// </summary>
    public int? EmbeddingDimension { get; set; }
    
    /// <summary>
    /// Unified property for backward compatibility - returns the populated vector field
    /// Gets/sets the appropriate field based on dimension
    /// </summary>
    public float[]? ChunkEmbedding
    {
        get
        {
            return ChunkEmbedding768 ?? ChunkEmbedding1536;
        }
        set
        {
            if (value == null)
            {
                ChunkEmbedding768 = null;
                ChunkEmbedding1536 = null;
                EmbeddingDimension = null;
            }
            else if (value.Length == Utilities.EmbeddingValidationHelper.SupportedDimension768)
            {
                ChunkEmbedding768 = value;
                ChunkEmbedding1536 = null;
                EmbeddingDimension = Utilities.EmbeddingValidationHelper.SupportedDimension768;
            }
            else if (value.Length == Utilities.EmbeddingValidationHelper.SupportedDimension1536)
            {
                ChunkEmbedding768 = null;
                ChunkEmbedding1536 = value;
                EmbeddingDimension = Utilities.EmbeddingValidationHelper.SupportedDimension1536;
            }
            else
            {
                throw new ArgumentException(
                    $"Unsupported embedding dimension: {value.Length}. " +
                    $"Expected {Utilities.EmbeddingValidationHelper.SupportedDimension768} or " +
                    $"{Utilities.EmbeddingValidationHelper.SupportedDimension1536}.");
            }
        }
    }

    /// <summary>
    /// Token count for this chunk (useful for staying within model limits)
    /// </summary>
    public int? TokenCount { get; set; }

    /// <summary>
    /// When this chunk was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Start position of this chunk in the original document text
    /// </summary>
    public int StartPosition { get; set; }

    /// <summary>
    /// End position of this chunk in the original document text
    /// </summary>
    public int EndPosition { get; set; }
    
    // Rich metadata for improved retrieval
    
    /// <summary>
    /// Title of the section this chunk belongs to
    /// </summary>
    public string? SectionTitle { get; set; }
    
    /// <summary>
    /// Section hierarchy path (e.g., "1.2.3" or "Introduction > Background")
    /// </summary>
    public string? SectionPath { get; set; }
    
    /// <summary>
    /// Auto-extracted keywords for this chunk (stored as JSON array)
    /// </summary>
    public string? KeywordsJson { get; set; }
    
    /// <summary>
    /// Document type hint (e.g., "technical", "legal", "report")
    /// </summary>
    public string? DocumentType { get; set; }
    
    /// <summary>
    /// Header level if this chunk starts with a header (0 = no header, 1-6 = H1-H6)
    /// </summary>
    public int HeaderLevel { get; set; }
    
    /// <summary>
    /// Semantic type of chunk (paragraph, section, list, sentence, etc.)
    /// </summary>
    public string ChunkType { get; set; } = "paragraph";
    
    /// <summary>
    /// Is this chunk part of a list?
    /// </summary>
    public bool IsListItem { get; set; }
    
    /// <summary>
    /// Additional custom metadata (stored as JSON)
    /// </summary>
    public string? CustomMetadataJson { get; set; }
}
