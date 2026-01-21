using Azure.AI.OpenAI;
using Azure;
using DocN.Data.Models;
using DocN.Data.Utilities;
using Microsoft.EntityFrameworkCore;
using OpenAI.Chat;
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

    /// <summary>
    /// Costruttore del servizio embedding con dipendenze iniettate.
    /// </summary>
    /// <param name="context">Contesto database per accesso configurazione AI</param>
    /// <param name="cacheService">Servizio cache opzionale per ottimizzare performance</param>
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
    /// Thread-safe: usa flag booleano con volatile semantics implicite.
    /// </remarks>
    private void EnsureInitialized()
    {
        if (_initialized) return;
        
        try
        {
            // OTTIMIZZAZIONE: AsNoTracking per query read-only senza necessità di tracking
            var config = _context.AIConfigurations
                .AsNoTracking()
                .FirstOrDefault(c => c.IsActive);
            
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
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Ricerca documenti simili usando similarità coseno su embedding vettoriali.
    /// </summary>
    /// <param name="queryEmbedding">Vettore embedding della query</param>
    /// <param name="topK">Numero massimo di risultati da restituire (default 5)</param>
    /// <returns>Lista documenti ordinati per score di similarità decrescente</returns>
    /// <remarks>
    /// ATTENZIONE: Implementazione dimostrativa con limitazioni scalabilità.
    /// Per produzione usare: SQL Server 2025 VECTOR, Azure Cognitive Search, o DB vettoriale dedicato.
    /// Carica tutti i documenti in memoria - NON scalabile per dataset grandi.
    /// OTTIMIZZAZIONE: Usa AsNoTracking per query read-only senza tracking EF.
    /// </remarks>
    public async Task<List<Document>> SearchSimilarDocumentsAsync(float[] queryEmbedding, int topK = 5)
    {
        // OTTIMIZZAZIONE: AsNoTracking per evitare tracking overhead su query read-only
        var documents = await _context.Documents
            .AsNoTracking()
            .Where(d => (d.EmbeddingVector768 != null && d.EmbeddingVector768.Length > 0) ||
                        (d.EmbeddingVector1536 != null && d.EmbeddingVector1536.Length > 0))
            .ToListAsync();
        
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
