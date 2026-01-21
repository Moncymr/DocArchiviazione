namespace DocN.Data.Models;

/// <summary>
/// Represents a single test sample in a golden dataset for RAG quality regression testing
/// </summary>
public class GoldenDatasetSample
{
    public int Id { get; set; }
    
    /// <summary>
    /// Reference to the parent golden dataset
    /// </summary>
    public int GoldenDatasetId { get; set; }
    public virtual GoldenDataset GoldenDataset { get; set; } = null!;
    
    /// <summary>
    /// The query/question to test
    /// </summary>
    public string Query { get; set; } = string.Empty;
    
    /// <summary>
    /// The expected/ideal response (ground truth)
    /// </summary>
    public string GroundTruth { get; set; } = string.Empty;
    
    /// <summary>
    /// JSON array of document IDs that should be retrieved for this query
    /// </summary>
    public string? RelevantDocumentIdsJson { get; set; }
    
    /// <summary>
    /// Expected response text (optional, for comparison)
    /// </summary>
    public string? ExpectedResponse { get; set; }
    
    /// <summary>
    /// Category/tag for organizing samples (e.g., "document-upload", "search", "metadata")
    /// </summary>
    public string? Category { get; set; }
    
    /// <summary>
    /// Difficulty level: "easy", "medium", "hard"
    /// </summary>
    public string DifficultyLevel { get; set; } = "medium";
    
    /// <summary>
    /// Importance weight (1-10, default 5)
    /// Higher weight means this sample is more critical to pass
    /// </summary>
    public int ImportanceWeight { get; set; } = 5;
    
    /// <summary>
    /// Notes about this sample or why it was added
    /// </summary>
    public string? Notes { get; set; }
    
    /// <summary>
    /// When this sample was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Who created this sample
    /// </summary>
    public string? CreatedBy { get; set; }
    
    /// <summary>
    /// Whether this sample is currently active in testing
    /// </summary>
    public bool IsActive { get; set; } = true;
}
