using DocN.Data.Models;
using DocN.Data.Utilities;
using Microsoft.Extensions.Logging;

namespace DocN.Data.Services;

/// <summary>
/// Interfaccia per servizio generazione embedding e ricerca semantica.
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// Genera embedding vettoriale per testo usando provider AI configurato.
    /// </summary>
    /// <param name="text">Testo da convertire in embedding</param>
    /// <returns>Array float rappresentante embedding, null se provider non disponibile</returns>
    Task<float[]?> GenerateEmbeddingAsync(string text);
    
    /// <summary>
    /// Ricerca documenti simili basandosi su embedding vettoriale query.
    /// </summary>
    /// <param name="queryEmbedding">Embedding vettoriale della query</param>
    /// <param name="topK">Numero massimo risultati da restituire</param>
    /// <returns>Lista documenti ordinati per similarità (cosine similarity)</returns>
    Task<List<Document>> SearchSimilarDocumentsAsync(float[] queryEmbedding, int topK = 5);
}

/// <summary>
/// Implementazione servizio embedding con supporto multi-provider e caching opzionale.
/// </summary>
/// <remarks>
/// Scopo: Generare embedding vettoriali per ricerca semantica con caching per performance.
/// Provider: Multi-provider (Gemini, OpenAI, Azure OpenAI, Ollama, Groq) con fallback automatico
/// Output: Float array dimensioni dipendenti dal provider configurato
/// </remarks>
public class EmbeddingService : IEmbeddingService
{
    private readonly ApplicationDbContext _context;
    private readonly ICacheService? _cacheService;
    private readonly ILogger<EmbeddingService> _logger;
    private readonly IMultiProviderAIService _multiProviderAIService;

    public EmbeddingService(
        ApplicationDbContext context, 
        ILogger<EmbeddingService> logger, 
        IMultiProviderAIService multiProviderAIService,
        ICacheService? cacheService = null)
    {
        _context = context;
        _logger = logger;
        _multiProviderAIService = multiProviderAIService;
        _cacheService = cacheService;
    }

    /// <summary>
    /// Genera embedding vettoriale per testo con caching automatico e supporto multi-provider.
    /// </summary>
    /// <param name="text">Testo da convertire (max ~8000 tokens)</param>
    /// <returns>Float array embedding o null se provider non configurato</returns>
    /// <remarks>
    /// Scopo: Convertire testo in rappresentazione vettoriale per ricerca semantica.
    /// Cache: Controlla cache prima di chiamare API (risparmio costi e latency).
    /// Provider: Utilizza MultiProviderAIService per supporto completo di tutti i provider configurati.
    /// Output: Float[] dimensioni dipendenti dal provider (768 per Gemini, 1536 per OpenAI/Azure, ecc.).
    /// </remarks>
    public async Task<float[]?> GenerateEmbeddingAsync(string text)
    {
        // Check cache first if available
        if (_cacheService != null)
        {
            var cachedEmbedding = await _cacheService.GetCachedEmbeddingAsync(text);
            if (cachedEmbedding != null)
            {
                _logger.LogDebug("Embedding retrieved from cache for text (length: {Length})", text.Length);
                return cachedEmbedding;
            }
        }

        try
        {
            _logger.LogDebug("Generating embedding for text (length: {Length}) using multi-provider service", text.Length);
            
            // Delegate to MultiProviderAIService which handles all provider types with fallback
            var embedding = await _multiProviderAIService.GenerateEmbeddingAsync(text);
            
            if (embedding != null)
            {
                _logger.LogDebug("Embedding generated successfully (dimensions: {Dimensions})", embedding.Length);
                
                // Cache the result if caching is available
                if (_cacheService != null)
                {
                    await _cacheService.SetCachedEmbeddingAsync(text, embedding);
                }
            }
            else
            {
                _logger.LogWarning("Embedding generation returned null. Check MultiProviderAIService logs for details.");
            }
            
            return embedding;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate embedding. Error: {ErrorMessage}. Check that an active AIConfiguration is configured with valid credentials.", ex.Message);
            return null;
        }
    }

    public async Task<List<Document>> SearchSimilarDocumentsAsync(float[] queryEmbedding, int topK = 5)
    {
        // WARNING: This is a simplified version for demonstration purposes only
        // In production, you should use:
        // 1. SQL Server 2025 native vector search with VECTOR data type
        // 2. Azure Cognitive Search with vector search
        // 3. A dedicated vector database like Pinecone, Weaviate, or Qdrant
        // Loading all documents into memory is NOT scalable for large datasets
        // Query the actual mapped fields: EmbeddingVector768 or EmbeddingVector1536
        var documents = await Task.Run(() => _context.Documents
            .Where(d => (d.EmbeddingVector768 != null && d.EmbeddingVector768.Length > 0) ||
                        (d.EmbeddingVector1536 != null && d.EmbeddingVector1536.Length > 0))
            .ToList());
        
        var scoredDocuments = documents
            .Where(d => d.EmbeddingVector != null) // Use the property getter
            .Select(d => new
            {
                Document = d,
                Score = VectorMathHelper.CosineSimilarity(queryEmbedding, d.EmbeddingVector!)
            })
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .Select(x => x.Document)
            .ToList();

        return scoredDocuments;
    }
}
