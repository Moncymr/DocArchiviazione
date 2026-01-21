namespace DocN.Data.Models;

/// <summary>
/// Predefined agent templates for quick setup
/// </summary>
public class AgentTemplate
{
    public int Id { get; set; }
    
    // Template Information
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = "🤖"; // Emoji or icon class
    public AgentType AgentType { get; set; }
    
    // Template Category
    public string Category { get; set; } = "General"; // e.g., "Legal", "HR", "Technical", etc.
    
    // Recommended Configuration
    public AIProviderType RecommendedProvider { get; set; }
    public string? RecommendedModel { get; set; }
    
    // Default System Prompt
    public string DefaultSystemPrompt { get; set; } = string.Empty;
    
    // Default Parameters (JSON)
    public string DefaultParametersJson { get; set; } = "{}";
    
    // Template Metadata
    public bool IsBuiltIn { get; set; } = true; // Built-in vs user-created
    public bool IsActive { get; set; } = true;
    public int UsageCount { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    
    // Owner (null for system templates)
    public string? OwnerId { get; set; }
    public virtual ApplicationUser? Owner { get; set; }
    
    // Template Examples and Documentation
    public string? ExampleQuery { get; set; }
    public string? ExampleResponse { get; set; }
    public string? ConfigurationGuide { get; set; }
    
    // Related agents created from this template
    public virtual ICollection<AgentConfiguration> Agents { get; set; } = new List<AgentConfiguration>();
}
