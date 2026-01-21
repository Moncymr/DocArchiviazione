using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocN.Data.Models;

/// <summary>
/// Represents a log entry for an ingestion execution
/// </summary>
public class IngestionLog
{
    [Key]
    public int Id { get; set; }
    
    /// <summary>
    /// Reference to the ingestion schedule
    /// </summary>
    [Required]
    public int IngestionScheduleId { get; set; }
    
    /// <summary>
    /// Navigation property for ingestion schedule
    /// </summary>
    [ForeignKey(nameof(IngestionScheduleId))]
    public virtual IngestionSchedule IngestionSchedule { get; set; } = null!;
    
    /// <summary>
    /// When the ingestion started
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// When the ingestion completed (or failed)
    /// </summary>
    public DateTime? CompletedAt { get; set; }
    
    /// <summary>
    /// Status: Running, Completed, Failed, Cancelled
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "Running";
    
    /// <summary>
    /// Number of documents discovered
    /// </summary>
    public int DocumentsDiscovered { get; set; } = 0;
    
    /// <summary>
    /// Number of documents successfully processed
    /// </summary>
    public int DocumentsProcessed { get; set; } = 0;
    
    /// <summary>
    /// Number of documents skipped (already exist, filtered out, etc.)
    /// </summary>
    public int DocumentsSkipped { get; set; } = 0;
    
    /// <summary>
    /// Number of documents that failed processing
    /// </summary>
    public int DocumentsFailed { get; set; } = 0;
    
    /// <summary>
    /// Error message if ingestion failed
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// Detailed log messages (JSON array)
    /// </summary>
    public string? DetailedLog { get; set; }
    
    /// <summary>
    /// Execution triggered manually or by schedule
    /// </summary>
    public bool IsManualExecution { get; set; } = false;
    
    /// <summary>
    /// User who triggered manual execution
    /// </summary>
    [MaxLength(450)]
    public string? TriggeredByUserId { get; set; }
    
    /// <summary>
    /// Duration of the ingestion in seconds
    /// </summary>
    public int? DurationSeconds { get; set; }
}
