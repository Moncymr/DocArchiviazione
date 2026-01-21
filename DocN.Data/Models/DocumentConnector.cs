using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocN.Data.Models;

/// <summary>
/// Represents a connection to an external document repository
/// (SharePoint, OneDrive, Google Drive, etc.)
/// </summary>
public class DocumentConnector
{
    [Key]
    public int Id { get; set; }
    
    /// <summary>
    /// Name of the connector (user-defined)
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Type of connector: SharePoint, OneDrive, GoogleDrive, etc.
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string ConnectorType { get; set; } = string.Empty;
    
    /// <summary>
    /// Connection configuration (JSON serialized)
    /// Contains endpoint URLs, folder paths, etc.
    /// </summary>
    [Required]
    public string Configuration { get; set; } = string.Empty;
    
    /// <summary>
    /// Encrypted credentials (JSON serialized)
    /// Contains OAuth tokens, API keys, etc.
    /// </summary>
    public string? EncryptedCredentials { get; set; }
    
    /// <summary>
    /// Whether the connector is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// Last successful connection test timestamp
    /// </summary>
    public DateTime? LastConnectionTest { get; set; }
    
    /// <summary>
    /// Result of last connection test
    /// </summary>
    [MaxLength(500)]
    public string? LastConnectionTestResult { get; set; }
    
    /// <summary>
    /// Last successful sync timestamp
    /// </summary>
    public DateTime? LastSyncedAt { get; set; }
    
    /// <summary>
    /// Owner of the connector
    /// </summary>
    [MaxLength(450)]
    public string? OwnerId { get; set; }
    
    /// <summary>
    /// Tenant ID for multi-tenancy support
    /// </summary>
    public int? TenantId { get; set; }
    
    /// <summary>
    /// When the connector was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// When the connector was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Optional description
    /// </summary>
    [MaxLength(1000)]
    public string? Description { get; set; }
    
    /// <summary>
    /// Navigation property for associated ingestion schedules
    /// </summary>
    public virtual ICollection<IngestionSchedule> IngestionSchedules { get; set; } = new List<IngestionSchedule>();
    
    /// <summary>
    /// Navigation property for tenant
    /// Note: Tenant entity is in ApplicationDbContext, not DocArcContext
    /// This property is not mapped to avoid cross-context relationship issues
    /// </summary>
    [ForeignKey(nameof(TenantId))]
    [NotMapped]
    public virtual Tenant? Tenant { get; set; }
}
