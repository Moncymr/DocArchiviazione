using System.ComponentModel.DataAnnotations;

namespace DocN.Data.Models;

/// <summary>
/// Represents an audit log entry for compliance tracking (GDPR/SOC2)
/// </summary>
public class AuditLog
{
    [Key]
    public long Id { get; set; }
    
    /// <summary>
    /// User ID who performed the action
    /// </summary>
    public string? UserId { get; set; }
    
    /// <summary>
    /// Username for quick reference
    /// </summary>
    [MaxLength(256)]
    public string? Username { get; set; }
    
    /// <summary>
    /// Action performed (e.g., "DocumentUploaded", "DocumentViewed", "UserLogin")
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Action { get; set; } = string.Empty;
    
    /// <summary>
    /// Type of resource affected (e.g., "Document", "Configuration", "User")
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string ResourceType { get; set; } = string.Empty;
    
    /// <summary>
    /// ID of the resource affected
    /// </summary>
    [MaxLength(100)]
    public string? ResourceId { get; set; }
    
    /// <summary>
    /// Additional details in JSON format
    /// </summary>
    public string? Details { get; set; }
    
    /// <summary>
    /// IP address of the client
    /// </summary>
    [MaxLength(45)] // IPv6 max length
    public string? IpAddress { get; set; }
    
    /// <summary>
    /// User agent string
    /// </summary>
    [MaxLength(500)]
    public string? UserAgent { get; set; }
    
    /// <summary>
    /// Tenant ID for multi-tenancy
    /// </summary>
    public int? TenantId { get; set; }
    
    /// <summary>
    /// Timestamp of the action (UTC)
    /// </summary>
    [Required]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Severity level: Info, Warning, Error, Critical
    /// </summary>
    [MaxLength(20)]
    public string Severity { get; set; } = "Info";
    
    /// <summary>
    /// Whether the action was successful
    /// </summary>
    public bool Success { get; set; } = true;
    
    /// <summary>
    /// Error message if action failed
    /// </summary>
    [MaxLength(1000)]
    public string? ErrorMessage { get; set; }
    
    // Navigation properties
    public virtual ApplicationUser? User { get; set; }
    public virtual Tenant? Tenant { get; set; }
}
