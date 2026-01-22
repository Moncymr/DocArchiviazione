namespace DocN.Core.Interfaces;

/// <summary>
/// Service for evaluating retrieval quality using standard IR metrics
/// </summary>
public interface IRetrievalMetricsService
{
    /// <summary>
    /// Calculate Mean Reciprocal Rank (MRR)
    /// Measures how well the system ranks the first relevant document
    /// </summary>
    /// <param name="retrievedDocIds">List of retrieved document IDs in rank order</param>
    /// <param name="relevantDocIds">Set of relevant document IDs</param>
    /// <returns>MRR score (0.0 to 1.0)</returns>
    double CalculateMRR(List<int> retrievedDocIds, HashSet<int> relevantDocIds);
    
    /// <summary>
    /// Calculate Normalized Discounted Cumulative Gain (NDCG)
    /// Measures ranking quality considering position and relevance
    /// </summary>
    /// <param name="retrievedDocIds">List of retrieved document IDs in rank order</param>
    /// <param name="relevanceScores">Dictionary mapping document IDs to relevance scores (0-5)</param>
    /// <param name="k">Consider only top k results</param>
    /// <returns>NDCG score (0.0 to 1.0)</returns>
    double CalculateNDCG(List<int> retrievedDocIds, Dictionary<int, double> relevanceScores, int k = 10);
    
    /// <summary>
    /// Calculate Hit Rate (Recall@K)
    /// Measures if at least one relevant document is in top K results
    /// </summary>
    /// <param name="retrievedDocIds">List of retrieved document IDs in rank order</param>
    /// <param name="relevantDocIds">Set of relevant document IDs</param>
    /// <param name="k">Consider only top k results</param>
    /// <returns>Hit rate (0.0 or 1.0)</returns>
    double CalculateHitRate(List<int> retrievedDocIds, HashSet<int> relevantDocIds, int k = 10);
    
    /// <summary>
    /// Calculate Precision@K
    /// Measures percentage of relevant documents in top K results
    /// </summary>
    /// <param name="retrievedDocIds">List of retrieved document IDs in rank order</param>
    /// <param name="relevantDocIds">Set of relevant document IDs</param>
    /// <param name="k">Consider only top k results</param>
    /// <returns>Precision score (0.0 to 1.0)</returns>
    double CalculatePrecisionAtK(List<int> retrievedDocIds, HashSet<int> relevantDocIds, int k = 10);
    
    /// <summary>
    /// Calculate Recall@K
    /// Measures percentage of relevant documents retrieved in top K results
    /// </summary>
    /// <param name="retrievedDocIds">List of retrieved document IDs in rank order</param>
    /// <param name="relevantDocIds">Set of relevant document IDs</param>
    /// <param name="k">Consider only top k results</param>
    /// <returns>Recall score (0.0 to 1.0)</returns>
    double CalculateRecallAtK(List<int> retrievedDocIds, HashSet<int> relevantDocIds, int k = 10);
    
    /// <summary>
    /// Evaluate retrieval quality on a golden dataset
    /// </summary>
    /// <param name="datasetId">Golden dataset identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Comprehensive retrieval evaluation result</returns>
    Task<RetrievalEvaluationResult> EvaluateRetrievalQualityAsync(
        string datasetId, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Compare retrieval quality between two configurations
    /// </summary>
    /// <param name="configurationA">First configuration name</param>
    /// <param name="configurationB">Second configuration name</param>
    /// <param name="datasetId">Test dataset identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A/B test comparison result</returns>
    Task<RetrievalABTestResult> CompareRetrievalConfigurationsAsync(
        string configurationA,
        string configurationB,
        string datasetId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of retrieval quality evaluation
/// </summary>
public class RetrievalEvaluationResult
{
    /// <summary>
    /// Mean Reciprocal Rank (average across all queries)
    /// </summary>
    public double MRR { get; set; }
    
    /// <summary>
    /// Normalized Discounted Cumulative Gain
    /// </summary>
    public double NDCG { get; set; }
    
    /// <summary>
    /// Hit Rate (at least one relevant doc in top K)
    /// </summary>
    public double HitRate { get; set; }
    
    /// <summary>
    /// Precision at K
    /// </summary>
    public double PrecisionAtK { get; set; }
    
    /// <summary>
    /// Recall at K
    /// </summary>
    public double RecallAtK { get; set; }
    
    /// <summary>
    /// Number of queries evaluated
    /// </summary>
    public int TotalQueries { get; set; }
    
    /// <summary>
    /// Per-query detailed metrics
    /// </summary>
    public Dictionary<string, QueryMetrics> PerQueryMetrics { get; set; } = new();
    
    /// <summary>
    /// Evaluation timestamp
    /// </summary>
    public DateTime EvaluatedAt { get; set; }
    
    /// <summary>
    /// Configuration used for retrieval
    /// </summary>
    public string ConfigurationName { get; set; } = string.Empty;
    
    /// <summary>
    /// K value used for @K metrics
    /// </summary>
    public int K { get; set; } = 10;
}

/// <summary>
/// Metrics for a single query
/// </summary>
public class QueryMetrics
{
    public string Query { get; set; } = string.Empty;
    public double MRR { get; set; }
    public double NDCG { get; set; }
    public double HitRate { get; set; }
    public double PrecisionAtK { get; set; }
    public double RecallAtK { get; set; }
    public int RelevantDocsRetrieved { get; set; }
    public int TotalRelevantDocs { get; set; }
}

/// <summary>
/// A/B test result for retrieval configurations
/// </summary>
public class RetrievalABTestResult
{
    public string ConfigurationA { get; set; } = string.Empty;
    public string ConfigurationB { get; set; } = string.Empty;
    public RetrievalEvaluationResult ResultsA { get; set; } = new();
    public RetrievalEvaluationResult ResultsB { get; set; } = new();
    public string Winner { get; set; } = string.Empty;
    public Dictionary<string, double> ImprovementPercentages { get; set; } = new();
    public bool IsStatisticallySignificant { get; set; }
}
