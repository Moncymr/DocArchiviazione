using DocN.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace DocN.Data.Services.Agents;

/// <summary>
/// Agent responsible for retrieving relevant documents and chunks
/// </summary>
public class RetrievalAgent : IRetrievalAgent
{
    private readonly ApplicationDbContext _context;
    private readonly IHybridSearchService _searchService;
    private readonly IEmbeddingService _embeddingService;
    
    // Configuration constants for chunk retrieval performance tuning
    private const int MaxChunksToEvaluate = 1000;  // Maximum chunks to load into memory
    private const int ChunkOverfetchMultiplier = 100;  // Multiplier for topK to ensure good recall

    public string Name => "RetrievalAgent";
    public string Description => "Retrieves relevant documents and document chunks using hybrid search";

    public RetrievalAgent(
        ApplicationDbContext context,
        IHybridSearchService searchService,
        IEmbeddingService embeddingService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _searchService = searchService ?? throw new ArgumentNullException(nameof(searchService));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
    }

    public async Task<List<Document>> RetrieveAsync(string query, string? userId = null, int topK = 5)
    {
        var options = new SearchOptions
        {
            TopK = topK,
            MinSimilarity = 0.7,
            OwnerId = userId
        };

        var results = await _searchService.SearchAsync(query, options);
        return results.Select(r => r.Document).ToList();
    }

    public async Task<List<DocumentChunk>> RetrieveChunksAsync(string query, string? userId = null, int topK = 10)
    {
        // Generate query embedding
        var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query);
        if (queryEmbedding == null)
        {
            return new List<DocumentChunk>();
        }

        // Get chunks with embeddings, with a reasonable limit to prevent loading entire database
        // Query the actual mapped fields: ChunkEmbedding768 or ChunkEmbedding1536
        var chunksQuery = _context.DocumentChunks
            .Include(c => c.Document)
            .Where(c => c.ChunkEmbedding768 != null || c.ChunkEmbedding1536 != null);

        // Filter by user ownership if specified
        if (!string.IsNullOrEmpty(userId))
        {
            chunksQuery = chunksQuery.Where(c => c.Document!.OwnerId == userId);
        }

        // Apply reasonable limit to prevent loading too many chunks into memory
        // We retrieve more than topK to ensure we have enough candidates after similarity filtering
        // Order by Id for consistent ordering (could be optimized with created timestamp if available)
        var maxChunksToEvaluate = Math.Min(MaxChunksToEvaluate, topK * ChunkOverfetchMultiplier);
        var chunks = await chunksQuery
            .OrderByDescending(c => c.Id)  // Get most recent chunks first for better relevance
            .Take(maxChunksToEvaluate)
            .ToListAsync();

        // Calculate similarity scores
        var scoredChunks = new List<(DocumentChunk chunk, double score)>();
        foreach (var chunk in chunks)
        {
            if (chunk.ChunkEmbedding == null) continue;

            var similarity = CosineSimilarity(queryEmbedding, chunk.ChunkEmbedding);
            if (similarity >= 0.7)
            {
                scoredChunks.Add((chunk, similarity));
            }
        }

        // Return top K chunks sorted by similarity
        return scoredChunks
            .OrderByDescending(x => x.score)
            .Take(topK)
            .Select(x => x.chunk)
            .ToList();
    }

    private double CosineSimilarity(float[] vector1, float[] vector2)
    {
        if (vector1.Length != vector2.Length)
            return 0;

        double dotProduct = 0;
        double magnitude1 = 0;
        double magnitude2 = 0;

        for (int i = 0; i < vector1.Length; i++)
        {
            dotProduct += vector1[i] * vector2[i];
            magnitude1 += vector1[i] * vector1[i];
            magnitude2 += vector2[i] * vector2[i];
        }

        magnitude1 = Math.Sqrt(magnitude1);
        magnitude2 = Math.Sqrt(magnitude2);

        if (magnitude1 == 0 || magnitude2 == 0)
            return 0;

        return dotProduct / (magnitude1 * magnitude2);
    }
}
