using DocN.Core.Interfaces;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace DocN.Data.Services;

/// <summary>
/// Service for intelligent semantic chunking of documents based on structure and content
/// </summary>
/// <remarks>
/// Implements advanced chunking strategies:
/// 1. Semantic chunking: Respects paragraph and sentence boundaries
/// 2. Structural chunking: Uses headers, sections, and document hierarchy
/// 3. Metadata extraction: Automatically extracts titles, keywords, and context
/// </remarks>
public class SemanticChunkingService : ISemanticChunkingService
{
    private readonly ILogger<SemanticChunkingService> _logger;
    
    // Common stop words for keyword extraction
    private static readonly HashSet<string> _stopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "a", "an", "and", "or", "but", "in", "on", "at", "to", "for",
        "of", "with", "by", "from", "as", "is", "was", "are", "were", "be",
        "been", "being", "have", "has", "had", "do", "does", "did", "will",
        "would", "could", "should", "may", "might", "can", "this", "that",
        "these", "those", "i", "you", "he", "she", "it", "we", "they"
    };
    
    public SemanticChunkingService(ILogger<SemanticChunkingService> logger)
    {
        _logger = logger;
    }
    
    /// <summary>
    /// Chunk document using semantic boundaries (paragraphs, sentences)
    /// </summary>
    public List<SemanticChunk> ChunkBySemantic(string text, int maxChunkSize = 1000, int minChunkSize = 200)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new List<SemanticChunk>();
        
        var chunks = new List<SemanticChunk>();
        
        // Split into paragraphs first (double newline or more)
        var paragraphs = Regex.Split(text, @"\r?\n\s*\r?\n")
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();
        
        var currentPosition = 0;
        var currentChunk = new List<string>();
        var currentChunkLength = 0;
        var chunkStartPosition = 0;
        
        foreach (var paragraph in paragraphs)
        {
            var paragraphLength = paragraph.Length;
            
            // If single paragraph exceeds max size, split by sentences
            if (paragraphLength > maxChunkSize)
            {
                // Save current chunk if exists
                if (currentChunk.Count > 0)
                {
                    chunks.Add(CreateSemanticChunk(
                        string.Join("\n\n", currentChunk),
                        chunkStartPosition,
                        currentPosition,
                        "paragraph"));
                    currentChunk.Clear();
                    currentChunkLength = 0;
                }
                
                // Split long paragraph by sentences
                var sentences = SplitIntoSentences(paragraph);
                var sentenceChunk = new List<string>();
                var sentenceChunkLength = 0;
                var sentenceStartPos = currentPosition;
                
                foreach (var sentence in sentences)
                {
                    if (sentenceChunkLength + sentence.Length > maxChunkSize && sentenceChunk.Count > 0)
                    {
                        chunks.Add(CreateSemanticChunk(
                            string.Join(" ", sentenceChunk),
                            sentenceStartPos,
                            sentenceStartPos + sentenceChunkLength,
                            "sentence"));
                        sentenceChunk.Clear();
                        sentenceChunkLength = 0;
                        sentenceStartPos = currentPosition + sentenceChunkLength;
                    }
                    
                    sentenceChunk.Add(sentence);
                    sentenceChunkLength += sentence.Length + 1; // +1 for space
                }
                
                if (sentenceChunk.Count > 0)
                {
                    chunks.Add(CreateSemanticChunk(
                        string.Join(" ", sentenceChunk),
                        sentenceStartPos,
                        currentPosition + paragraphLength,
                        "sentence"));
                }
                
                chunkStartPosition = currentPosition + paragraphLength;
            }
            else if (currentChunkLength + paragraphLength > maxChunkSize)
            {
                // Current chunk would exceed max size, save it and start new one
                if (currentChunk.Count > 0)
                {
                    chunks.Add(CreateSemanticChunk(
                        string.Join("\n\n", currentChunk),
                        chunkStartPosition,
                        currentPosition,
                        "paragraph"));
                }
                
                currentChunk.Clear();
                currentChunk.Add(paragraph);
                currentChunkLength = paragraphLength;
                chunkStartPosition = currentPosition;
            }
            else
            {
                // Add paragraph to current chunk
                if (currentChunk.Count == 0)
                {
                    chunkStartPosition = currentPosition;
                }
                currentChunk.Add(paragraph);
                currentChunkLength += paragraphLength + 2; // +2 for double newline
            }
            
            currentPosition += paragraphLength + 2; // Move position forward
        }
        
        // Add remaining chunk
        if (currentChunk.Count > 0 && currentChunkLength >= minChunkSize)
        {
            chunks.Add(CreateSemanticChunk(
                string.Join("\n\n", currentChunk),
                chunkStartPosition,
                currentPosition,
                "paragraph"));
        }
        
        _logger.LogDebug("Created {ChunkCount} semantic chunks from document", chunks.Count);
        return chunks;
    }
    
    /// <summary>
    /// Chunk document by structure (sections, headers)
    /// </summary>
    public List<SemanticChunk> ChunkByStructure(string text, int maxChunkSize = 1000)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new List<SemanticChunk>();
        
        var structure = ExtractStructure(text);
        var chunks = new List<SemanticChunk>();
        
        if (!structure.HasHierarchy || structure.Sections.Count == 0)
        {
            // Fall back to semantic chunking if no clear structure
            return ChunkBySemantic(text, maxChunkSize);
        }
        
        // Process each section
        foreach (var section in structure.Sections)
        {
            var sectionText = text.Substring(
                section.StartPosition,
                section.EndPosition - section.StartPosition);
            
            if (sectionText.Length <= maxChunkSize)
            {
                // Section fits in one chunk
                var chunk = CreateSemanticChunk(
                    sectionText,
                    section.StartPosition,
                    section.EndPosition,
                    "section");
                chunk.Metadata.Title = section.Title;
                chunk.Metadata.HeaderLevel = section.Level;
                chunk.Metadata.SectionPath = section.Title;
                chunks.Add(chunk);
            }
            else
            {
                // Section too large, split semantically but preserve section metadata
                var subChunks = ChunkBySemantic(sectionText, maxChunkSize);
                foreach (var subChunk in subChunks)
                {
                    subChunk.StartPosition += section.StartPosition;
                    subChunk.EndPosition += section.StartPosition;
                    subChunk.Metadata.Title = section.Title;
                    subChunk.Metadata.HeaderLevel = section.Level;
                    subChunk.Metadata.SectionPath = section.Title;
                    chunks.Add(subChunk);
                }
            }
        }
        
        _logger.LogDebug("Created {ChunkCount} structural chunks from {SectionCount} sections",
            chunks.Count, structure.Sections.Count);
        return chunks;
    }
    
    /// <summary>
    /// Extract document structure (sections, headers)
    /// </summary>
    public DocumentStructure ExtractStructure(string text)
    {
        var structure = new DocumentStructure();
        
        if (string.IsNullOrWhiteSpace(text))
            return structure;
        
        // Detect headers (markdown style, numbered sections, or ALL CAPS lines)
        var lines = text.Split('\n');
        var sections = new List<DocumentSection>();
        var currentPosition = 0;
        
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            var headerInfo = DetectHeader(line);
            
            if (headerInfo.IsHeader)
            {
                var section = new DocumentSection
                {
                    Title = headerInfo.Title,
                    Level = headerInfo.Level,
                    StartPosition = currentPosition
                };
                
                // Find end of section (next header or end of document)
                var endPosition = text.Length;
                for (int j = i + 1; j < lines.Length; j++)
                {
                    var nextLine = lines[j].Trim();
                    var nextHeaderInfo = DetectHeader(nextLine);
                    if (nextHeaderInfo.IsHeader && nextHeaderInfo.Level <= headerInfo.Level)
                    {
                        endPosition = currentPosition;
                        for (int k = 0; k < j; k++)
                        {
                            endPosition += lines[k].Length + 1; // +1 for newline
                        }
                        break;
                    }
                }
                
                section.EndPosition = endPosition;
                sections.Add(section);
            }
            
            currentPosition += lines[i].Length + 1; // +1 for newline
        }
        
        structure.Sections = sections;
        structure.HasHierarchy = sections.Count > 0;
        
        // Detect document type based on content
        structure.DocumentType = DetectDocumentType(text);
        
        return structure;
    }
    
    /// <summary>
    /// Create a semantic chunk with automatic metadata extraction
    /// </summary>
    private SemanticChunk CreateSemanticChunk(string text, int startPos, int endPos, string chunkType)
    {
        var chunk = new SemanticChunk
        {
            Text = text.Trim(),
            StartPosition = startPos,
            EndPosition = endPos,
            ChunkType = chunkType,
            Metadata = new ChunkMetadata
            {
                Keywords = ExtractKeywords(text, topN: 5)
            }
        };
        
        return chunk;
    }
    
    /// <summary>
    /// Split text into sentences
    /// </summary>
    private List<string> SplitIntoSentences(string text)
    {
        // Simple sentence splitting (can be improved with NLP)
        var sentences = Regex.Split(text, @"(?<=[.!?])\s+")
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
        return sentences;
    }
    
    /// <summary>
    /// Extract keywords from text using simple TF approach
    /// </summary>
    private List<string> ExtractKeywords(string text, int topN = 5)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new List<string>();
        
        // Tokenize and count word frequencies
        var words = Regex.Split(text.ToLower(), @"\W+")
            .Where(w => w.Length > 3 && !_stopWords.Contains(w))
            .ToList();
        
        var wordFrequencies = words
            .GroupBy(w => w)
            .OrderByDescending(g => g.Count())
            .Take(topN)
            .Select(g => g.Key)
            .ToList();
        
        return wordFrequencies;
    }
    
    /// <summary>
    /// Detect if a line is a header and extract information
    /// </summary>
    private (bool IsHeader, string Title, int Level) DetectHeader(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return (false, "", 0);
        
        // Markdown headers
        var markdownMatch = Regex.Match(line, @"^(#{1,6})\s+(.+)$");
        if (markdownMatch.Success)
        {
            var level = markdownMatch.Groups[1].Value.Length;
            var title = markdownMatch.Groups[2].Value.Trim();
            return (true, title, level);
        }
        
        // Numbered sections (1., 1.1., etc.)
        var numberedMatch = Regex.Match(line, @"^(\d+\.(?:\d+\.)*)\s+(.+)$");
        if (numberedMatch.Success)
        {
            var numberParts = numberedMatch.Groups[1].Value.Split('.');
            var level = numberParts.Length - 1; // Number of dots indicates level
            var title = numberedMatch.Groups[2].Value.Trim();
            return (true, title, level);
        }
        
        // ALL CAPS lines (potential headers)
        if (line.Length > 5 && line.Length < 100 && line == line.ToUpper() && 
            line.Any(char.IsLetter) && !line.Any(char.IsDigit))
        {
            return (true, line, 1);
        }
        
        // Underlined headers (next line is all === or ---)
        // This would require looking at next line, skip for now
        
        return (false, "", 0);
    }
    
    /// <summary>
    /// Detect document type based on content patterns
    /// </summary>
    private string DetectDocumentType(string text)
    {
        var lowerText = text.ToLower();
        
        // Technical document indicators
        if (lowerText.Contains("algorithm") || lowerText.Contains("implementation") ||
            lowerText.Contains("architecture") || lowerText.Contains("api"))
        {
            return "technical";
        }
        
        // Legal document indicators
        if (lowerText.Contains("whereas") || lowerText.Contains("hereinafter") ||
            lowerText.Contains("pursuant to") || lowerText.Contains("agreement"))
        {
            return "legal";
        }
        
        // Report indicators
        if (lowerText.Contains("executive summary") || lowerText.Contains("conclusions") ||
            lowerText.Contains("recommendations") || lowerText.Contains("findings"))
        {
            return "report";
        }
        
        return "general";
    }
}
