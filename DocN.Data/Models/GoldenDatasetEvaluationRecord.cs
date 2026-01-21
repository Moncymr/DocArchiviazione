namespace DocN.Data.Models;

/// <summary>
/// Stores the results of evaluating a golden dataset at a point in time
/// Used for tracking quality trends and regression detection
/// </summary>
public class GoldenDatasetEvaluationRecord
{
    public int Id { get; set; }
    
    /// <summary>
    /// Reference to the golden dataset that was evaluated
    /// </summary>
    public int GoldenDatasetId { get; set; }
    public virtual GoldenDataset GoldenDataset { get; set; } = null!;
    
    /// <summary>
    /// When this evaluation was performed
    /// </summary>
    public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Configuration identifier used during evaluation (e.g., "production-v1", "experimental-reranking")
    /// </summary>
    public string? ConfigurationId { get; set; }
    
    /// <summary>
    /// Total number of samples in the dataset at evaluation time
    /// </summary>
    public int TotalSamples { get; set; }
    
    /// <summary>
    /// Number of samples successfully evaluated
    /// </summary>
    public int EvaluatedSamples { get; set; }
    
    /// <summary>
    /// Number of samples that failed evaluation
    /// </summary>
    public int FailedSamples { get; set; }
    
    /// <summary>
    /// Average RAGAS faithfulness score across all samples
    /// </summary>
    public double AverageFaithfulnessScore { get; set; }
    
    /// <summary>
    /// Average RAGAS answer relevancy score
    /// </summary>
    public double AverageAnswerRelevancyScore { get; set; }
    
    /// <summary>
    /// Average RAGAS context precision score
    /// </summary>
    public double AverageContextPrecisionScore { get; set; }
    
    /// <summary>
    /// Average RAGAS context recall score
    /// </summary>
    public double AverageContextRecallScore { get; set; }
    
    /// <summary>
    /// Overall RAGAS score (harmonic mean of all metrics)
    /// </summary>
    public double OverallRAGASScore { get; set; }
    
    /// <summary>
    /// Average confidence score from RAG quality checks
    /// </summary>
    public double AverageConfidenceScore { get; set; }
    
    /// <summary>
    /// Percentage of responses with low confidence warnings
    /// </summary>
    public double LowConfidenceRate { get; set; }
    
    /// <summary>
    /// Percentage of responses with detected hallucinations
    /// </summary>
    public double HallucinationRate { get; set; }
    
    /// <summary>
    /// Average citation verification rate
    /// </summary>
    public double CitationVerificationRate { get; set; }
    
    /// <summary>
    /// JSON object with per-sample detailed results
    /// </summary>
    public string? DetailedResultsJson { get; set; }
    
    /// <summary>
    /// JSON array of sample IDs that failed
    /// </summary>
    public string? FailedSampleIdsJson { get; set; }
    
    /// <summary>
    /// Overall evaluation status: "success", "partial", "failed"
    /// </summary>
    public string Status { get; set; } = "success";
    
    /// <summary>
    /// Notes or error messages from the evaluation
    /// </summary>
    public string? Notes { get; set; }
    
    /// <summary>
    /// Time taken to complete the evaluation (in seconds)
    /// </summary>
    public double DurationSeconds { get; set; }
    
    /// <summary>
    /// Multi-tenant support
    /// </summary>
    public int? TenantId { get; set; }
    public virtual Tenant? Tenant { get; set; }
}
