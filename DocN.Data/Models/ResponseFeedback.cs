using System.ComponentModel.DataAnnotations;

namespace DocN.Data.Models;

/// <summary>
/// Stores user feedback on RAG query responses for quality improvement
/// </summary>
public class ResponseFeedback
{
    [Key]
    public int Id { get; set; }
    
    /// <summary>
    /// User who provided the feedback
    /// </summary>
    public string UserId { get; set; } = string.Empty;
    
    /// <summary>
    /// Original query text
    /// </summary>
    [Required]
    [MaxLength(2000)]
    public string Query { get; set; } = string.Empty;
    
    /// <summary>
    /// Generated response text
    /// </summary>
    [Required]
    public string Response { get; set; } = string.Empty;
    
    /// <summary>
    /// Confidence score (0-100)
    /// </summary>
    public double ConfidenceScore { get; set; }
    
    /// <summary>
    /// Was the response helpful? (true = thumbs up, false = thumbs down)
    /// </summary>
    public bool IsHelpful { get; set; }
    
    /// <summary>
    /// Optional user comment
    /// </summary>
    [MaxLength(1000)]
    public string? Comment { get; set; }
    
    /// <summary>
    /// Document IDs that were used as sources
    /// </summary>
    public string? SourceDocumentIds { get; set; }
    
    /// <summary>
    /// Chunk IDs that were used
    /// </summary>
    public string? SourceChunkIds { get; set; }
    
    /// <summary>
    /// When the feedback was submitted
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Tenant ID for multi-tenancy
    /// </summary>
    public int? TenantId { get; set; }
    
    // Navigation properties
    public virtual ApplicationUser? User { get; set; }
    public virtual Tenant? Tenant { get; set; }
}
