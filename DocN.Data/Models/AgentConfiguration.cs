namespace DocN.Data.Models;

/// <summary>
/// Represents a configured AI agent for RAG operations
/// </summary>
public class AgentConfiguration
{
    public int Id { get; set; }
    
    // Basic Information
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public AgentType AgentType { get; set; }
    
    // Provider Configuration
    public AIProviderType PrimaryProvider { get; set; }
    public AIProviderType? FallbackProvider { get; set; }
    
    // Model Configuration
    public string? ModelName { get; set; }
    public string? EmbeddingModelName { get; set; }
    
    // RAG Configuration
    public int MaxDocumentsToRetrieve { get; set; } = 5;
    public double SimilarityThreshold { get; set; } = 0.7;
    public int MaxTokensForContext { get; set; } = 4000;
    public int MaxTokensForResponse { get; set; } = 2000;
    public double Temperature { get; set; } = 0.7;
    
    // System Prompt and Instructions
    public string SystemPrompt { get; set; } = string.Empty;
    public string? CustomInstructions { get; set; }
    
    // Agent Capabilities
    public bool CanRetrieveDocuments { get; set; } = true;
    public bool CanClassifyDocuments { get; set; } = false;
    public bool CanExtractTags { get; set; } = false;
    public bool CanSummarize { get; set; } = true;
    public bool CanAnswer { get; set; } = true;
    
    // Search Configuration
    public bool UseHybridSearch { get; set; } = true;
    public double HybridSearchAlpha { get; set; } = 0.5; // Balance between vector and full-text
    
    // Advanced Options
    public bool EnableConversationHistory { get; set; } = true;
    public int MaxConversationHistoryMessages { get; set; } = 10;
    public bool EnableCitation { get; set; } = true;
    public bool EnableStreaming { get; set; } = false;
    
    // Filters and Scope
    public string? CategoryFilter { get; set; } // JSON array of categories to restrict search
    public string? TagFilter { get; set; } // JSON array of tags to restrict search
    public DocumentVisibility? VisibilityFilter { get; set; }
    
    // Performance Tuning
    public int? CacheTTLSeconds { get; set; }
    public bool EnableParallelRetrieval { get; set; } = false;
    
    // Status and Metadata
    public bool IsActive { get; set; } = true;
    public bool IsPublic { get; set; } = false; // Can be used by all users
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public int UsageCount { get; set; } = 0;
    
    // Owner
    public string? OwnerId { get; set; }
    public virtual ApplicationUser? Owner { get; set; }
    
    // Multi-tenant support
    public int? TenantId { get; set; }
    public virtual Tenant? Tenant { get; set; }
    
    // Template reference (if created from template)
    public int? TemplateId { get; set; }
    public virtual AgentTemplate? Template { get; set; }
}

/// <summary>
/// Types of AI agents that can be configured
/// </summary>
public enum AgentType
{
    /// <summary>
    /// General Q&A agent for document retrieval and answering
    /// </summary>
    QuestionAnswering = 1,
    
    /// <summary>
    /// Agent specialized in document summarization
    /// </summary>
    Summarization = 2,
    
    /// <summary>
    /// Agent for classifying and categorizing documents
    /// </summary>
    Classification = 3,
    
    /// <summary>
    /// Agent for extracting structured data from documents
    /// </summary>
    DataExtraction = 4,
    
    /// <summary>
    /// Agent for comparing multiple documents
    /// </summary>
    Comparison = 5,
    
    /// <summary>
    /// Custom agent with user-defined behavior
    /// </summary>
    Custom = 99
}
