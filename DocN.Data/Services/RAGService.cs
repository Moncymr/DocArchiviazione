using Azure.AI.OpenAI;
using Azure;
using DocN.Data.Models;
using System.Text;
using OpenAI.Chat;
using System.ClientModel;
using Microsoft.EntityFrameworkCore;

namespace DocN.Data.Services;

/// <summary>
/// Interfaccia per il servizio RAG (Retrieval-Augmented Generation)
/// Fornisce funzionalità per generare risposte basate su documenti rilevanti
/// </summary>
public interface IRAGService
{
    /// <summary>
    /// Genera una risposta AI basata sulla query e sui documenti rilevanti forniti
    /// </summary>
    /// <param name="query">Domanda o query dell'utente</param>
    /// <param name="relevantDocuments">Lista di documenti rilevanti da utilizzare come contesto</param>
    /// <returns>Risposta generata dall'AI basata sui documenti forniti</returns>
    Task<string> GenerateResponseAsync(string query, List<Document> relevantDocuments);
}

/// <summary>
/// Implementazione del servizio RAG utilizzando Azure OpenAI
/// Gestisce la generazione di risposte basate su documenti attraverso l'integrazione con Azure OpenAI
/// </summary>
public class RAGService : IRAGService, IDisposable
{
    private readonly ApplicationDbContext _context;
    private ChatClient? _client;
    private readonly SemaphoreSlim _clientInitLock = new SemaphoreSlim(1, 1);
    private AIConfiguration? _cachedSystemPromptConfig;
    private bool _disposed = false;

    /// <summary>
    /// Inizializza una nuova istanza di RAGService
    /// </summary>
    /// <param name="context">Contesto del database per accedere alle configurazioni AI</param>
    /// <exception cref="ArgumentNullException">Lanciato se context è null</exception>
    public RAGService(ApplicationDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        // Client initialization moved to async method to avoid blocking constructor
    }

    /// <summary>
    /// Inizializza il client Azure OpenAI utilizzando la configurazione attiva dal database
    /// Carica endpoint, chiave API e deployment name dalla configurazione AI attiva
    /// </summary>
    /// <remarks>
    /// OTTIMIZZAZIONE: Metodo asincrono con AsNoTracking per evitare overhead di change tracking
    /// Usa semaphore per thread-safety durante inizializzazione lazy
    /// </remarks>
    private async Task<ChatClient?> GetOrCreateClientAsync()
    {
        if (_client != null)
            return _client;

        await _clientInitLock.WaitAsync();
        try
        {
            // Double-check pattern per evitare race condition
            if (_client != null)
                return _client;

            var config = await _context.AIConfigurations
                .AsNoTracking() // OTTIMIZZAZIONE: Read-only query
                .FirstOrDefaultAsync(c => c.IsActive);

            if (config != null && !string.IsNullOrEmpty(config.AzureOpenAIEndpoint) && !string.IsNullOrEmpty(config.AzureOpenAIKey))
            {
                // Cache configuration for system prompt reuse
                _cachedSystemPromptConfig = config;
                
                var azureClient = new AzureOpenAIClient(new Uri(config.AzureOpenAIEndpoint), new AzureKeyCredential(config.AzureOpenAIKey));
                _client = azureClient.GetChatClient(config.ChatDeploymentName ?? "gpt-4");
            }

            return _client;
        }
        finally
        {
            _clientInitLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<string> GenerateResponseAsync(string query, List<Document> relevantDocuments)
    {
        // OTTIMIZZAZIONE: Inizializzazione lazy asincrona del client
        var client = await GetOrCreateClientAsync();
        if (client == null)
            return "AI service not configured.";

        try
        {
            // OTTIMIZZAZIONE: Usa configurazione cached se disponibile, altrimenti query DB
            var systemPrompt = _cachedSystemPromptConfig?.SystemPrompt;
            if (string.IsNullOrEmpty(systemPrompt))
            {
                var config = await _context.AIConfigurations
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.IsActive);
                systemPrompt = config?.SystemPrompt;
            }
            systemPrompt ??= "You are a helpful assistant that answers questions based on provided documents.";

            // Build context from relevant documents
            var contextBuilder = new StringBuilder();
            contextBuilder.AppendLine("Use the following documents to answer the question:");
            contextBuilder.AppendLine();

            foreach (var doc in relevantDocuments)
            {
                contextBuilder.AppendLine($"Document: {doc.FileName}");
                contextBuilder.AppendLine($"Category: {doc.ActualCategory ?? doc.SuggestedCategory}");
                contextBuilder.AppendLine($"Content: {TruncateText(doc.ExtractedText, 1000)}");
                contextBuilder.AppendLine();
            }

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(contextBuilder.ToString()),
                new UserChatMessage(query)
            };

            var response = await client.CompleteChatAsync(messages);
            return response.Value.Content[0].Text;
        }
        catch (Exception ex)
        {
            return $"Error generating response: {ex.Message}";
        }
    }

    /// <summary>
    /// Tronca il testo alla lunghezza massima specificata
    /// Aggiunge "..." alla fine se il testo è stato troncato
    /// </summary>
    /// <param name="text">Testo da troncare</param>
    /// <param name="maxLength">Lunghezza massima desiderata</param>
    /// <returns>Testo troncato con "..." se necessario, altrimenti il testo originale</returns>
    private string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;

        return text.Substring(0, maxLength) + "...";
    }

    /// <summary>
    /// Dispose pattern implementation per rilasciare SemaphoreSlim
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Protected dispose pattern per pulizia risorse
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _clientInitLock?.Dispose();
            }
            _disposed = true;
        }
    }
}
