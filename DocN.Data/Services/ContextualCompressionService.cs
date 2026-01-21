using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DocN.Core.Interfaces;
using DocN.Data.Utilities;
using System.Text;
using System.Text.RegularExpressions;

namespace DocN.Data.Services;

/// <summary>
/// Implementation of contextual compression service
/// Reduces token count while preserving relevant information
/// </summary>
public class ContextualCompressionService : IContextualCompressionService
{
    private readonly ILogger<ContextualCompressionService> _logger;
    private readonly IEmbeddingService _embeddingService;
    private readonly ContextualCompressionConfiguration _config;
    
    // Approximate tokens per character ratio (English text)
    private const double TOKENS_PER_CHAR = 0.25;
    
    public ContextualCompressionService(
        ILogger<ContextualCompressionService> logger,
        IEmbeddingService embeddingService,
        IOptions<ContextualCompressionConfiguration>? config = null)
    {
        _logger = logger;
        _embeddingService = embeddingService;
        _config = config?.Value ?? new ContextualCompressionConfiguration();
    }

    /// <inheritdoc/>
    public async Task<List<CompressedChunk>> CompressChunksAsync(
        string query,
        List<string> chunks,
        int targetTokenCount)
    {
        try
        {
            if (!_config.Enabled || chunks.Count == 0)
            {
                return chunks.Select((c, i) => new CompressedChunk
                {
                    Content = c,
                    OriginalIndex = i,
                    CompressionRatio = 1.0,
                    RelevanceScore = 1.0,
                    TokenCount = EstimateTokenCount(c)
                }).ToList();
            }

            _logger.LogDebug("Compressing {Count} chunks to target {TargetTokens} tokens", 
                chunks.Count, targetTokenCount);

            // Step 1: Deduplicate if enabled
            var uniqueChunks = _config.EnableDeduplication
                ? await DeduplicateChunksAsync(chunks, _config.DeduplicationThreshold)
                : chunks;

            // Step 2: Calculate relevance scores for each chunk
            var chunksWithScores = new List<(string chunk, int index, double score)>();
            for (int i = 0; i < uniqueChunks.Count; i++)
            {
                var score = await CalculateRelevanceScoreAsync(query, uniqueChunks[i]);
                chunksWithScores.Add((uniqueChunks[i], i, score));
            }

            // Step 3: Sort by relevance and select chunks within token budget
            var sortedChunks = chunksWithScores
                .Where(c => c.score >= _config.MinRelevanceScore)
                .OrderByDescending(c => c.score)
                .ToList();

            var compressedResults = new List<CompressedChunk>();
            int currentTokens = 0;

            foreach (var (chunk, index, score) in sortedChunks)
            {
                var chunkTokens = EstimateTokenCount(chunk);
                
                // If adding this chunk would exceed budget, try to compress it
                if (currentTokens + chunkTokens > targetTokenCount)
                {
                    var remainingTokens = targetTokenCount - currentTokens;
                    if (remainingTokens > 50) // Only compress if we have reasonable space
                    {
                        var compressed = await CompressTextAsync(query, chunk, remainingTokens);
                        var compressedTokens = EstimateTokenCount(compressed);
                        
                        if (compressedTokens <= remainingTokens)
                        {
                            compressedResults.Add(new CompressedChunk
                            {
                                Content = compressed,
                                OriginalIndex = index,
                                CompressionRatio = (double)compressedTokens / chunkTokens,
                                RelevanceScore = score,
                                TokenCount = compressedTokens
                            });
                            currentTokens += compressedTokens;
                        }
                    }
                    break;
                }

                compressedResults.Add(new CompressedChunk
                {
                    Content = chunk,
                    OriginalIndex = index,
                    CompressionRatio = 1.0,
                    RelevanceScore = score,
                    TokenCount = chunkTokens
                });
                currentTokens += chunkTokens;
            }

            _logger.LogInformation(
                "Compressed {OriginalCount} chunks ({OriginalTokens} tokens) to {CompressedCount} chunks ({CompressedTokens} tokens)",
                chunks.Count, chunks.Sum(c => EstimateTokenCount(c)), compressedResults.Count, currentTokens);

            return compressedResults;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error compressing chunks");
            // Return original chunks on error
            return chunks.Select((c, i) => new CompressedChunk
            {
                Content = c,
                OriginalIndex = i,
                CompressionRatio = 1.0,
                RelevanceScore = 1.0,
                TokenCount = EstimateTokenCount(c)
            }).ToList();
        }
    }

