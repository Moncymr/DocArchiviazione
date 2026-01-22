namespace DocN.Data.Models;

/// <summary>
/// Represents a retrieval evaluation query with ground truth
/// </summary>
public class RetrievalEvaluationQuery
{
    /// <summary>
    /// Unique identifier
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// Query text
    /// </summary>
    public string QueryText { get; set; } = string.Empty;
    
    /// <summary>
    /// Expected relevant document IDs in order of relevance (JSON array)
    /// </summary>
    public string RelevantDocumentIdsJson { get; set; } = string.Empty;
    
    /// <summary>
    /// Expected relevant chunk IDs in order of relevance (JSON array)
    /// </summary>
    public string? RelevantChunkIdsJson { get; set; }
    
    /// <summary>
    /// Relevance scores for each document (JSON object: {docId: score})
    /// </summary>
    public string? RelevanceScoresJson { get; set; }
    
    /// <summary>
    /// Domain/category for this query
    /// </summary>
    public string? Domain { get; set; }
    
    /// <summary>
    /// Difficulty level (Easy, Medium, Hard)
    /// </summary>
    public string? DifficultyLevel { get; set; }
    
    /// <summary>
    /// Expected answer or description
    /// </summary>
    public string? ExpectedAnswer { get; set; }
    
    /// <summary>
    /// When this evaluation query was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Whether this query is part of the active evaluation set
    /// </summary>
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Represents the result of a retrieval evaluation run
/// </summary>
public class RetrievalEvaluationResult
{
    /// <summary>
    /// Unique identifier
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// Evaluation run name/description
    /// </summary>
    public string EvaluationName { get; set; } = string.Empty;
    
    /// <summary>
    /// Configuration used for this evaluation (JSON)
    /// </summary>
    public string ConfigurationJson { get; set; } = string.Empty;
    
    /// <summary>
    /// Model/system version being evaluated
    /// </summary>
    public string ModelVersion { get; set; } = string.Empty;
    
    /// <summary>
    /// Chunking strategy used
    /// </summary>
    public string? ChunkingStrategy { get; set; }
    
    /// <summary>
    /// Number of queries evaluated
    /// </summary>
    public int TotalQueries { get; set; }
    
    /// <summary>
    /// Mean Reciprocal Rank (MRR) score
    /// </summary>
    public double MRRScore { get; set; }
    
    /// <summary>
    /// Normalized Discounted Cumulative Gain at k=5
    /// </summary>
    public double NDCG_at_5 { get; set; }
    
    /// <summary>
    /// Normalized Discounted Cumulative Gain at k=10
    /// </summary>
    public double NDCG_at_10 { get; set; }
    
    /// <summary>
    /// Precision at k=1
    /// </summary>
    public double Precision_at_1 { get; set; }
    
    /// <summary>
    /// Precision at k=5
    /// </summary>
    public double Precision_at_5 { get; set; }
    
    /// <summary>
    /// Recall at k=5
    /// </summary>
    public double Recall_at_5 { get; set; }
    
    /// <summary>
    /// Recall at k=10
    /// </summary>
    public double Recall_at_10 { get; set; }
    
    /// <summary>
    /// Mean Average Precision (MAP)
    /// </summary>
    public double MAP { get; set; }
    
    /// <summary>
    /// Detailed per-query results (JSON)
    /// </summary>
    public string? DetailedResultsJson { get; set; }
    
    /// <summary>
    /// When this evaluation was run
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Duration of the evaluation in milliseconds
    /// </summary>
    public long? DurationMs { get; set; }
    
    /// <summary>
    /// Additional notes or comments
    /// </summary>
    public string? Notes { get; set; }
}

/// <summary>
/// Stores individual query results for detailed analysis
/// </summary>
public class QueryEvaluationDetail
{
    /// <summary>
    /// Unique identifier
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// Parent evaluation result ID
    /// </summary>
    public int EvaluationResultId { get; set; }
    
    /// <summary>
    /// Evaluation query ID
    /// </summary>
    public int EvaluationQueryId { get; set; }
    
    /// <summary>
    /// Retrieved document IDs in order (JSON array)
    /// </summary>
    public string RetrievedDocumentIdsJson { get; set; } = string.Empty;
    
    /// <summary>
    /// Retrieval scores for each document (JSON object)
    /// </summary>
    public string RetrievalScoresJson { get; set; } = string.Empty;
    
    /// <summary>
    /// Reciprocal Rank for this query
    /// </summary>
    public double ReciprocalRank { get; set; }
    
    /// <summary>
    /// NDCG score for this query at k=10
    /// </summary>
    public double NDCG { get; set; }
    
    /// <summary>
    /// Average Precision for this query
    /// </summary>
    public double AveragePrecision { get; set; }
    
    /// <summary>
    /// Whether the first result was relevant
    /// </summary>
    public bool FirstResultRelevant { get; set; }
    
    /// <summary>
    /// Rank of the first relevant document (1-based, 0 if none found)
    /// </summary>
    public int FirstRelevantRank { get; set; }
    
    /// <summary>
    /// Number of relevant documents retrieved in top-k
    /// </summary>
    public int RelevantRetrievedCount { get; set; }
    
    /// <summary>
    /// Total number of expected relevant documents
    /// </summary>
    public int TotalRelevantCount { get; set; }
    
    /// <summary>
    /// Query evaluation timestamp
    /// </summary>
    public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;
}
