using DocN.Core.AI.Models;

namespace DocN.Core.Interfaces;

/// <summary>
/// Advanced RAG service interface using Microsoft Semantic Kernel and Agent Framework
/// Provides vector-based retrieval and chat capabilities on uploaded documents
/// </summary>
public interface ISemanticRAGService
{
    /// <summary>
    /// Generate a response using RAG with conversation context and vector search
    /// </summary>
    /// <param name="query">User's question or query</param>
    /// <param name="userId">User ID for access control</param>
    /// <param name="conversationId">Optional conversation ID for context</param>
    /// <param name="specificDocumentIds">Optional specific documents to search within</param>
    /// <param name="topK">Number of top documents/chunks to retrieve</param>
    /// <returns>RAG response with answer and source documents</returns>
    Task<SemanticRAGResponse> GenerateResponseAsync(
        string query, 
        string userId, 
        int? conversationId = null,
        List<int>? specificDocumentIds = null,
        int topK = 5);

    /// <summary>
    /// Generate streaming response for real-time chat experience
    /// </summary>
    /// <param name="query">User's question</param>
    /// <param name="userId">User ID</param>
    /// <param name="conversationId">Optional conversation ID</param>
    /// <param name="specificDocumentIds">Optional specific documents</param>
    /// <returns>Async enumerable of response chunks</returns>
    IAsyncEnumerable<string> GenerateStreamingResponseAsync(
        string query, 
        string userId, 
        int? conversationId = null,
        List<int>? specificDocumentIds = null);

    /// <summary>
    /// Search documents using vector embeddings
    /// </summary>
    /// <param name="query">Search query</param>
    /// <param name="userId">User ID for access control</param>
    /// <param name="topK">Number of results to return</param>
    /// <param name="minSimilarity">Minimum similarity threshold (0-1)</param>
    /// <returns>List of relevant documents with similarity scores</returns>
    Task<List<RelevantDocumentResult>> SearchDocumentsAsync(
        string query,
        string userId,
        int topK = 10,
        double minSimilarity = 0.7);

    /// <summary>
    /// Search documents using a pre-generated embedding vector
    /// </summary>
    /// <param name="queryEmbedding">Pre-generated embedding vector to search with</param>
    /// <param name="userId">User ID for access control</param>
    /// <param name="topK">Number of results to return</param>
    /// <param name="minSimilarity">Minimum similarity threshold (0-1)</param>
    /// <returns>List of relevant documents with similarity scores</returns>
    Task<List<RelevantDocumentResult>> SearchDocumentsWithEmbeddingAsync(
        float[] queryEmbedding,
        string userId,
        int topK = 10,
        double minSimilarity = 0.7);
}

/// <summary>
/// Response from Semantic RAG service
/// </summary>
public class SemanticRAGResponse
{
    /// <summary>
    /// Generated answer from the AI
    /// </summary>
    public string Answer { get; set; } = string.Empty;

    /// <summary>
    /// Source documents used to generate the answer
    /// </summary>
    public List<RelevantDocumentResult> SourceDocuments { get; set; } = new();

    /// <summary>
    /// Conversation ID for continuing the chat
    /// </summary>
    public int ConversationId { get; set; }

    /// <summary>
    /// Time taken to generate response (milliseconds)
    /// </summary>
    public long ResponseTimeMs { get; set; }

    /// <summary>
    /// Whether the response was retrieved from cache
    /// </summary>
    public bool FromCache { get; set; }

    /// <summary>
    /// Additional metadata about the RAG process
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Relevant document result with similarity score
/// </summary>
public class RelevantDocumentResult
{
    /// <summary>
    /// Document ID
    /// </summary>
    public int DocumentId { get; set; }

    /// <summary>
    /// Document file name
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Document category
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Similarity score (0-1)
    /// </summary>
    public double SimilarityScore { get; set; }

    /// <summary>
    /// Relevant text chunk from the document
    /// </summary>
    public string? RelevantChunk { get; set; }

    /// <summary>
    /// Chunk index in the document
    /// </summary>
    public int? ChunkIndex { get; set; }

    /// <summary>
    /// Full extracted text (optional)
    /// </summary>
    public string? ExtractedText { get; set; }
}