    /// <inheritdoc/>
    public async Task<string> CompressTextAsync(string query, string text, int maxTokens)
    {
        try
        {
            if (!_config.Enabled || EstimateTokenCount(text) <= maxTokens)
                return text;

            _logger.LogDebug("Compressing text from ~{OriginalTokens} to {MaxTokens} tokens",
                EstimateTokenCount(text), maxTokens);

            if (_config.UseExtractiveSummarization)
            {
                // Extract relevant sentences
                var targetSentences = Math.Max(1, maxTokens / 50); // Assume ~50 tokens per sentence
                var relevantSentences = await ExtractRelevantSentencesAsync(query, text, (int)targetSentences);
                
                var compressed = string.Join(" ", relevantSentences);
                
                // If still too long, truncate
                if (EstimateTokenCount(compressed) > maxTokens)
                {
                    var maxChars = (int)(maxTokens / TOKENS_PER_CHAR);
                    compressed = compressed.Substring(0, Math.Min(compressed.Length, maxChars));
                }
                
                return compressed;
            }
            else
            {
                // Simple truncation as fallback
                var maxChars = (int)(maxTokens / TOKENS_PER_CHAR);
                return text.Substring(0, Math.Min(text.Length, maxChars));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error compressing text");
            // Fallback to simple truncation
            var maxChars = (int)(maxTokens / TOKENS_PER_CHAR);
            return text.Substring(0, Math.Min(text.Length, maxChars));
        }
    }

    /// <inheritdoc/>
    public async Task<List<string>> DeduplicateChunksAsync(
        List<string> chunks,
        double similarityThreshold = 0.85)
    {
        try
        {
            if (chunks.Count <= 1)
                return chunks;

            _logger.LogDebug("Deduplicating {Count} chunks with threshold {Threshold:F2}",
                chunks.Count, similarityThreshold);

            // Generate embeddings for all chunks
            var embeddings = new List<float[]?>();
            foreach (var chunk in chunks)
            {
                var embedding = await _embeddingService.GenerateEmbeddingAsync(chunk);
                embeddings.Add(embedding);
            }

            // Find duplicates using cosine similarity
            var uniqueChunks = new List<string>();
            var uniqueEmbeddings = new List<float[]>();

            for (int i = 0; i < chunks.Count; i++)
            {
                if (embeddings[i] == null)
                {
                    uniqueChunks.Add(chunks[i]);
                    continue;
                }

                bool isDuplicate = false;
                foreach (var existingEmbedding in uniqueEmbeddings)
                {
                    var similarity = VectorMathHelper.CosineSimilarity(embeddings[i]!, existingEmbedding);
                    if (similarity >= similarityThreshold)
                    {
                        isDuplicate = true;
                        break;
                    }
                }

                if (!isDuplicate)
                {
                    uniqueChunks.Add(chunks[i]);
                    uniqueEmbeddings.Add(embeddings[i]!);
                }
            }

            _logger.LogInformation("Deduplicated {OriginalCount} chunks to {UniqueCount} unique chunks",
                chunks.Count, uniqueChunks.Count);

            return uniqueChunks;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deduplicating chunks");
            return chunks; // Return original on error
        }
    }

    /// <inheritdoc/>
    public int EstimateTokenCount(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        // Rough estimation: ~0.25 tokens per character for English text
        // This is conservative (actual is closer to 0.3-0.35 for English)
        return (int)(text.Length * TOKENS_PER_CHAR);
    }

    /// <inheritdoc/>
    public async Task<List<string>> ExtractRelevantSentencesAsync(
        string query,
        string text,
        int maxSentences)
    {
        try
        {
            // Split text into sentences
            var sentences = SplitIntoSentences(text);
            
            if (sentences.Count <= maxSentences)
                return sentences;

            _logger.LogDebug("Extracting {MaxSentences} most relevant sentences from {TotalSentences} sentences",
                maxSentences, sentences.Count);

            // Calculate relevance score for each sentence
            var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query);
            if (queryEmbedding == null)
            {
                // Fallback: return first N sentences
                return sentences.Take(maxSentences).ToList();
            }

            var sentencesWithScores = new List<(string sentence, double score)>();
            foreach (var sentence in sentences)
            {
                var sentenceEmbedding = await _embeddingService.GenerateEmbeddingAsync(sentence);
                if (sentenceEmbedding != null)
                {
                    var similarity = VectorMathHelper.CosineSimilarity(queryEmbedding, sentenceEmbedding);
                    sentencesWithScores.Add((sentence, similarity));
                }
                else
                {
                    sentencesWithScores.Add((sentence, 0.0));
                }
            }

            // Return top N sentences by relevance score
            var topSentences = sentencesWithScores
                .OrderByDescending(s => s.score)
                .Take(maxSentences)
                .Select(s => s.sentence)
                .ToList();

            return topSentences;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting relevant sentences");
            // Fallback: return first N sentences
            var sentences = SplitIntoSentences(text);
            return sentences.Take(maxSentences).ToList();
        }
    }

    /// <summary>
    /// Calculate relevance score between query and text using embeddings
    /// </summary>
    private async Task<double> CalculateRelevanceScoreAsync(string query, string text)
    {
        try
        {
            var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query);
            var textEmbedding = await _embeddingService.GenerateEmbeddingAsync(text);

            if (queryEmbedding == null || textEmbedding == null)
                return 0.5; // Default medium relevance

            return VectorMathHelper.CosineSimilarity(queryEmbedding, textEmbedding);
        }
        catch
        {
            return 0.5;
        }
    }

    /// <summary>
    /// Split text into sentences
    /// </summary>
    private List<string> SplitIntoSentences(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new List<string>();

        // Simple sentence splitter - can be improved with NLP library
        var sentenceRegex = new Regex(@"[.!?]+\s+", RegexOptions.Compiled);
        var sentences = sentenceRegex.Split(text)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .ToList();

        return sentences;
    }
}
