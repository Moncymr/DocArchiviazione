using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocN.Data.Models;

/// <summary>
/// Represents a scheduled or manual ingestion task
/// </summary>
public class IngestionSchedule
{
    [Key]
    public int Id { get; set; }
    
    /// <summary>
    /// Reference to the connector
    /// </summary>
    [Required]
    public int ConnectorId { get; set; }
    
    /// <summary>
    /// Navigation property for connector
    /// </summary>
    [ForeignKey(nameof(ConnectorId))]
    public virtual DocumentConnector? Connector { get; set; }
    
    /// <summary>
    /// User-defined name for the schedule
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Type of schedule: Manual, Scheduled, Continuous
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string ScheduleType { get; set; } = "Manual";
    
    /// <summary>
    /// Cron expression for scheduled ingestion (if ScheduleType = Scheduled)
    /// Example: "0 0 * * *" for daily at midnight
    /// </summary>
    [MaxLength(100)]
    public string? CronExpression { get; set; }
    
    /// <summary>
    /// Interval in minutes for continuous ingestion (if ScheduleType = Continuous)
    /// </summary>
    public int? IntervalMinutes { get; set; }
    
    /// <summary>
    /// Whether the schedule is currently active
    /// </summary>
    public bool IsEnabled { get; set; } = true;
    
    /// <summary>
    /// Default category to assign to ingested documents
    /// </summary>
    [MaxLength(255)]
    public string? DefaultCategory { get; set; }
    
    /// <summary>
    /// Default visibility level for ingested documents
    /// </summary>
    public DocumentVisibility DefaultVisibility { get; set; } = DocumentVisibility.Private;
    
    /// <summary>
    /// Filter configuration (JSON serialized)
    /// Contains file type filters, path patterns, date ranges, etc.
    /// </summary>
    public string? FilterConfiguration { get; set; }
    
    /// <summary>
    /// Whether to generate embeddings immediately or in background
    /// </summary>
    public bool GenerateEmbeddingsImmediately { get; set; } = false;
    
    /// <summary>
    /// Whether to enable AI analysis (category, tags, metadata extraction)
    /// </summary>
    public bool EnableAIAnalysis { get; set; } = true;
    
    /// <summary>
    /// Last time the schedule was executed
    /// </summary>
    public DateTime? LastExecutedAt { get; set; }
    
    /// <summary>
    /// Next scheduled execution time
    /// </summary>
    public DateTime? NextExecutionAt { get; set; }
    
    /// <summary>
    /// Number of documents processed in last execution
    /// </summary>
    public int LastExecutionDocumentCount { get; set; } = 0;
    
    /// <summary>
    /// Status of last execution
    /// </summary>
    [MaxLength(50)]
    public string? LastExecutionStatus { get; set; }
    
    /// <summary>
    /// Owner of the schedule
    /// </summary>
    [MaxLength(450)]
    public string? OwnerId { get; set; }
    
    /// <summary>
    /// When the schedule was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// When the schedule was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Optional description
    /// </summary>
    [MaxLength(1000)]
    public string? Description { get; set; }
    
    /// <summary>
    /// Navigation property for ingestion logs
    /// </summary>
    public virtual ICollection<IngestionLog> IngestionLogs { get; set; } = new List<IngestionLog>();
}
