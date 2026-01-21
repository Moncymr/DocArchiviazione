using DocN.Data.Models;

namespace DocN.Data.Services.Agents;

/// <summary>
/// Base interface for all agents in the system
/// </summary>
public interface IAgent
{
    /// <summary>
    /// Name of the agent
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Description of what this agent does
    /// </summary>
    string Description { get; }
}

/// <summary>
/// Agent responsible for retrieving relevant documents
/// </summary>
public interface IRetrievalAgent : IAgent
{
    /// <summary>
    /// Retrieve relevant documents for a query
    /// </summary>
    Task<List<Document>> RetrieveAsync(string query, string? userId = null, int topK = 5);

    /// <summary>
    /// Retrieve relevant document chunks for more precise retrieval
    /// </summary>
    Task<List<DocumentChunk>> RetrieveChunksAsync(string query, string? userId = null, int topK = 10);
}

/// <summary>
/// Agent responsible for synthesizing answers from retrieved documents
/// </summary>
public interface ISynthesisAgent : IAgent
{
    /// <summary>
    /// Synthesize an answer from documents
    /// </summary>
    Task<string> SynthesizeAsync(string query, List<Document> documents, List<Message>? conversationHistory = null);

    /// <summary>
    /// Synthesize an answer from document chunks
    /// </summary>
    Task<string> SynthesizeFromChunksAsync(string query, List<DocumentChunk> chunks, List<Message>? conversationHistory = null);
}

/// <summary>
/// Agent responsible for classifying and categorizing documents
/// </summary>
public interface IClassificationAgent : IAgent
{
    /// <summary>
    /// Suggest a category for a document
    /// </summary>
    Task<CategorySuggestion> SuggestCategoryAsync(Document document);

    /// <summary>
    /// Extract tags from a document
    /// </summary>
    Task<List<string>> ExtractTagsAsync(Document document);

    /// <summary>
    /// Classify document type
    /// </summary>
    Task<string> ClassifyDocumentTypeAsync(Document document);
}

/// <summary>
/// Category suggestion with confidence and reasoning
/// </summary>
public class CategorySuggestion
{
    public string Category { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string Reasoning { get; set; } = string.Empty;
    public List<string> AlternativeCategories { get; set; } = new();
}

/// <summary>
/// Result from the agent orchestrator
/// </summary>
public class AgentOrchestrationResult
{
    public string Answer { get; set; } = string.Empty;
    public List<Document> RetrievedDocuments { get; set; } = new();
    public List<DocumentChunk> RetrievedChunks { get; set; } = new();
    public string RetrievalStrategy { get; set; } = string.Empty;
    public TimeSpan RetrievalTime { get; set; }
    public TimeSpan SynthesisTime { get; set; }
    public TimeSpan TotalTime { get; set; }
}

/// <summary>
/// Orchestrator that coordinates multiple agents
/// </summary>
public interface IAgentOrchestrator
{
    /// <summary>
    /// Process a query using multi-agent workflow
    /// </summary>
    Task<AgentOrchestrationResult> ProcessQueryAsync(
        string query,
        string? userId = null,
        int? conversationId = null);

    /// <summary>
    /// Process a document using classification agents
    /// </summary>
    Task<DocumentClassificationResult> ClassifyDocumentAsync(Document document);
}

/// <summary>
/// Result from document classification
/// </summary>
public class DocumentClassificationResult
{
    public CategorySuggestion CategorySuggestion { get; set; } = null!;
    public List<string> Tags { get; set; } = new();
    public string DocumentType { get; set; } = string.Empty;
}
