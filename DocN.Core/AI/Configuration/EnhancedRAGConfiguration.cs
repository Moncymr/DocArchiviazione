namespace DocN.Core.AI.Configuration;

/// <summary>
/// Configuration options for Enhanced Agent RAG features
/// </summary>
public class EnhancedRAGConfiguration
{
    /// <summary>
    /// Enable the Enhanced Agent RAG Service with multi-agent orchestration
    /// </summary>
    public bool UseEnhancedAgentRAG { get; set; } = false;

    /// <summary>
    /// Query Analysis configuration
    /// </summary>
    public QueryAnalysisOptions QueryAnalysis { get; set; } = new();

    /// <summary>
    /// Retrieval configuration
    /// </summary>
    public RetrievalOptions Retrieval { get; set; } = new();

    /// <summary>
    /// Reranking configuration
    /// </summary>
    public RerankingOptions Reranking { get; set; } = new();

    /// <summary>
    /// Synthesis configuration
    /// </summary>
    public SynthesisOptions Synthesis { get; set; } = new();

    /// <summary>
    /// Telemetry configuration
    /// </summary>
    public TelemetryOptions Telemetry { get; set; } = new();

    /// <summary>
    /// Caching configuration
    /// </summary>
    public CachingOptions Caching { get; set; } = new();
}

/// <summary>
/// Query analysis phase options
/// </summary>
public class QueryAnalysisOptions
{
    /// <summary>
    /// Enable query analysis phase
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Maximum number of expansion terms to add
    /// </summary>
    public int MaxExpansionTerms { get; set; } = 10;

    /// <summary>
    /// Include synonyms in query expansion
    /// </summary>
    public bool IncludeSynonyms { get; set; } = true;

    /// <summary>
    /// Enable HyDE (Hypothetical Document Embeddings)
    /// </summary>
    public bool EnableHyDE { get; set; } = true;

    /// <summary>
    /// Enable query rewriting
    /// </summary>
    public bool EnableQueryRewriting { get; set; } = true;
}

/// <summary>
/// Retrieval phase options
/// </summary>
public class RetrievalOptions
{
    /// <summary>
    /// Default number of documents to retrieve
    /// </summary>
    public int DefaultTopK { get; set; } = 10;

    /// <summary>
    /// Minimum similarity score threshold (0-1)
    /// </summary>
    public double MinSimilarity { get; set; } = 0.5;

    /// <summary>
    /// Enable fallback to keyword search if vector search fails
    /// </summary>
    public bool FallbackToKeyword { get; set; } = true;

    /// <summary>
    /// Enable chunk-based retrieval instead of document-level
    /// </summary>
    public bool UseChunkRetrieval { get; set; } = true;

    /// <summary>
    /// Maximum candidate multiplier for initial retrieval (topK * multiplier)
    /// </summary>
    public int CandidateMultiplier { get; set; } = 2;

    /// <summary>
    /// Enable hybrid search (combine vector and text search)
    /// </summary>
    public bool EnableHybridSearch { get; set; } = false;
}

/// <summary>
/// Reranking phase options
/// </summary>
public class RerankingOptions
{
    /// <summary>
    /// Enable reranking phase
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Reranking model to use (if external reranker is configured)
    /// </summary>
    public string? RerankingModel { get; set; } = null;

    /// <summary>
    /// Enable diversity consideration in ranking
    /// </summary>
    public bool ConsiderDiversity { get; set; } = true;

    /// <summary>
    /// Enable temporal relevance weighting
    /// </summary>
    public bool EnableTemporalWeighting { get; set; } = false;

    /// <summary>
    /// Weight for recency in scoring (0-1)
    /// </summary>
    public double RecencyWeight { get; set; } = 0.1;

    /// <summary>
    /// MMR Lambda parameter for balancing relevance vs diversity (0-1)
    /// - 0.0 = Pure diversity (maximum variety, minimum relevance)
    /// - 0.5 = Balanced (recommended default)
    /// - 0.7 = Mostly relevant with some diversity (good for most use cases)
    /// - 1.0 = Pure relevance (no diversity consideration)
    /// </summary>
    public double MMRLambda { get; set; } = 0.7;
}

/// <summary>
/// Synthesis phase options
/// </summary>
public class SynthesisOptions
{
    /// <summary>
    /// Maximum context length in tokens
    /// </summary>
    public int MaxContextLength { get; set; } = 4000;

    /// <summary>
    /// Include citations in the response
    /// </summary>
    public bool IncludeCitations { get; set; } = true;

    /// <summary>
    /// Confidence threshold for answers (0-1)
    /// </summary>
    public double ConfidenceThreshold { get; set; } = 0.7;

    /// <summary>
    /// Enable contextual compression to fit more relevant info
    /// </summary>
    public bool EnableContextualCompression { get; set; } = false;

    /// <summary>
    /// Maximum number of iterative refinement attempts
    /// </summary>
    public int MaxRefinementIterations { get; set; } = 0;

    /// <summary>
    /// Enable fact checking during synthesis
    /// </summary>
    public bool EnableFactChecking { get; set; } = false;
}

/// <summary>
/// Telemetry options for agent tracking
/// </summary>
public class TelemetryOptions
{
    /// <summary>
    /// Enable detailed logging of agent operations
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = true;

    /// <summary>
    /// Track performance metrics for each phase
    /// </summary>
    public bool TrackPerformance { get; set; } = true;

    /// <summary>
    /// Track token usage per agent
    /// </summary>
    public bool TrackTokenUsage { get; set; } = true;

    /// <summary>
    /// Track agent decisions and reasoning
    /// </summary>
    public bool TrackAgentDecisions { get; set; } = false;
}

/// <summary>
/// Caching options for RAG pipeline
/// </summary>
public class CachingOptions
{
    /// <summary>
    /// Enable caching for retrieval results
    /// </summary>
    public bool EnableRetrievalCache { get; set; } = true;

    /// <summary>
    /// Enable caching for query analysis
    /// </summary>
    public bool EnableQueryAnalysisCache { get; set; } = true;

    /// <summary>
    /// Cache expiration in hours
    /// </summary>
    public int CacheExpirationHours { get; set; } = 1;

    /// <summary>
    /// Enable semantic cache (similar queries use cached results)
    /// </summary>
    public bool EnableSemanticCache { get; set; } = false;

    /// <summary>
    /// Similarity threshold for semantic cache matching (0-1)
    /// </summary>
    public double SemanticCacheSimilarityThreshold { get; set; } = 0.95;
}
