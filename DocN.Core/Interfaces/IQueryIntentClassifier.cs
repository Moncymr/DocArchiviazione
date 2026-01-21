namespace DocN.Core.Interfaces;

/// <summary>
/// Types of query intents for routing to appropriate handlers
/// </summary>
public enum QueryIntent
{
    /// <summary>
    /// Semantic search query - requires vector embedding search
    /// Examples: "What topics are discussed in...", "Find documents about..."
    /// </summary>
    SemanticSearch,
    
    /// <summary>
    /// Statistical query - requires database aggregation without vector filtering
    /// Examples: "How many PDFs...", "Count of...", "Total documents..."
    /// </summary>
    Statistical,
    
    /// <summary>
    /// Metadata query - focuses on categories, types, or document properties
    /// Examples: "What categories exist...", "List all file types..."
    /// </summary>
    MetadataQuery,
    
    /// <summary>
    /// Hybrid query - combines statistical and semantic aspects
    /// Examples: "How many documents discuss climate change?"
    /// </summary>
    Hybrid
}

/// <summary>
/// Service for classifying user query intent to route to appropriate processing pipeline
/// </summary>
public interface IQueryIntentClassifier
{
    /// <summary>
    /// Classify the intent of a user query
    /// </summary>
    /// <param name="query">The user's natural language query</param>
    /// <returns>The classified intent type</returns>
    Task<QueryIntent> ClassifyAsync(string query);
    
    /// <summary>
    /// Check if a query is statistical in nature (requires count/aggregate operations)
    /// </summary>
    /// <param name="query">The user's query</param>
    /// <returns>True if the query is statistical</returns>
    Task<bool> IsStatisticalQueryAsync(string query);
}
