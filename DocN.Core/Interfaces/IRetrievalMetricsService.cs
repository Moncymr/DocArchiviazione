namespace DocN.Core.Interfaces;

/// <summary>
/// Service for calculating retrieval metrics (MRR, NDCG, MAP, etc.)
/// </summary>
public interface IRetrievalMetricsService
{
    /// <summary>
    /// Calculate Mean Reciprocal Rank (MRR)
    /// </summary>
    /// <param name="retrievedIds">List of retrieved document/chunk IDs for each query</param>
    /// <param name="relevantIds">List of relevant document/chunk IDs for each query</param>
    /// <returns>MRR score (0.0 to 1.0)</returns>
    double CalculateMRR(List<List<int>> retrievedIds, List<List<int>> relevantIds);
    
    /// <summary>
    /// Calculate Normalized Discounted Cumulative Gain (NDCG)
    /// </summary>
    /// <param name="retrievedIds">Retrieved document/chunk IDs</param>
    /// <param name="relevanceScores">Relevance scores for each document (graded relevance)</param>
    /// <param name="k">Number of top results to consider</param>
    /// <returns>NDCG score (0.0 to 1.0)</returns>
    double CalculateNDCG(List<int> retrievedIds, Dictionary<int, double> relevanceScores, int k = 10);
    
    /// <summary>
    /// Calculate Mean Average Precision (MAP)
    /// </summary>
    /// <param name="retrievedIds">List of retrieved document/chunk IDs for each query</param>
    /// <param name="relevantIds">List of relevant document/chunk IDs for each query</param>
    /// <returns>MAP score (0.0 to 1.0)</returns>
    double CalculateMAP(List<List<int>> retrievedIds, List<List<int>> relevantIds);
    
    /// <summary>
    /// Calculate Precision at k
    /// </summary>
    /// <param name="retrievedIds">Retrieved document/chunk IDs</param>
    /// <param name="relevantIds">Relevant document/chunk IDs</param>
    /// <param name="k">Number of top results to consider</param>
    /// <returns>Precision score (0.0 to 1.0)</returns>
    double CalculatePrecisionAtK(List<int> retrievedIds, List<int> relevantIds, int k);
    
    /// <summary>
    /// Calculate Recall at k
    /// </summary>
    /// <param name="retrievedIds">Retrieved document/chunk IDs</param>
    /// <param name="relevantIds">Relevant document/chunk IDs</param>
    /// <param name="k">Number of top results to consider</param>
    /// <returns>Recall score (0.0 to 1.0)</returns>
    double CalculateRecallAtK(List<int> retrievedIds, List<int> relevantIds, int k);
    
    /// <summary>
    /// Run a complete evaluation on an evaluation dataset
    /// </summary>
    Task<EvaluationMetricsResult> EvaluateRetrievalAsync(
        string evaluationName,
        List<EvaluationQuery> queries,
        string modelVersion,
        string? chunkingStrategy = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get historical evaluation results for comparison
    /// </summary>
    Task<List<EvaluationMetricsResult>> GetEvaluationHistoryAsync(
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Compare two evaluation results
    /// </summary>
    Task<EvaluationComparison> CompareEvaluationsAsync(
        int evaluationId1,
        int evaluationId2,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Evaluation query with ground truth
/// </summary>
public class EvaluationQuery
{
    public int QueryId { get; set; }
    public string QueryText { get; set; } = string.Empty;
    public List<int> RelevantDocumentIds { get; set; } = new();
    public List<int>? RelevantChunkIds { get; set; }
    public Dictionary<int, double>? RelevanceScores { get; set; }
    public string? Domain { get; set; }
}

/// <summary>
/// Complete evaluation metrics result
/// </summary>
public class EvaluationMetricsResult
{
    public int EvaluationId { get; set; }
    public string EvaluationName { get; set; } = string.Empty;
    public string ModelVersion { get; set; } = string.Empty;
    public string? ChunkingStrategy { get; set; }
    public int TotalQueries { get; set; }
    public double MRRScore { get; set; }
    public double NDCG_at_5 { get; set; }
    public double NDCG_at_10 { get; set; }
    public double Precision_at_1 { get; set; }
    public double Precision_at_5 { get; set; }
    public double Recall_at_5 { get; set; }
    public double Recall_at_10 { get; set; }
    public double MAP { get; set; }
    public DateTime CreatedAt { get; set; }
    public long DurationMs { get; set; }
    public Dictionary<int, QueryMetrics> QueryDetails { get; set; } = new();
}

/// <summary>
/// Metrics for a single query
/// </summary>
public class QueryMetrics
{
    public int QueryId { get; set; }
    public List<int> RetrievedIds { get; set; } = new();
    public Dictionary<int, double> RetrievalScores { get; set; } = new();
    public double ReciprocalRank { get; set; }
    public double NDCG { get; set; }
    public double AveragePrecision { get; set; }
    public bool FirstResultRelevant { get; set; }
    public int FirstRelevantRank { get; set; }
}

/// <summary>
/// Comparison between two evaluations
/// </summary>
public class EvaluationComparison
{
    public EvaluationMetricsResult Evaluation1 { get; set; } = new();
    public EvaluationMetricsResult Evaluation2 { get; set; } = new();
    public Dictionary<string, double> Improvements { get; set; } = new();
    public Dictionary<string, double> PercentageChanges { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
    public bool IsImprovement { get; set; }
}
