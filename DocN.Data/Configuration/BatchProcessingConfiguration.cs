namespace DocN.Data.Configuration;

/// <summary>
/// Configuration for batch processing and workflow management
/// </summary>
public class BatchProcessingConfiguration
{
    /// <summary>
    /// Maximum number of documents to process in a single batch
    /// </summary>
    public int MaxBatchSize { get; set; } = 10;

    /// <summary>
    /// Maximum number of concurrent operations
    /// </summary>
    public int MaxConcurrency { get; set; } = 3;

    /// <summary>
    /// Processing interval in seconds
    /// </summary>
    public int ProcessingIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum number of documents to retry in a single cycle
    /// </summary>
    public int MaxRetryBatchSize { get; set; } = 5;

    /// <summary>
    /// Circuit breaker failure threshold before opening
    /// </summary>
    public int CircuitBreakerFailureThreshold { get; set; } = 5;

    /// <summary>
    /// Circuit breaker open duration in seconds
    /// </summary>
    public int CircuitBreakerOpenDurationSeconds { get; set; } = 60;

    /// <summary>
    /// Enable retry queue processing
    /// </summary>
    public bool EnableRetryQueue { get; set; } = true;

    /// <summary>
    /// Enable detailed logging for batch operations
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = false;
}
