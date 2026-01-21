using DocN.Core.Interfaces;
using DocN.Data.Models;
using DocN.Data.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DocN.Data.Services;

/// <summary>
/// No-op implementation of ISemanticRAGService for when AI services are not configured
/// Provides similarity search capabilities using existing embeddings in the database
/// </summary>
public class NoOpSemanticRAGService : ISemanticRAGService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<NoOpSemanticRAGService> _logger;

    public NoOpSemanticRAGService(
        ApplicationDbContext context,
        ILogger<NoOpSemanticRAGService> logger)
    {
        _context = context;
        _logger = logger;
    }
    public Task<SemanticRAGResponse> GenerateResponseAsync(
        string query, 
        string userId, 
        int? conversationId = null, 
        List<int>? specificDocumentIds = null, 
        int topK = 5)
    {
        return Task.FromResult(new SemanticRAGResponse
        {
            Answer = "AI services are not configured. Please configure Azure OpenAI or OpenAI in appsettings.json.",
            SourceDocuments = new List<RelevantDocumentResult>(),
            Metadata = new Dictionary<string, object>
            {
                { "error", "AI services not configured" }
            }
        });
    }

    public async IAsyncEnumerable<string> GenerateStreamingResponseAsync(
        string query, 
        string userId, 
        int? conversationId = null, 
        List<int>? specificDocumentIds = null)
    {
        yield return "AI services are not configured. Please configure Azure OpenAI or OpenAI in appsettings.json.";
        await Task.CompletedTask;
    }

    public Task<List<RelevantDocumentResult>> SearchDocumentsAsync(
        string query, 
        string userId, 
        int topK = 10, 
        double minSimilarity = 0.7)
    {
        // Return empty list when AI services are not configured
        return Task.FromResult(new List<RelevantDocumentResult>());
    }

    public async Task<List<RelevantDocumentResult>> SearchDocumentsWithEmbeddingAsync(
        float[] queryEmbedding,
        string userId,
        int topK = 10,
        double minSimilarity = 0.7)
    {
        try
        {
            _logger.LogDebug("Searching documents with pre-generated embedding for user: {UserId} (NoOp mode)", userId);

            if (queryEmbedding == null || queryEmbedding.Length == 0)
            {
                _logger.LogWarning("Query embedding is null or empty");
                return new List<RelevantDocumentResult>();
            }

            // Get all documents with embeddings for the user
            // Query the actual mapped fields: EmbeddingVector768 or EmbeddingVector1536
            var documents = await _context.Documents
                .Where(d => d.OwnerId == userId && (d.EmbeddingVector768 != null || d.EmbeddingVector1536 != null))
                .ToListAsync();

            _logger.LogInformation("Found {Count} documents with embeddings for user {UserId}", documents.Count, userId);
            
            // Calculate similarity scores for documents
            var scoredDocs = new List<(Document doc, double score)>();
            foreach (var doc in documents)
            {
                // Use the EmbeddingVector property getter which returns the populated field
                var docEmbedding = doc.EmbeddingVector;
                if (docEmbedding == null) continue;

                var similarity = VectorMathHelper.CosineSimilarity(queryEmbedding, docEmbedding);
                if (similarity >= minSimilarity)
                {
                    scoredDocs.Add((doc, similarity));
                }
            }

            _logger.LogInformation("Found {Count} documents above similarity threshold {Threshold:P0}", scoredDocs.Count, minSimilarity);

            // Get chunks for better precision
            // Query the actual mapped fields: ChunkEmbedding768 or ChunkEmbedding1536
            var chunks = await _context.DocumentChunks
                .Include(c => c.Document)
                .Where(c => c.Document!.OwnerId == userId && (c.ChunkEmbedding768 != null || c.ChunkEmbedding1536 != null))
                .ToListAsync();

            var scoredChunks = new List<(DocumentChunk chunk, double score)>();
            foreach (var chunk in chunks)
            {
                // Use the ChunkEmbedding property getter which returns the populated field
                var chunkEmbedding = chunk.ChunkEmbedding;
                if (chunkEmbedding == null) continue;

                var similarity = VectorMathHelper.CosineSimilarity(queryEmbedding, chunkEmbedding);
                if (similarity >= minSimilarity)
                {
                    scoredChunks.Add((chunk, similarity));
                }
            }

            // Combine document-level and chunk-level results
            var results = new List<RelevantDocumentResult>();
            
            // Add chunk-based results (higher priority)
            var topChunks = scoredChunks.OrderByDescending(x => x.score).Take(topK).ToList();
            var existingDocIds = new HashSet<int>();
            
            foreach (var (chunk, score) in topChunks)
            {
                if (chunk.Document == null) continue;

                results.Add(new RelevantDocumentResult
                {
                    DocumentId = chunk.DocumentId,
                    FileName = chunk.Document.FileName,
                    Category = chunk.Document.ActualCategory,
                    SimilarityScore = score,
                    RelevantChunk = chunk.ChunkText,
                    ChunkIndex = chunk.ChunkIndex
                });
                existingDocIds.Add(chunk.DocumentId);
            }

            // Add document-level results if we don't have enough chunks
            if (results.Count < topK)
            {
                foreach (var (doc, score) in scoredDocs.OrderByDescending(x => x.score))
                {
                    // Stop if we've reached topK results
                    if (results.Count >= topK)
                        break;
                        
                    // Avoid duplicates
                    if (existingDocIds.Contains(doc.Id))
                        continue;

                    results.Add(new RelevantDocumentResult
                    {
                        DocumentId = doc.Id,
                        FileName = doc.FileName,
                        Category = doc.ActualCategory,
                        SimilarityScore = score,
                        ExtractedText = doc.ExtractedText
                    });
                    existingDocIds.Add(doc.Id);
                }
            }

            _logger.LogDebug("Returning {Count} total results", results.Count);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching documents with embedding for user: {UserId}", userId);
            return new List<RelevantDocumentResult>();
        }
    }

}
