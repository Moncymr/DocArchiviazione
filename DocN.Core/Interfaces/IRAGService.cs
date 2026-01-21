namespace DocN.Core.Interfaces;

/// <summary>
/// Interface for RAG (Retrieval Augmented Generation) service
/// </summary>
public interface IRAGService
{
    /// <summary>
    /// Generate response using RAG with conversation history
    /// </summary>
    /// <param name="query">User query</param>
    /// <param name="userId">User ID for document access control</param>
    /// <param name="conversationId">Optional conversation ID for context</param>
    /// <param name="specificDocumentIds">Optional specific documents to search</param>
    /// <returns>Chat response with answer and references</returns>
    Task<object> GenerateResponseAsync(string query, string userId, int? conversationId = null, List<int>? specificDocumentIds = null);

    /// <summary>
    /// Generate streaming response for real-time display
    /// </summary>
    /// <param name="query">User query</param>
    /// <param name="userId">User ID</param>
    /// <param name="conversationId">Optional conversation ID</param>
    /// <returns>Async enumerable of response chunks</returns>
    IAsyncEnumerable<string> GenerateStreamingResponseAsync(string query, string userId, int? conversationId = null);
}
