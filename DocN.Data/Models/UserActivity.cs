namespace DocN.Data.Models;

/// <summary>
/// Represents a user activity entry for the activity feed
/// </summary>
public class UserActivity
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string ActivityType { get; set; } = string.Empty; // DocumentUploaded, DocumentViewed, SearchPerformed, etc.
    public string Description { get; set; } = string.Empty;
    public int? DocumentId { get; set; }
    public string? Metadata { get; set; } // JSON metadata
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public virtual ApplicationUser? User { get; set; }
    public virtual Document? Document { get; set; }
}
