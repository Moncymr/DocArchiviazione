using Azure;
using Azure.AI.OpenAI;
using DocN.Data.Models;
using DocN.Data.Utilities;
using OpenAI.Embeddings;

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
/// Implementazione servizio embedding con Azure OpenAI e caching opzionale.
/// </summary>
/// <remarks>
/// Scopo: Generare embedding vettoriali per ricerca semantica con caching per performance.
/// Provider: Azure OpenAI (text-embedding-ada-002 o configurato)
/// Output: Float array dimensioni 1536 (o specifiche del modello)
/// </remarks>
public class EmbeddingService : IEmbeddingService
{
    private readonly ApplicationDbContext _context;
    private readonly ICacheService? _cacheService;
    private EmbeddingClient? _client;
    private bool _initialized = false;

    public EmbeddingService(ApplicationDbContext context, ICacheService? cacheService = null)
    {
        _context = context;
        _cacheService = cacheService;
    }

    /// <summary>
    /// Inizializza client Azure OpenAI lazy-loading dalla configurazione database.
    /// </summary>
    /// <remarks>
    /// Chiamato al primo utilizzo. Fallisce silenziosamente se DB non pronto.
    /// </remarks>
    private void EnsureInitialized()
    {
        if (_initialized) return;

        try
        {
            var config = _context.AIConfigurations.FirstOrDefault(c => c.IsActive);
            if (config != null && !string.IsNullOrEmpty(config.AzureOpenAIEndpoint) && !string.IsNullOrEmpty(config.AzureOpenAIKey))
            {
                var azureClient = new AzureOpenAIClient(new Uri(config.AzureOpenAIEndpoint), new AzureKeyCredential(config.AzureOpenAIKey));
                _client = azureClient.GetEmbeddingClient(config.EmbeddingDeploymentName ?? "text-embedding-ada-002");
            }
        }
        catch
        {
            // Initialization can fail if database doesn't exist yet or AIConfigurations table is empty
            // This is OK - the service will work without AI features
        }
        finally
        {
            _initialized = true;
        }
    }

    /// <summary>
    /// Genera embedding vettoriale per testo con caching automatico.
    /// </summary>
    /// <param name="text">Testo da convertire (max ~8000 tokens)</param>
    /// <returns>Float array embedding o null se provider non configurato</returns>
    /// <remarks>
    /// Scopo: Convertire testo in rappresentazione vettoriale per ricerca semantica.
    /// Cache: Controlla cache prima di chiamare API (risparmio costi e latency).
    /// Output: Float[] dimensioni dipendenti da modello (1536 per ada-002).
    /// </remarks>
    public async Task<float[]?> GenerateEmbeddingAsync(string text)
    {
        EnsureInitialized();

        if (_client == null)
            return null;

        // Check cache first if available
        if (_cacheService != null)
        {
            var cachedEmbedding = await _cacheService.GetCachedEmbeddingAsync(text);
            if (cachedEmbedding != null)
                return cachedEmbedding;
        }

        try
        {
            var response = await _client.GenerateEmbeddingAsync(text);
            var embedding = response.Value.ToFloats().ToArray();

            // Cache the result if caching is available
            if (_cacheService != null && embedding != null)
            {
                await _cacheService.SetCachedEmbeddingAsync(text, embedding);
            }

            return embedding;
        }
        catch (Exception ex)
        {
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
