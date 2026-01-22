namespace DocN.Core.Interfaces;

/// <summary>
/// Enhanced chunking service with semantic and structure-aware capabilities
/// </summary>
public interface ISemanticChunkingService
{
    /// <summary>
    /// Chunk document using semantic boundaries (paragraphs, sections, headings)
    /// </summary>
    Task<List<EnhancedChunk>> ChunkDocumentSemanticAsync(
        string text,
        ChunkingStrategy strategy = ChunkingStrategy.Semantic,
        ChunkingOptions? options = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Chunk document with structure detection (Markdown, HTML, plain text)
    /// </summary>
    Task<List<EnhancedChunk>> ChunkWithStructureDetectionAsync(
        string text,
        string? contentType = null,
        ChunkingOptions? options = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Extract metadata from a chunk (keywords, title, section path)
    /// </summary>
    Task<ChunkMetadata> ExtractChunkMetadataAsync(
        string chunkText,
        string? context = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Calculate importance score for a chunk based on position, keywords, length
    /// </summary>
    double CalculateImportanceScore(
        string chunkText,
        int position,
        int totalChunks,
        Dictionary<string, double>? keywordWeights = null);
}

/// <summary>
/// Interface extension for document-specific operations
/// Note: This is kept separate to avoid circular dependency issues
/// between Core and Data layers
/// </summary>
public interface IDocumentChunkingExtensions
{
    /// <summary>
    /// Create DocumentChunk entities with rich metadata
    /// </summary>
    Task<List<object>> CreateEnhancedDocumentChunksAsync(
        object document,
        ChunkingStrategy strategy = ChunkingStrategy.Semantic,
        ChunkingOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Chunking strategy options
/// </summary>
public enum ChunkingStrategy
{
    /// <summary>
    /// Simple sliding window with overlap
    /// </summary>
    SlidingWindow,
    
    /// <summary>
    /// Semantic boundaries (paragraphs, sentences)
    /// </summary>
    Semantic,
    
    /// <summary>
    /// Section-based (headers and structure)
    /// </summary>
    Section,
    
    /// <summary>
    /// Adaptive based on content type and structure
    /// </summary>
    Adaptive,
    
    /// <summary>
    /// Sentence-aware chunking
    /// </summary>
    Sentence,
    
    /// <summary>
    /// Paragraph-aware chunking
    /// </summary>
    Paragraph
}

/// <summary>
/// Options for chunking
/// </summary>
public class ChunkingOptions
{
    /// <summary>
    /// Maximum chunk size in characters
    /// </summary>
    public int MaxChunkSize { get; set; } = 1000;
    
    /// <summary>
    /// Minimum chunk size in characters
    /// </summary>
    public int MinChunkSize { get; set; } = 100;
    
    /// <summary>
    /// Overlap between chunks in characters
    /// </summary>
    public int Overlap { get; set; } = 200;
    
    /// <summary>
    /// Whether to extract keywords
    /// </summary>
    public bool ExtractKeywords { get; set; } = true;
    
    /// <summary>
    /// Maximum number of keywords per chunk
    /// </summary>
    public int MaxKeywords { get; set; } = 10;
    
    /// <summary>
    /// Whether to detect section hierarchy
    /// </summary>
    public bool DetectSections { get; set; } = true;
    
    /// <summary>
    /// Whether to calculate importance scores
    /// </summary>
    public bool CalculateImportance { get; set; } = true;
    
    /// <summary>
    /// Content type hint (text/plain, text/markdown, text/html)
    /// </summary>
    public string? ContentType { get; set; }
    
    /// <summary>
    /// Whether to preserve code blocks intact
    /// </summary>
    public bool PreserveCodeBlocks { get; set; } = true;
    
    /// <summary>
    /// Whether to preserve tables intact
    /// </summary>
    public bool PreserveTables { get; set; } = true;
}

/// <summary>
/// Enhanced chunk with metadata
/// </summary>
public class EnhancedChunk
{
    public string Text { get; set; } = string.Empty;
    public int StartPosition { get; set; }
    public int EndPosition { get; set; }
    public string? Title { get; set; }
    public string? SectionPath { get; set; }
    public string? ChunkType { get; set; }
    public List<string> Keywords { get; set; } = new();
    public double ImportanceScore { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Chunk metadata
/// </summary>
public class ChunkMetadata
{
    public string? Title { get; set; }
    public string? SectionPath { get; set; }
    public List<string> Keywords { get; set; } = new();
    public string? ChunkType { get; set; }
    public string? Language { get; set; }
    public Dictionary<string, object> AdditionalMetadata { get; set; } = new();
}
