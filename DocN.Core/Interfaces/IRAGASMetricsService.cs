namespace DocN.Core.Interfaces;

/// <summary>
/// Service for RAGAS (RAG Assessment) metrics evaluation
/// </summary>
public interface IRAGASMetricsService
{
    /// <summary>
    /// Evaluate RAG response using RAGAS metrics
    /// </summary>
    Task<RAGASEvaluationResult> EvaluateResponseAsync(
        string query,
        string response,
        IEnumerable<string> contexts,
        string? groundTruth = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Calculate faithfulness score (response based on given context)
    /// </summary>
    Task<double> CalculateFaithfulnessAsync(
        string response,
        IEnumerable<string> contexts,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Calculate answer relevancy score (response relevant to query)
    /// </summary>
    Task<double> CalculateAnswerRelevancyAsync(
        string query,
        string response,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Calculate context precision (relevant context retrieved)
    /// </summary>
    Task<double> CalculateContextPrecisionAsync(
        string query,
        IEnumerable<string> contexts,
        string? groundTruth = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Calculate context recall (all relevant context retrieved)
    /// </summary>
    Task<double> CalculateContextRecallAsync(
        IEnumerable<string> contexts,
        string? groundTruth = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Run automated evaluation on golden dataset
    /// </summary>
    Task<GoldenDatasetEvaluationResult> EvaluateGoldenDatasetAsync(
        string datasetId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get continuous monitoring metrics
    /// </summary>
    Task<ContinuousMonitoringMetrics> GetMonitoringMetricsAsync(
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Compare two RAG configurations (A/B testing)
    /// </summary>
    Task<ABTestResult> CompareConfigurationsAsync(
        string configurationA,
        string configurationB,
        string testDatasetId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// RAGAS evaluation result
/// </summary>
public class RAGASEvaluationResult
{
    public double FaithfulnessScore { get; set; }
    public double AnswerRelevancyScore { get; set; }
    public double ContextPrecisionScore { get; set; }
    public double ContextRecallScore { get; set; }
    public double OverallRAGASScore { get; set; }
    public Dictionary<string, double> DetailedMetrics { get; set; } = new();
    public List<string> Insights { get; set; } = new();
}

/// <summary>
/// Golden dataset evaluation result
/// </summary>
public class GoldenDatasetEvaluationResult
{
    public string DatasetId { get; set; } = string.Empty;
    public int TotalSamples { get; set; }
    public int EvaluatedSamples { get; set; }
    public RAGASEvaluationResult AverageScores { get; set; } = new();
    public Dictionary<string, RAGASEvaluationResult> PerSampleScores { get; set; } = new();
    public List<string> FailedSamples { get; set; } = new();
    public DateTime EvaluatedAt { get; set; }
}

/// <summary>
/// Continuous monitoring metrics
/// </summary>
public class ContinuousMonitoringMetrics
{
    public int TotalEvaluations { get; set; }
    public RAGASEvaluationResult AverageScores { get; set; } = new();
    public Dictionary<DateTime, RAGASEvaluationResult> TrendData { get; set; } = new();
    public List<QualityDegradationAlert> QualityAlerts { get; set; } = new();
    public double QualityTrend { get; set; } // Positive = improving, Negative = degrading
}

/// <summary>
/// Quality degradation alert
/// </summary>
public class QualityDegradationAlert
{
    public string MetricName { get; set; } = string.Empty;
    public double CurrentValue { get; set; }
    public double Threshold { get; set; }
    public double PreviousValue { get; set; }
    public DateTime DetectedAt { get; set; }
    public string Severity { get; set; } = string.Empty;
}

/// <summary>
/// A/B test result
/// </summary>
public class ABTestResult
{
    public string ConfigurationA { get; set; } = string.Empty;
    public string ConfigurationB { get; set; } = string.Empty;
    public RAGASEvaluationResult ScoresA { get; set; } = new();
    public RAGASEvaluationResult ScoresB { get; set; } = new();
    public string Winner { get; set; } = string.Empty;
    public Dictionary<string, double> ImprovementPercentages { get; set; } = new();
    public bool IsStatisticallySignificant { get; set; }
    public int SampleSize { get; set; }
}
