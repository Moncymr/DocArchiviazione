namespace DocN.Data.Models;

/// <summary>
/// Represents a customizable widget for the dashboard
/// </summary>
public class DashboardWidget
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string WidgetType { get; set; } = string.Empty; // RecentDocuments, ActivityFeed, Statistics, SavedSearches
    public string Title { get; set; } = string.Empty;
    public int Position { get; set; } // For ordering
    public string? Configuration { get; set; } // JSON configuration
    public bool IsVisible { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    
    public virtual ApplicationUser? User { get; set; }
}
