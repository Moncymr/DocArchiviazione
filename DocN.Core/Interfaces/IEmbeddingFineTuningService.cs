namespace DocN.Core.Interfaces;

/// <summary>
/// Service for fine-tuning embedding models on domain-specific documents
/// </summary>
public interface IEmbeddingFineTuningService
{
    /// <summary>
    /// Prepare training data from domain documents
    /// </summary>
    /// <param name="documentIds">Document IDs to use for training</param>
    /// <param name="outputPath">Path to save training data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Training data preparation result</returns>
    Task<TrainingDataResult> PrepareTrainingDataAsync(
        List<int> documentIds,
        string outputPath,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Generate contrastive pairs for fine-tuning
    /// Creates positive and negative examples for similarity learning
    /// </summary>
    /// <param name="documentIds">Source documents</param>
    /// <param name="numPairs">Number of pairs to generate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of contrastive pairs</returns>
    Task<List<ContrastivePair>> GenerateContrastivePairsAsync(
        List<int> documentIds,
        int numPairs = 1000,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Evaluate embedding model quality on test set
    /// </summary>
    /// <param name="testDatasetId">Test dataset identifier</param>
    /// <param name="modelName">Model to evaluate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Model evaluation metrics</returns>
    Task<EmbeddingEvaluationResult> EvaluateEmbeddingModelAsync(
        string testDatasetId,
        string modelName,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Create domain-specific embedding adapter
    /// Adapts base embedding model to domain-specific vocabulary and patterns
    /// </summary>
    /// <param name="baseModelName">Base embedding model</param>
    /// <param name="domainDocumentIds">Domain documents for adaptation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Adapter configuration</returns>
    Task<EmbeddingAdapterConfig> CreateDomainAdapterAsync(
        string baseModelName,
        List<int> domainDocumentIds,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of training data preparation
/// </summary>
public class TrainingDataResult
{
    /// <summary>
    /// Number of training examples generated
    /// </summary>
    public int TotalExamples { get; set; }
    
    /// <summary>
    /// Path to training data file
    /// </summary>
    public string OutputPath { get; set; } = string.Empty;
    
    /// <summary>
    /// Format of training data (jsonl, csv, etc.)
    /// </summary>
    public string Format { get; set; } = "jsonl";
    
    /// <summary>
    /// Statistics about the training data
    /// </summary>
    public Dictionary<string, int> Statistics { get; set; } = new();
    
    /// <summary>
    /// Warnings or issues encountered
    /// </summary>
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Contrastive pair for similarity learning
/// </summary>
public class ContrastivePair
{
    /// <summary>
    /// Anchor text (query or document)
    /// </summary>
    public string Anchor { get; set; } = string.Empty;
    
    /// <summary>
    /// Positive example (similar to anchor)
    /// </summary>
    public string Positive { get; set; } = string.Empty;
    
    /// <summary>
    /// Negative example (dissimilar to anchor)
    /// </summary>
    public string Negative { get; set; } = string.Empty;
    
    /// <summary>
    /// Metadata for the pair
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// Result of embedding model evaluation
/// </summary>
public class EmbeddingEvaluationResult
{
    /// <summary>
    /// Model being evaluated
    /// </summary>
    public string ModelName { get; set; } = string.Empty;
    
    /// <summary>
    /// Cosine similarity score on test set
    /// </summary>
    public double AverageSimilarityScore { get; set; }
    
    /// <summary>
    /// Retrieval accuracy (correct document in top K)
    /// </summary>
    public double RetrievalAccuracy { get; set; }
    
    /// <summary>
    /// Mean Average Precision
    /// </summary>
    public double MAP { get; set; }
    
    /// <summary>
    /// Embedding dimension
    /// </summary>
    public int EmbeddingDimension { get; set; }
    
    /// <summary>
    /// Inference time per embedding (milliseconds)
    /// </summary>
    public double AverageInferenceTimeMs { get; set; }
    
    /// <summary>
    /// Per-category performance breakdown
    /// </summary>
    public Dictionary<string, double> CategoryPerformance { get; set; } = new();
    
    /// <summary>
    /// Evaluation timestamp
    /// </summary>
    public DateTime EvaluatedAt { get; set; }
}

/// <summary>
/// Configuration for domain-specific embedding adapter
/// </summary>
public class EmbeddingAdapterConfig
{
    /// <summary>
    /// Base model name
    /// </summary>
    public string BaseModel { get; set; } = string.Empty;
    
    /// <summary>
    /// Adapter name/identifier
    /// </summary>
    public string AdapterName { get; set; } = string.Empty;
    
    /// <summary>
    /// Domain vocabulary (key terms and their embeddings)
    /// </summary>
    public Dictionary<string, float[]> DomainVocabulary { get; set; } = new();
    
    /// <summary>
    /// Domain-specific weight adjustments
    /// </summary>
    public Dictionary<string, double> WeightAdjustments { get; set; } = new();
    
    /// <summary>
    /// Number of documents used for adaptation
    /// </summary>
    public int TrainingDocumentCount { get; set; }
    
    /// <summary>
    /// When the adapter was created
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// Adapter configuration metadata
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}
