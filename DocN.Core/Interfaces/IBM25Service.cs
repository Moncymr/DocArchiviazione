namespace DocN.Core.Interfaces;

/// <summary>
/// Service for BM25 (Best Matching 25) scoring algorithm
/// BM25 is a probabilistic ranking function used in information retrieval
/// </summary>
public interface IBM25Service
{
    /// <summary>
    /// Calculate BM25 score for a document given a query
    /// </summary>
    /// <param name="query">Search query terms</param>
    /// <param name="documentText">Document text content</param>
    /// <param name="documentFieldWeights">Optional field-specific weights (e.g., title vs body)</param>
    /// <returns>BM25 relevance score</returns>
    double CalculateScore(string query, string documentText, Dictionary<string, double>? documentFieldWeights = null);
    
    /// <summary>
    /// Calculate BM25 scores for multiple documents
    /// </summary>
    /// <param name="query">Search query</param>
    /// <param name="documents">Collection of documents with their texts</param>
    /// <returns>Dictionary mapping document IDs to BM25 scores</returns>
    Dictionary<int, double> CalculateScores(string query, Dictionary<int, string> documents);
    
    /// <summary>
    /// Initialize or update document statistics for BM25 calculation
    /// Should be called when document collection changes
    /// </summary>
    /// <param name="documents">All documents in the collection</param>
    void UpdateDocumentStatistics(Dictionary<int, string> documents);
}
