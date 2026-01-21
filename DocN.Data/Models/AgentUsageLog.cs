namespace DocN.Data.Models;

/// <summary>
/// Tracks usage and performance of agents
/// </summary>
public class AgentUsageLog
{
    public int Id { get; set; }
    
    // Agent Reference
    public int AgentConfigurationId { get; set; }
    public virtual AgentConfiguration AgentConfiguration { get; set; } = null!;
    
    // Query Information
    public string Query { get; set; } = string.Empty;
    public string? Response { get; set; }
    
    // Performance Metrics
    public int DocumentsRetrieved { get; set; }
    
    // TimeSpan stored as Ticks for SQL compatibility
    public long RetrievalTimeTicks { get; set; }
    public long SynthesisTimeTicks { get; set; }
    public long TotalTimeTicks { get; set; }
    
    // Computed properties for easy access
    public TimeSpan RetrievalTime
    {
        get => TimeSpan.FromTicks(RetrievalTimeTicks);
        set => RetrievalTimeTicks = value.Ticks;
    }
    
    public TimeSpan SynthesisTime
    {
        get => TimeSpan.FromTicks(SynthesisTimeTicks);
        set => SynthesisTimeTicks = value.Ticks;
    }
    
    public TimeSpan TotalTime
    {
        get => TimeSpan.FromTicks(TotalTimeTicks);
        set => TotalTimeTicks = value.Ticks;
    }
    
    // Token Usage
    public int? PromptTokens { get; set; }
    public int? CompletionTokens { get; set; }
    public int? TotalTokens { get; set; }
    
    // Provider Used
    public AIProviderType ProviderUsed { get; set; }
    public string? ModelUsed { get; set; }
    
    // Quality Metrics
    public double? RelevanceScore { get; set; }
    public bool? UserFeedbackPositive { get; set; }
    public string? UserFeedbackComment { get; set; }
    
    // Error Tracking
    public bool IsError { get; set; } = false;
    public string? ErrorMessage { get; set; }
    
    // User and Tenant
    public string? UserId { get; set; }
    public virtual ApplicationUser? User { get; set; }
    
    public int? TenantId { get; set; }
    public virtual Tenant? Tenant { get; set; }
    
    // Timestamp
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
