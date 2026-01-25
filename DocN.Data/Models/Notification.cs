namespace DocN.Data.Models;

/// <summary>
/// Represents a user notification for real-time updates
/// </summary>
public class Notification
{
    public int Id { get; set; }
    
    /// <summary>
    /// User who receives this notification
    /// </summary>
    public string UserId { get; set; } = string.Empty;
    
    /// <summary>
    /// Type of notification (document_processed, comment, mention, system_alert, task_completed)
    /// </summary>
    public string Type { get; set; } = string.Empty;
    
    /// <summary>
    /// Short title of the notification
    /// </summary>
    public string Title { get; set; } = string.Empty;
    
    /// <summary>
    /// Detailed message content
    /// </summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary>
    /// Optional link/action associated with the notification
    /// </summary>
    public string? Link { get; set; }
    
    /// <summary>
    /// Icon identifier for UI display (document, comment, mention, alert, check)
    /// </summary>
    public string Icon { get; set; } = "info";
    
    /// <summary>
    /// Whether the notification has been read
    /// </summary>
    public bool IsRead { get; set; } = false;
    
    /// <summary>
    /// Whether this is an important/priority notification
    /// </summary>
    public bool IsImportant { get; set; } = false;
    
    /// <summary>
    /// When the notification was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// When the notification was read (if applicable)
    /// </summary>
    public DateTime? ReadAt { get; set; }
    
    /// <summary>
    /// Optional metadata as JSON for extensibility
    /// </summary>
    public string? Metadata { get; set; }
    
    // Navigation properties
    public virtual ApplicationUser? User { get; set; }
}

/// <summary>
/// Notification types constants
/// </summary>
public static class NotificationTypes
{
    public const string DocumentProcessed = "document_processed";
    public const string NewComment = "new_comment";
    public const string Mention = "mention";
    public const string SystemAlert = "system_alert";
    public const string TaskCompleted = "task_completed";
}

/// <summary>
/// User notification preferences
/// </summary>
public class NotificationPreference
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    
    // Notification type toggles
    public bool EnableDocumentProcessed { get; set; } = true;
    public bool EnableComments { get; set; } = true;
    public bool EnableMentions { get; set; } = true;
    public bool EnableSystemAlerts { get; set; } = true;
    public bool EnableTaskCompleted { get; set; } = true;
    
    // Sound and desktop notifications
    public bool EnableSound { get; set; } = true;
    public bool EnableDesktopNotifications { get; set; } = false;
    
    // Email digest settings
    public string EmailDigestFrequency { get; set; } = "none"; // none, daily, weekly
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    
    // Navigation properties
    public virtual ApplicationUser? User { get; set; }
}
