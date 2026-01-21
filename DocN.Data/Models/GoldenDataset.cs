namespace DocN.Data.Models;

/// <summary>
/// Represents a collection of test samples for RAG quality regression testing
/// </summary>
public class GoldenDataset
{
    public int Id { get; set; }
    
    /// <summary>
    /// Unique identifier for this dataset (e.g., "general-qa-v1", "technical-docs-v2")
    /// </summary>
    public string DatasetId { get; set; } = string.Empty;
    
    /// <summary>
    /// Display name for this dataset
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Description of what this dataset tests
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Version number (e.g., "1.0", "2.1")
    /// </summary>
    public string Version { get; set; } = "1.0";
    
    /// <summary>
    /// Multi-tenant support - which tenant owns this dataset
    /// </summary>
    public int? TenantId { get; set; }
    public virtual Tenant? Tenant { get; set; }
    
    /// <summary>
    /// Who created this dataset
    /// </summary>
    public string? CreatedBy { get; set; }
    
    /// <summary>
    /// When this dataset was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// When this dataset was last updated
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
    
    /// <summary>
    /// Whether this dataset is currently active for testing
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// JSON metadata for additional configuration
    /// </summary>
    public string? MetadataJson { get; set; }
    
    /// <summary>
    /// The samples in this dataset
    /// </summary>
    public virtual ICollection<GoldenDatasetSample> Samples { get; set; } = new List<GoldenDatasetSample>();
    
    /// <summary>
    /// Evaluation results for this dataset
    /// </summary>
    public virtual ICollection<GoldenDatasetEvaluationRecord> EvaluationRecords { get; set; } = new List<GoldenDatasetEvaluationRecord>();
}
