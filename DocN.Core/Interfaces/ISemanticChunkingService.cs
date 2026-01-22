namespace DocN.Core.Interfaces;

/// <summary>
/// Service for intelligent semantic chunking of documents
/// </summary>
public interface ISemanticChunkingService
{
    /// <summary>
    /// Chunk document using semantic boundaries (paragraphs, sections, headers)
    /// </summary>
    /// <param name="text">Full document text</param>
    /// <param name="maxChunkSize">Maximum chunk size in characters</param>
    /// <param name="minChunkSize">Minimum chunk size in characters</param>
    /// <returns>List of semantic chunks with metadata</returns>
    List<SemanticChunk> ChunkBySemantic(string text, int maxChunkSize = 1000, int minChunkSize = 200);
    
    /// <summary>
    /// Chunk document by structure (sections, headers, paragraphs)
    /// </summary>
    /// <param name="text">Full document text</param>
    /// <param name="maxChunkSize">Maximum chunk size</param>
    /// <returns>List of structured chunks with hierarchy metadata</returns>
    List<SemanticChunk> ChunkByStructure(string text, int maxChunkSize = 1000);
    
    /// <summary>
    /// Extract document structure for semantic understanding
    /// </summary>
    /// <param name="text">Full document text</param>
    /// <returns>Document structure information</returns>
    DocumentStructure ExtractStructure(string text);
}

/// <summary>
/// Represents a semantic chunk with rich metadata
/// </summary>
public class SemanticChunk
{
    /// <summary>
    /// The chunk text content
    /// </summary>
    public string Text { get; set; } = string.Empty;
    
    /// <summary>
    /// Start position in original document
    /// </summary>
    public int StartPosition { get; set; }
    
    /// <summary>
    /// End position in original document
    /// </summary>
    public int EndPosition { get; set; }
    
    /// <summary>
    /// Chunk metadata (title, section, keywords, etc.)
    /// </summary>
    public ChunkMetadata Metadata { get; set; } = new();
    
    /// <summary>
    /// Semantic type of chunk (paragraph, section, list, etc.)
    /// </summary>
    public string ChunkType { get; set; } = "paragraph";
}

/// <summary>
/// Metadata for a document chunk
/// </summary>
public class ChunkMetadata
{
    /// <summary>
    /// Title of the section this chunk belongs to
    /// </summary>
    public string? Title { get; set; }
    
    /// <summary>
    /// Section hierarchy (e.g., "1.2.3" or "Introduction > Background")
    /// </summary>
    public string? SectionPath { get; set; }
    
    /// <summary>
    /// Auto-extracted keywords for this chunk
    /// </summary>
    public List<string> Keywords { get; set; } = new();
    
    /// <summary>
    /// Document type hint (e.g., "technical", "legal", "report")
    /// </summary>
    public string? DocumentType { get; set; }
    
    /// <summary>
    /// Header level (0 = no header, 1-6 = H1-H6)
    /// </summary>
    public int HeaderLevel { get; set; }
    
    /// <summary>
    /// Is this chunk part of a list?
    /// </summary>
    public bool IsListItem { get; set; }
    
    /// <summary>
    /// Additional custom metadata
    /// </summary>
    public Dictionary<string, string> CustomMetadata { get; set; } = new();
}

/// <summary>
/// Document structure information
/// </summary>
public class DocumentStructure
{
    /// <summary>
    /// Detected sections in the document
    /// </summary>
    public List<DocumentSection> Sections { get; set; } = new();
    
    /// <summary>
    /// Overall document type
    /// </summary>
    public string? DocumentType { get; set; }
    
    /// <summary>
    /// Has clear hierarchical structure
    /// </summary>
    public bool HasHierarchy { get; set; }
}

/// <summary>
/// Document section information
/// </summary>
public class DocumentSection
{
    /// <summary>
    /// Section title
    /// </summary>
    public string Title { get; set; } = string.Empty;
    
    /// <summary>
    /// Section level (1 = top level, 2 = subsection, etc.)
    /// </summary>
    public int Level { get; set; }
    
    /// <summary>
    /// Start position in document
    /// </summary>
    public int StartPosition { get; set; }
    
    /// <summary>
    /// End position in document
    /// </summary>
    public int EndPosition { get; set; }
    
    /// <summary>
    /// Child sections
    /// </summary>
    public List<DocumentSection> Children { get; set; } = new();
}
