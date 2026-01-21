using Azure.AI.OpenAI;
using Azure;
using DocN.Data.Models;
using DocN.Data.Utilities;
using OpenAI.Chat;
using OpenAI.Embeddings;
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
    private readonly ILogger<EmbeddingService> _logger;
    private EmbeddingClient? _client;
    private bool _initialized = false;

    public EmbeddingService(ApplicationDbContext context, ILogger<EmbeddingService> logger, ICacheService? cacheService = null)
    {
        _context = context;
        _logger = logger;
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
            _logger.LogDebug("Initializing EmbeddingService - loading configuration from database");
            
            var config = _context.AIConfigurations.FirstOrDefault(c => c.IsActive);
            if (config == null)
            {
                _logger.LogWarning("No active AI configuration found in database. Embedding generation will not be available. Please configure an active AIConfiguration record with Azure OpenAI credentials.");
                _initialized = true;
                return;
            }
            
            if (string.IsNullOrEmpty(config.AzureOpenAIEndpoint))
            {
                _logger.LogWarning("Azure OpenAI Endpoint is not configured in the active AIConfiguration. Embedding generation will not be available.");
                _initialized = true;
                return;
            }
            
            if (string.IsNullOrEmpty(config.AzureOpenAIKey))
            {
                _logger.LogWarning("Azure OpenAI API Key is not configured in the active AIConfiguration. Embedding generation will not be available.");
                _initialized = true;
                return;
            }
            
            var deploymentName = config.EmbeddingDeploymentName ?? "text-embedding-ada-002";
            _logger.LogInformation("Initializing Azure OpenAI Embedding client with endpoint: {Endpoint}, deployment: {Deployment}", 
                config.AzureOpenAIEndpoint, deploymentName);
            
            var azureClient = new AzureOpenAIClient(new Uri(config.AzureOpenAIEndpoint), new AzureKeyCredential(config.AzureOpenAIKey));
            _client = azureClient.GetEmbeddingClient(deploymentName);
            
            _logger.LogInformation("EmbeddingService initialized successfully with Azure OpenAI");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize EmbeddingService. Embedding generation will not be available. Error: {ErrorMessage}", ex.Message);
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
        {
            _logger.LogWarning("Cannot generate embedding: Azure OpenAI client is not initialized. Please ensure an active AIConfiguration is set up with valid Azure OpenAI credentials.");
            return null;
        }

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
            _logger.LogDebug("Generating embedding for text (length: {Length})", text.Length);
            var response = await _client.GenerateEmbeddingAsync(text);
            var embedding = response.Value.ToFloats().ToArray();
            
            _logger.LogDebug("Embedding generated successfully (dimensions: {Dimensions})", embedding.Length);
            
            // Cache the result if caching is available
            if (_cacheService != null && embedding != null)
            {
                await _cacheService.SetCachedEmbeddingAsync(text, embedding);
            }
            
            return embedding;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate embedding. Error: {ErrorMessage}. This may be due to invalid API credentials, deployment name, or Azure OpenAI service issues.", ex.Message);
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
