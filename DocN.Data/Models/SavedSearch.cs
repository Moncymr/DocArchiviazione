namespace DocN.Data.Models;

/// <summary>
/// Represents a saved search query with filters
/// </summary>
public class SavedSearch
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public string? Filters { get; set; } // JSON filters (category, date range, etc.)
    public string SearchType { get; set; } = "hybrid"; // hybrid, vector, text
    public bool IsDefault { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAt { get; set; }
    public int UseCount { get; set; } = 0;
    
    public virtual ApplicationUser? User { get; set; }
}
