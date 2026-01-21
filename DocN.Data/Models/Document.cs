using System.ComponentModel.DataAnnotations.Schema;

namespace DocN.Data.Models;

public class Document
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string ExtractedText { get; set; } = string.Empty;
    
    // Category suggested by AI with reasoning
    public string? SuggestedCategory { get; set; }
    public string? CategoryReasoning { get; set; } // NEW: Explains why AI suggested this category
    public string? ActualCategory { get; set; }
    
    // AI Tag Analysis Results
    public string? AITagsJson { get; set; } // JSON array of AI-detected tags with confidence scores
    public DateTime? AIAnalysisDate { get; set; }
    
    // AI Extracted Metadata (invoices, contracts, etc.)
    public string? ExtractedMetadataJson { get; set; } // JSON object with extracted structured data
    
    // Document metadata from processing
    public int? PageCount { get; set; }
    public string? DetectedLanguage { get; set; }
    
    /// <summary>
    /// Legacy processing status field - kept for backward compatibility
    /// Use WorkflowState for new implementations
    /// </summary>
    public string? ProcessingStatus { get; set; } // "Pending", "Processing", "Completed", "Failed"
    
    /// <summary>
    /// Legacy processing error field - kept for backward compatibility
    /// Use ErrorDetailsJson for structured error information
    /// </summary>
    public string? ProcessingError { get; set; }
    
    public string? ChunkEmbeddingStatus { get; set; } // "Pending", "Processing", "Completed", "NotRequired"
    
    // Enhanced workflow state management
    /// <summary>
    /// Current workflow state using enhanced state management
    /// See DocumentProcessingState constants for valid values
    /// </summary>
    public string? WorkflowState { get; set; }
    
    /// <summary>
    /// Previous workflow state (for rollback and debugging)
    /// </summary>
    public string? PreviousWorkflowState { get; set; }
    
    /// <summary>
    /// When the current state was entered
    /// </summary>
    public DateTime? StateEnteredAt { get; set; }
    
    /// <summary>
    /// Number of times processing has been retried
    /// </summary>
    public int RetryCount { get; set; } = 0;
    
    /// <summary>
    /// Maximum number of retries before marking as permanent failure
    /// </summary>
    public int MaxRetries { get; set; } = 3;
    
    /// <summary>
    /// When the last retry occurred
    /// </summary>
    public DateTime? LastRetryAt { get; set; }
    
    /// <summary>
    /// Next scheduled retry time (for exponential backoff)
    /// </summary>
    public DateTime? NextRetryAt { get; set; }
    
    /// <summary>
    /// Structured error information in JSON format
    /// Contains: errorType, errorMessage, stackTrace, timestamp, context
    /// </summary>
    public string? ErrorDetailsJson { get; set; }
    
    /// <summary>
    /// User-friendly error message
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// Type of error (see DocumentErrorType constants)
    /// </summary>
    public string? ErrorType { get; set; }
    
    /// <summary>
    /// Whether the error is transient and retryable
    /// </summary>
    public bool IsRetryable { get; set; } = false;
    
    // User notes
    public string? Notes { get; set; }
    
    // Visibility management
    public DocumentVisibility Visibility { get; set; } = DocumentVisibility.Private;
    
    // Connector Source Tracking (for ingestion deduplication)
    /// <summary>
    /// ID of the connector used to ingest this document (if any)
    /// </summary>
    public int? SourceConnectorId { get; set; }
    
    /// <summary>
    /// Original file path in the external source
    /// </summary>
    public string? SourceFilePath { get; set; }
    
    /// <summary>
    /// Hash of the file content for change detection
    /// </summary>
    public string? SourceFileHash { get; set; }
    
    /// <summary>
    /// Last modified date in the source system
    /// </summary>
    public DateTime? SourceLastModified { get; set; }
    
    // Vector embeddings for semantic search - separate fields for different dimensions
    // Using native SQL Server VECTOR type for optimal performance
    
    /// <summary>
    /// 768-dimensional embedding vector (for Gemini and similar providers)
    /// Stored as VECTOR(768) in SQL Server 2025
    /// </summary>
    public float[]? EmbeddingVector768 { get; set; }
    
    /// <summary>
    /// 1536-dimensional embedding vector (for OpenAI ada-002 and similar providers)
    /// Stored as VECTOR(1536) in SQL Server 2025
    /// </summary>
    public float[]? EmbeddingVector1536 { get; set; }
    
    /// <summary>
    /// The actual dimension of the embedding vector stored.
    /// Indicates which vector field is populated: 768 or 1536
    /// </summary>
    public int? EmbeddingDimension { get; set; }
    
    /// <summary>
    /// Unified property for backward compatibility - returns the populated vector field
    /// Gets/sets the appropriate field based on dimension
    /// </summary>
    public float[]? EmbeddingVector
    {
        get
        {
            return EmbeddingVector768 ?? EmbeddingVector1536;
        }
        set
        {
            if (value == null)
            {
                EmbeddingVector768 = null;
                EmbeddingVector1536 = null;
                EmbeddingDimension = null;
            }
            else if (value.Length == Utilities.EmbeddingValidationHelper.SupportedDimension768)
            {
                EmbeddingVector768 = value;
                EmbeddingVector1536 = null;
                EmbeddingDimension = Utilities.EmbeddingValidationHelper.SupportedDimension768;
            }
            else if (value.Length == Utilities.EmbeddingValidationHelper.SupportedDimension1536)
            {
                EmbeddingVector768 = null;
                EmbeddingVector1536 = value;
                EmbeddingDimension = Utilities.EmbeddingValidationHelper.SupportedDimension1536;
            }
            else
            {
                throw new ArgumentException(
                    $"Unsupported embedding dimension: {value.Length}. " +
                    $"Expected {Utilities.EmbeddingValidationHelper.SupportedDimension768} or " +
                    $"{Utilities.EmbeddingValidationHelper.SupportedDimension1536}.");
            }
        }
    }
    
    // Metadata
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastAccessedAt { get; set; }
    public int AccessCount { get; set; } = 0;
    
    // Owner (nullable until authentication is fully implemented)
    // Note: ApplicationUser is in ApplicationDbContext, not DocArcContext
    public string? OwnerId { get; set; }
    
    /// <summary>
    /// Navigation property to owner - not mapped in DocArcContext
    /// </summary>
    [NotMapped]
    public virtual ApplicationUser? Owner { get; set; }
    
    // Multi-tenant support
    public int? TenantId { get; set; }
    
    /// <summary>
    /// Navigation property to tenant - not mapped in DocArcContext
    /// </summary>
    [NotMapped]
    public virtual Tenant? Tenant { get; set; }
    
    // Document sharing
    /// <summary>
    /// Navigation property for individual user shares - not mapped in DocArcContext
    /// </summary>
    [NotMapped]
    public virtual ICollection<DocumentShare> Shares { get; set; } = new List<DocumentShare>();
    
    /// <summary>
    /// Navigation property for group shares - not mapped in DocArcContext
    /// </summary>
    [NotMapped]
    public virtual ICollection<DocumentGroupShare> GroupShares { get; set; } = new List<DocumentGroupShare>();
    
    // Tags
    /// <summary>
    /// Navigation property for tags - not mapped in DocArcContext
    /// </summary>
    [NotMapped]
    public virtual ICollection<DocumentTag> Tags { get; set; } = new List<DocumentTag>();
}

public enum DocumentVisibility
{
    Private = 0,      // Only owner can see
    Shared = 1,       // Shared with specific users
    Organization = 2, // All organization members
    Public = 3        // Everyone can see
}
