using DocN.Core.Interfaces;
using DocN.Data.Models;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace DocN.Data.Services;

/// <summary>
/// Enhanced semantic chunking service with structure-aware capabilities
/// </summary>
public class SemanticChunkingService : ISemanticChunkingService
{
    private readonly ILogger<SemanticChunkingService> _logger;
    private readonly IMultiProviderAIService? _aiService;

    // Common stop words for keyword extraction
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "a", "an", "and", "or", "but", "in", "on", "at", "to", "for",
        "of", "with", "by", "from", "as", "is", "was", "are", "were", "be",
        "been", "being", "have", "has", "had", "do", "does", "did", "will",
        "would", "could", "should", "may", "might", "can", "this", "that",
        "these", "those", "i", "you", "he", "she", "it", "we", "they", "what",
        "which", "who", "when", "where", "why", "how"
    };

    public SemanticChunkingService(
        ILogger<SemanticChunkingService> logger,
        IMultiProviderAIService? aiService = null)
    {
        _logger = logger;
        _aiService = aiService;
    }

    /// <summary>
    /// Chunk document using semantic boundaries
    /// </summary>
    public async Task<List<EnhancedChunk>> ChunkDocumentSemanticAsync(
        string text,
        ChunkingStrategy strategy = ChunkingStrategy.Semantic,
        ChunkingOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new ChunkingOptions();
        
        _logger.LogInformation("Starting semantic chunking with strategy: {Strategy}", strategy);

        return strategy switch
        {
            ChunkingStrategy.Paragraph => await ChunkByParagraphAsync(text, options, cancellationToken),
            ChunkingStrategy.Sentence => await ChunkBySentenceAsync(text, options, cancellationToken),
            ChunkingStrategy.Section => await ChunkBySectionAsync(text, options, cancellationToken),
            ChunkingStrategy.Adaptive => await ChunkAdaptiveAsync(text, options, cancellationToken),
            ChunkingStrategy.Semantic => await ChunkSemanticAsync(text, options, cancellationToken),
            _ => await ChunkSlidingWindowAsync(text, options, cancellationToken)
        };
    }

    /// <summary>
    /// Chunk with structure detection
    /// </summary>
    public async Task<List<EnhancedChunk>> ChunkWithStructureDetectionAsync(
        string text,
        string? contentType = null,
        ChunkingOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new ChunkingOptions();
        
        // Detect content type if not provided
        contentType ??= DetectContentType(text);
        options.ContentType = contentType;

        _logger.LogInformation("Chunking with detected content type: {ContentType}", contentType);

        return contentType switch
        {
            "text/markdown" => await ChunkMarkdownAsync(text, options, cancellationToken),
            "text/html" => await ChunkHtmlAsync(text, options, cancellationToken),
            _ => await ChunkDocumentSemanticAsync(text, ChunkingStrategy.Semantic, options, cancellationToken)
        };
    }

    /// <summary>
    /// Extract metadata from a chunk
    /// </summary>
    public async Task<ChunkMetadata> ExtractChunkMetadataAsync(
        string chunkText,
        string? context = null,
        CancellationToken cancellationToken = default)
    {
        var metadata = new ChunkMetadata();

        // Extract keywords
        metadata.Keywords = ExtractKeywords(chunkText, maxKeywords: 10);

        // Detect chunk type
        metadata.ChunkType = DetectChunkType(chunkText);

        // Extract title from first line if it looks like a header
        var lines = chunkText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length > 0)
        {
            var firstLine = lines[0].Trim();
            if (IsLikelyHeader(firstLine))
            {
                metadata.Title = firstLine.TrimStart('#', ' ', '\t');
            }
        }

        return metadata;
    }

    /// <summary>
    /// Calculate importance score for a chunk
    /// </summary>
    public double CalculateImportanceScore(
        string chunkText,
        int position,
        int totalChunks,
        Dictionary<string, double>? keywordWeights = null)
    {
        double score = 0.5; // Base score

        // Position-based scoring (first and last chunks often more important)
        if (position == 0)
            score += 0.2;
        else if (position < totalChunks * 0.1)
            score += 0.1;
        
        if (position == totalChunks - 1 || position >= totalChunks * 0.9)
            score += 0.1;

        // Length-based scoring (moderate length is good)
        int length = chunkText.Length;
        if (length >= 200 && length <= 800)
            score += 0.1;

        // Header detection
        if (chunkText.TrimStart().StartsWith("#") || IsLikelyHeader(chunkText))
            score += 0.15;

        // Keyword density
        if (keywordWeights != null)
        {
            var words = GetWords(chunkText);
            double keywordScore = 0;
            foreach (var word in words)
            {
                if (keywordWeights.TryGetValue(word.ToLower(), out double weight))
                {
                    keywordScore += weight;
                }
            }
            score += Math.Min(0.15, keywordScore / words.Count * 10);
        }

        return Math.Min(1.0, score);
    }

    /// <summary>
    /// Create enhanced document chunks
    /// </summary>
    public async Task<List<DocumentChunk>> CreateEnhancedDocumentChunksAsync(
        Document document,
        ChunkingStrategy strategy = ChunkingStrategy.Semantic,
        ChunkingOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new ChunkingOptions();
        
        var enhancedChunks = await ChunkDocumentSemanticAsync(
            document.ExtractedText, 
            strategy, 
            options, 
            cancellationToken);

        var documentChunks = new List<DocumentChunk>();
        
        for (int i = 0; i < enhancedChunks.Count; i++)
        {
            var chunk = enhancedChunks[i];
            
            var documentChunk = new DocumentChunk
            {
                DocumentId = document.Id,
                ChunkIndex = i,
                ChunkText = chunk.Text,
                StartPosition = chunk.StartPosition,
                EndPosition = chunk.EndPosition,
                Title = chunk.Title,
                SectionPath = chunk.SectionPath,
                ChunkType = chunk.ChunkType,
                KeywordsJson = JsonSerializer.Serialize(chunk.Keywords),
                ImportanceScore = chunk.ImportanceScore,
                MetadataJson = JsonSerializer.Serialize(chunk.Metadata),
                TokenCount = EstimateTokenCount(chunk.Text),
                CreatedAt = DateTime.UtcNow
            };

            documentChunks.Add(documentChunk);
        }

        _logger.LogInformation("Created {Count} enhanced document chunks for document {DocumentId}", 
            documentChunks.Count, document.Id);

        return documentChunks;
    }

    // Private helper methods

    private async Task<List<EnhancedChunk>> ChunkByParagraphAsync(
        string text, 
        ChunkingOptions options, 
        CancellationToken cancellationToken)
    {
        var chunks = new List<EnhancedChunk>();
        var paragraphs = text.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);
        
        int position = 0;
        var currentChunk = new StringBuilder();
        int chunkStartPos = 0;

        foreach (var para in paragraphs)
        {
            var trimmedPara = para.Trim();
            if (string.IsNullOrEmpty(trimmedPara))
                continue;

            // If adding this paragraph exceeds max size, finalize current chunk
            if (currentChunk.Length > 0 && 
                currentChunk.Length + trimmedPara.Length > options.MaxChunkSize)
            {
                await FinalizeChunkAsync(chunks, currentChunk.ToString(), 
                    chunkStartPos, position, options, cancellationToken);
                
                // Start new chunk with overlap
                currentChunk.Clear();
                chunkStartPos = Math.Max(0, position - options.Overlap);
            }

            currentChunk.AppendLine(trimmedPara);
            position += para.Length + 2; // +2 for \n\n
        }

        // Finalize last chunk
        if (currentChunk.Length > 0)
        {
            await FinalizeChunkAsync(chunks, currentChunk.ToString(), 
                chunkStartPos, position, options, cancellationToken);
        }

        return chunks;
    }

    private async Task<List<EnhancedChunk>> ChunkBySentenceAsync(
        string text, 
        ChunkingOptions options, 
        CancellationToken cancellationToken)
    {
        var chunks = new List<EnhancedChunk>();
        var sentences = SplitIntoSentences(text);
        
        int position = 0;
        var currentChunk = new StringBuilder();
        int chunkStartPos = 0;

        foreach (var sentence in sentences)
        {
            if (string.IsNullOrWhiteSpace(sentence))
                continue;

            // If adding this sentence exceeds max size, finalize current chunk
            if (currentChunk.Length > 0 && 
                currentChunk.Length + sentence.Length > options.MaxChunkSize)
            {
                await FinalizeChunkAsync(chunks, currentChunk.ToString(), 
                    chunkStartPos, position, options, cancellationToken);
                
                currentChunk.Clear();
                chunkStartPos = Math.Max(0, position - options.Overlap);
            }

            currentChunk.Append(sentence).Append(" ");
            position += sentence.Length + 1;
        }

        if (currentChunk.Length > 0)
        {
            await FinalizeChunkAsync(chunks, currentChunk.ToString(), 
                chunkStartPos, position, options, cancellationToken);
        }

        return chunks;
    }

    private async Task<List<EnhancedChunk>> ChunkBySectionAsync(
        string text, 
        ChunkingOptions options, 
        CancellationToken cancellationToken)
    {
        var chunks = new List<EnhancedChunk>();
        var sections = DetectSections(text);
        
        foreach (var section in sections)
        {
            // If section is too large, split it further
            if (section.Content.Length > options.MaxChunkSize)
            {
                var subChunks = await ChunkByParagraphAsync(section.Content, options, cancellationToken);
                foreach (var subChunk in subChunks)
                {
                    subChunk.Title = section.Title;
                    subChunk.SectionPath = section.Path;
                    chunks.Add(subChunk);
                }
            }
            else
            {
                var chunk = new EnhancedChunk
                {
                    Text = section.Content,
                    StartPosition = section.StartPosition,
                    EndPosition = section.EndPosition,
                    Title = section.Title,
                    SectionPath = section.Path,
                    ChunkType = "Section"
                };

                if (options.ExtractKeywords)
                {
                    chunk.Keywords = ExtractKeywords(chunk.Text, options.MaxKeywords);
                }

                if (options.CalculateImportance)
                {
                    chunk.ImportanceScore = CalculateImportanceScore(
                        chunk.Text, chunks.Count, sections.Count);
                }

                chunks.Add(chunk);
            }
        }

        return chunks;
    }

    private async Task<List<EnhancedChunk>> ChunkSemanticAsync(
        string text, 
        ChunkingOptions options, 
        CancellationToken cancellationToken)
    {
        // Semantic chunking combines paragraph and sentence awareness
        var chunks = new List<EnhancedChunk>();
        var paragraphs = text.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);
        
        int position = 0;
        var currentChunk = new StringBuilder();
        int chunkStartPos = 0;
        string? currentTitle = null;

        foreach (var para in paragraphs)
        {
            var trimmedPara = para.Trim();
            if (string.IsNullOrEmpty(trimmedPara))
                continue;

            // Check if this is a header
            if (IsLikelyHeader(trimmedPara))
            {
                // Finalize previous chunk before header
                if (currentChunk.Length >= options.MinChunkSize)
                {
                    await FinalizeChunkAsync(chunks, currentChunk.ToString(), 
                        chunkStartPos, position, options, cancellationToken, currentTitle);
                    currentChunk.Clear();
                    chunkStartPos = position;
                }
                
                currentTitle = trimmedPara.TrimStart('#', ' ', '\t');
            }

            // Check if we need to split
            if (currentChunk.Length > 0 && 
                currentChunk.Length + trimmedPara.Length > options.MaxChunkSize)
            {
                await FinalizeChunkAsync(chunks, currentChunk.ToString(), 
                    chunkStartPos, position, options, cancellationToken, currentTitle);
                currentChunk.Clear();
                chunkStartPos = Math.Max(0, position - options.Overlap);
            }

            currentChunk.AppendLine(trimmedPara);
            position += para.Length + 2;
        }

        if (currentChunk.Length > 0)
        {
            await FinalizeChunkAsync(chunks, currentChunk.ToString(), 
                chunkStartPos, position, options, cancellationToken, currentTitle);
        }

        return chunks;
    }

    private async Task<List<EnhancedChunk>> ChunkAdaptiveAsync(
        string text, 
        ChunkingOptions options, 
        CancellationToken cancellationToken)
    {
        // Adaptive chunking chooses the best strategy based on content
        var contentType = DetectContentType(text);
        
        if (contentType == "text/markdown")
            return await ChunkMarkdownAsync(text, options, cancellationToken);
        
        // If text has clear sections, use section-based
        var sections = DetectSections(text);
        if (sections.Count > 3)
            return await ChunkBySectionAsync(text, options, cancellationToken);
        
        // Otherwise use semantic
        return await ChunkSemanticAsync(text, options, cancellationToken);
    }

    private async Task<List<EnhancedChunk>> ChunkSlidingWindowAsync(
        string text, 
        ChunkingOptions options, 
        CancellationToken cancellationToken)
    {
        var chunks = new List<EnhancedChunk>();
        int position = 0;

        while (position < text.Length)
        {
            int endPosition = Math.Min(position + options.MaxChunkSize, text.Length);
            
            // Try to break at sentence boundary
            if (endPosition < text.Length)
            {
                var searchStart = Math.Max(position, endPosition - 100);
                var lastPeriod = text.LastIndexOfAny(new[] { '.', '!', '?' }, 
                    endPosition - 1, endPosition - searchStart);
                
                if (lastPeriod > searchStart)
                    endPosition = lastPeriod + 1;
            }

            var chunkText = text.Substring(position, endPosition - position).Trim();
            
            if (!string.IsNullOrWhiteSpace(chunkText))
            {
                await FinalizeChunkAsync(chunks, chunkText, position, endPosition, 
                    options, cancellationToken);
            }

            position = endPosition - options.Overlap;
            if (position <= 0 || position >= text.Length)
                break;
        }

        return chunks;
    }

    private async Task<List<EnhancedChunk>> ChunkMarkdownAsync(
        string text, 
        ChunkingOptions options, 
        CancellationToken cancellationToken)
    {
        var chunks = new List<EnhancedChunk>();
        var lines = text.Split('\n');
        var currentChunk = new StringBuilder();
        var sectionStack = new Stack<string>();
        int position = 0;
        int chunkStartPos = 0;

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            
            // Detect markdown headers
            var headerMatch = Regex.Match(trimmedLine, @"^(#{1,6})\s+(.+)$");
            if (headerMatch.Success)
            {
                // Finalize previous chunk
                if (currentChunk.Length >= options.MinChunkSize)
                {
                    await FinalizeChunkAsync(chunks, currentChunk.ToString(), 
                        chunkStartPos, position, options, cancellationToken, 
                        sectionStack.Count > 0 ? string.Join(" > ", sectionStack.Reverse()) : null);
                    currentChunk.Clear();
                    chunkStartPos = position;
                }

                // Update section hierarchy
                int level = headerMatch.Groups[1].Length;
                string title = headerMatch.Groups[2].Value;
                
                while (sectionStack.Count >= level)
                    sectionStack.Pop();
                
                sectionStack.Push(title);
            }

            currentChunk.AppendLine(line);
            position += line.Length + 1;

            // Check if we need to split
            if (currentChunk.Length > options.MaxChunkSize)
            {
                await FinalizeChunkAsync(chunks, currentChunk.ToString(), 
                    chunkStartPos, position, options, cancellationToken, 
                    sectionStack.Count > 0 ? string.Join(" > ", sectionStack.Reverse()) : null);
                currentChunk.Clear();
                chunkStartPos = position - options.Overlap;
            }
        }

        if (currentChunk.Length > 0)
        {
            await FinalizeChunkAsync(chunks, currentChunk.ToString(), 
                chunkStartPos, position, options, cancellationToken,
                sectionStack.Count > 0 ? string.Join(" > ", sectionStack.Reverse()) : null);
        }

        return chunks;
    }

    private Task<List<EnhancedChunk>> ChunkHtmlAsync(
        string text, 
        ChunkingOptions options, 
        CancellationToken cancellationToken)
    {
        // Simplified HTML chunking - strip tags and chunk as text
        var plainText = Regex.Replace(text, "<[^>]+>", " ");
        plainText = Regex.Replace(plainText, @"\s+", " ").Trim();
        return ChunkSemanticAsync(plainText, options, cancellationToken);
    }

    private async Task FinalizeChunkAsync(
        List<EnhancedChunk> chunks,
        string chunkText,
        int startPos,
        int endPos,
        ChunkingOptions options,
        CancellationToken cancellationToken,
        string? title = null)
    {
        var chunk = new EnhancedChunk
        {
            Text = chunkText.Trim(),
            StartPosition = startPos,
            EndPosition = endPos,
            Title = title,
            ChunkType = DetectChunkType(chunkText)
        };

        if (options.ExtractKeywords)
        {
            chunk.Keywords = ExtractKeywords(chunk.Text, options.MaxKeywords);
        }

        if (options.CalculateImportance)
        {
            chunk.ImportanceScore = CalculateImportanceScore(
                chunk.Text, chunks.Count, 100); // Estimate total
        }

        chunks.Add(chunk);
    }

    private List<string> ExtractKeywords(string text, int maxKeywords)
    {
        var words = GetWords(text);
        var wordFreq = new Dictionary<string, int>();

        foreach (var word in words)
        {
            var lower = word.ToLower();
            if (lower.Length >= 3 && !StopWords.Contains(lower))
            {
                wordFreq[lower] = wordFreq.GetValueOrDefault(lower, 0) + 1;
            }
        }

        return wordFreq
            .OrderByDescending(kvp => kvp.Value)
            .Take(maxKeywords)
            .Select(kvp => kvp.Key)
            .ToList();
    }

    private List<string> GetWords(string text)
    {
        return Regex.Split(text, @"\W+")
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .ToList();
    }

    private string DetectChunkType(string text)
    {
        var trimmed = text.TrimStart();
        
        if (Regex.IsMatch(trimmed, @"^#{1,6}\s"))
            return "Header";
        
        if (Regex.IsMatch(trimmed, @"^[-*+]\s") || Regex.IsMatch(trimmed, @"^\d+\.\s"))
            return "ListItem";
        
        if (Regex.IsMatch(trimmed, @"^```") || Regex.IsMatch(trimmed, @"^    "))
            return "Code";
        
        if (Regex.IsMatch(trimmed, @"^\|.*\|"))
            return "Table";
        
        return "Paragraph";
    }

    private bool IsLikelyHeader(string text)
    {
        var trimmed = text.Trim();
        return trimmed.Length < 100 && 
               (Regex.IsMatch(trimmed, @"^#{1,6}\s") || 
                (trimmed.Length < 50 && trimmed.EndsWith(":")) ||
                trimmed.All(c => char.IsUpper(c) || char.IsWhiteSpace(c) || char.IsDigit(c)));
    }

    private string DetectContentType(string text)
    {
        if (Regex.IsMatch(text, @"#{1,6}\s+\w+") || Regex.IsMatch(text, @"^```"))
            return "text/markdown";
        
        if (Regex.IsMatch(text, @"<[^>]+>"))
            return "text/html";
        
        return "text/plain";
    }

    private List<DocumentSection> DetectSections(string text)
    {
        var sections = new List<DocumentSection>();
        var lines = text.Split('\n');
        var currentSection = new DocumentSection { Title = "Introduction", StartPosition = 0 };
        var contentBuilder = new StringBuilder();
        int position = 0;
        var sectionPath = new Stack<string>();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            
            if (IsLikelyHeader(trimmed))
            {
                // Save previous section
                if (contentBuilder.Length > 0)
                {
                    currentSection.Content = contentBuilder.ToString().Trim();
                    currentSection.EndPosition = position;
                    sections.Add(currentSection);
                }

                // Start new section
                var title = trimmed.TrimStart('#', ' ', '\t');
                sectionPath.Push(title);
                
                currentSection = new DocumentSection
                {
                    Title = title,
                    Path = string.Join(" > ", sectionPath.Reverse()),
                    StartPosition = position
                };
                contentBuilder.Clear();
            }
            else
            {
                contentBuilder.AppendLine(line);
            }

            position += line.Length + 1;
        }

        // Add last section
        if (contentBuilder.Length > 0)
        {
            currentSection.Content = contentBuilder.ToString().Trim();
            currentSection.EndPosition = position;
            sections.Add(currentSection);
        }

        return sections;
    }

    private List<string> SplitIntoSentences(string text)
    {
        // Simple sentence splitter
        var pattern = @"(?<=[.!?])\s+(?=[A-Z])";
        return Regex.Split(text, pattern)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
    }

    private int EstimateTokenCount(string text)
    {
        // Rough estimation: 1 token ≈ 4 characters
        return (int)Math.Ceiling(text.Length / 4.0);
    }

    private class DocumentSection
    {
        public string Title { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public int StartPosition { get; set; }
        public int EndPosition { get; set; }
    }
}
