namespace DocN.Core.Interfaces;

/// <summary>
/// Service for fine-tuning embedding models on domain-specific documents
/// </summary>
public interface IFineTuningService
{
    /// <summary>
    /// Generate training examples from existing documents
    /// </summary>
    Task<List<TrainingExample>> GenerateTrainingExamplesAsync(
        IEnumerable<int> documentIds,
        int maxExamples = 1000,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Create a fine-tuning job
    /// </summary>
    Task<FineTuningJobResult> CreateFineTuningJobAsync(
        string baseModel,
        IEnumerable<TrainingExample> trainingExamples,
        FineTuningConfiguration? configuration = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check the status of a fine-tuning job
    /// </summary>
    Task<FineTuningJobStatus> GetJobStatusAsync(
        string jobId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// List all fine-tuned models
    /// </summary>
    Task<List<FineTunedModelInfo>> ListFineTunedModelsAsync(
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Activate a fine-tuned model for use
    /// </summary>
    Task<bool> ActivateModelAsync(
        string modelId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Export training data in the format required by the provider
    /// </summary>
    Task<string> ExportTrainingDataAsync(
        IEnumerable<TrainingExample> examples,
        string provider = "OpenAI",
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Training example for embedding fine-tuning
/// </summary>
public class TrainingExample
{
    public string InputText { get; set; } = string.Empty;
    public string PositiveExample { get; set; } = string.Empty;
    public string? NegativeExample { get; set; }
    public string? Domain { get; set; }
    public double QualityScore { get; set; }
}

/// <summary>
/// Configuration for fine-tuning
/// </summary>
public class FineTuningConfiguration
{
    public string Provider { get; set; } = "OpenAI";
    public int Epochs { get; set; } = 3;
    public double LearningRate { get; set; } = 0.0001;
    public int BatchSize { get; set; } = 32;
    public string? ValidationSplit { get; set; }
    public Dictionary<string, object> AdditionalParameters { get; set; } = new();
}

/// <summary>
/// Result of creating a fine-tuning job
/// </summary>
public class FineTuningJobResult
{
    public string JobId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Message { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Status of a fine-tuning job
/// </summary>
public class FineTuningJobStatus
{
    public string JobId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? FineTunedModel { get; set; }
    public Dictionary<string, double>? Metrics { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// Information about a fine-tuned model
/// </summary>
public class FineTunedModelInfo
{
    public string ModelId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string BaseModel { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public Dictionary<string, double>? PerformanceMetrics { get; set; }
}
