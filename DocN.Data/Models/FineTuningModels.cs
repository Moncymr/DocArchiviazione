namespace DocN.Data.Models;

/// <summary>
/// Represents a training example for embedding fine-tuning
/// </summary>
public class EmbeddingTrainingExample
{
    /// <summary>
    /// Unique identifier
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// Input text for training
    /// </summary>
    public string InputText { get; set; } = string.Empty;
    
    /// <summary>
    /// Positive example (similar/related text)
    /// </summary>
    public string PositiveExample { get; set; } = string.Empty;
    
    /// <summary>
    /// Negative example (dissimilar/unrelated text)
    /// </summary>
    public string? NegativeExample { get; set; }
    
    /// <summary>
    /// Source document ID
    /// </summary>
    public int? SourceDocumentId { get; set; }
    
    /// <summary>
    /// Related document ID (for positive example)
    /// </summary>
    public int? RelatedDocumentId { get; set; }
    
    /// <summary>
    /// Domain/category for this training example
    /// </summary>
    public string? Domain { get; set; }
    
    /// <summary>
    /// Quality score for this training pair (0.0-1.0)
    /// </summary>
    public double? QualityScore { get; set; }
    
    /// <summary>
    /// Creation timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Whether this example has been used in training
    /// </summary>
    public bool IsUsed { get; set; } = false;
}

/// <summary>
/// Represents a fine-tuning job for embedding models
/// </summary>
public class FineTuningJob
{
    /// <summary>
    /// Unique identifier
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// Job name/description
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Base model being fine-tuned
    /// </summary>
    public string BaseModel { get; set; } = string.Empty;
    
    /// <summary>
    /// Provider (OpenAI, Cohere, etc.)
    /// </summary>
    public string Provider { get; set; } = string.Empty;
    
    /// <summary>
    /// Status: Pending, Running, Completed, Failed
    /// </summary>
    public string Status { get; set; } = "Pending";
    
    /// <summary>
    /// External job ID from the provider
    /// </summary>
    public string? ExternalJobId { get; set; }
    
    /// <summary>
    /// Number of training examples used
    /// </summary>
    public int TrainingExamplesCount { get; set; }
    
    /// <summary>
    /// Fine-tuned model ID/name
    /// </summary>
    public string? FineTunedModelId { get; set; }
    
    /// <summary>
    /// Training configuration as JSON
    /// </summary>
    public string? ConfigurationJson { get; set; }
    
    /// <summary>
    /// Training metrics and results as JSON
    /// </summary>
    public string? MetricsJson { get; set; }
    
    /// <summary>
    /// Error message if job failed
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// When the job was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// When the job started
    /// </summary>
    public DateTime? StartedAt { get; set; }
    
    /// <summary>
    /// When the job completed
    /// </summary>
    public DateTime? CompletedAt { get; set; }
    
    /// <summary>
    /// Cost of the fine-tuning job
    /// </summary>
    public decimal? Cost { get; set; }
}

/// <summary>
/// Represents a fine-tuned model record
/// </summary>
public class FineTunedModel
{
    /// <summary>
    /// Unique identifier
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// Model name/identifier
    /// </summary>
    public string ModelId { get; set; } = string.Empty;
    
    /// <summary>
    /// Display name
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;
    
    /// <summary>
    /// Base model
    /// </summary>
    public string BaseModel { get; set; } = string.Empty;
    
    /// <summary>
    /// Provider
    /// </summary>
    public string Provider { get; set; } = string.Empty;
    
    /// <summary>
    /// Fine-tuning job ID that created this model
    /// </summary>
    public int? FineTuningJobId { get; set; }
    
    /// <summary>
    /// Whether this model is currently active/in use
    /// </summary>
    public bool IsActive { get; set; } = false;
    
    /// <summary>
    /// Domain/category this model specializes in
    /// </summary>
    public string? Domain { get; set; }
    
    /// <summary>
    /// Embedding dimension
    /// </summary>
    public int EmbeddingDimension { get; set; }
    
    /// <summary>
    /// Performance metrics as JSON
    /// </summary>
    public string? PerformanceMetricsJson { get; set; }
    
    /// <summary>
    /// When the model was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// When the model was last used
    /// </summary>
    public DateTime? LastUsedAt { get; set; }
}
