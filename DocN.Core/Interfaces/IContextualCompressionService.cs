namespace DocN.Core.Interfaces;

/// <summary>
/// Service for contextual compression - reduces token count while preserving relevance
/// </summary>
/// <remarks>
/// Contextual compression helps:
/// - Reduce token usage and costs
/// - Fit more relevant information in context window
/// - Remove redundant or irrelevant parts of retrieved documents
/// - Improve response quality by focusing on key information
/// 
/// Techniques used:
/// - Extractive summarization
/// - Relevance-based filtering
/// - Semantic deduplication
/// </remarks>
public interface IContextualCompressionService
{
    /// <summary>
    /// Compress a list of document chunks by removing irrelevant content
    /// </summary>
    /// <param name="query">Original user query for relevance context</param>
    /// <param name="chunks">List of document chunks to compress</param>
    /// <param name="targetTokenCount">Target token count after compression</param>
    /// <returns>Compressed chunks with only relevant content</returns>
    Task<List<CompressedChunk>> CompressChunksAsync(
        string query,
        List<string> chunks,
        int targetTokenCount);

    /// <summary>
    /// Compress a single text by extracting only relevant sentences
    /// </summary>
    /// <param name="query">Original user query</param>
    /// <param name="text">Text to compress</param>
    /// <param name="maxTokens">Maximum tokens in result</param>
    /// <returns>Compressed text</returns>
    Task<string> CompressTextAsync(
        string query,
        string text,
        int maxTokens);

    /// <summary>
    /// Remove duplicate or redundant information across multiple chunks
    /// </summary>
    /// <param name="chunks">List of chunks to deduplicate</param>
    /// <param name="similarityThreshold">Threshold for considering chunks similar (0-1)</param>
    /// <returns>Deduplicated chunks</returns>
    Task<List<string>> DeduplicateChunksAsync(
        List<string> chunks,
        double similarityThreshold = 0.85);

    /// <summary>
    /// Estimate token count for a given text
    /// </summary>
    /// <param name="text">Text to estimate</param>
    /// <returns>Estimated token count</returns>
    int EstimateTokenCount(string text);

    /// <summary>
    /// Extract the most relevant sentences from text based on query
    /// </summary>
    /// <param name="query">User query</param>
    /// <param name="text">Text to extract from</param>
    /// <param name="maxSentences">Maximum number of sentences to extract</param>
    /// <returns>Most relevant sentences</returns>
    Task<List<string>> ExtractRelevantSentencesAsync(
        string query,
        string text,
        int maxSentences);
}

/// <summary>
/// Compressed chunk with metadata
/// </summary>
public class CompressedChunk
{
    /// <summary>
    /// Compressed text content
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Original chunk index
    /// </summary>
    public int OriginalIndex { get; set; }

    /// <summary>
    /// Compression ratio (0-1, where 0.5 means 50% reduction)
    /// </summary>
    public double CompressionRatio { get; set; }

    /// <summary>
    /// Relevance score to query (0-1)
    /// </summary>
    public double RelevanceScore { get; set; }

    /// <summary>
    /// Token count after compression
    /// </summary>
    public int TokenCount { get; set; }
}

/// <summary>
/// Configuration for contextual compression
/// </summary>
public class ContextualCompressionConfiguration
{
    /// <summary>
    /// Enable contextual compression
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Target compression ratio (0-1, where 0.5 means compress to 50% of original)
    /// </summary>
    public double TargetCompressionRatio { get; set; } = 0.6;

    /// <summary>
    /// Minimum relevance score for keeping content (0-1)
    /// </summary>
    public double MinRelevanceScore { get; set; } = 0.3;

    /// <summary>
    /// Enable semantic deduplication
    /// </summary>
    public bool EnableDeduplication { get; set; } = true;

    /// <summary>
    /// Similarity threshold for deduplication (0-1)
    /// </summary>
    public double DeduplicationThreshold { get; set; } = 0.85;

    /// <summary>
    /// Use extractive summarization for compression
    /// </summary>
    public bool UseExtractiveSummarization { get; set; } = true;
}
