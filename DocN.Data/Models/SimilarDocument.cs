namespace DocN.Data.Models;

/// <summary>
/// Stores relationships between documents based on vector similarity analysis
/// This allows tracking which documents are semantically similar for better categorization
/// </summary>
public class SimilarDocument
{
    /// <summary>
    /// Unique identifier for the similarity relationship
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// ID of the source document (the document being analyzed)
    /// </summary>
    public int SourceDocumentId { get; set; }
    
    /// <summary>
    /// Reference to the source document
    /// </summary>
    public virtual Document SourceDocument { get; set; } = null!;

    /// <summary>
    /// ID of the similar document found through vector analysis
    /// </summary>
    public int SimilarDocumentId { get; set; }
    
    /// <summary>
    /// Reference to the similar document
    /// </summary>
    public virtual Document SimilarDocumentRef { get; set; } = null!;

    /// <summary>
    /// Similarity score (0-1) based on cosine similarity of embeddings
    /// Higher values indicate more similar documents
    /// </summary>
    public double SimilarityScore { get; set; }

    /// <summary>
    /// The most relevant text chunk from the similar document
    /// </summary>
    public string? RelevantChunk { get; set; }

    /// <summary>
    /// Index of the chunk in the similar document (if chunk-based matching)
    /// </summary>
    public int? ChunkIndex { get; set; }

    /// <summary>
    /// When this similarity relationship was identified
    /// </summary>
    public DateTime AnalyzedAt { get; set; }

    /// <summary>
    /// Rank of this similar document (1-5 typically, where 1 is most similar)
    /// </summary>
    public int Rank { get; set; }
}
