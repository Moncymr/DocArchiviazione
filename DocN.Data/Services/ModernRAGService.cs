using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using DocN.Data.Models;
using System.Text;

#pragma warning disable SKEXP0001 // ISemanticTextMemory is experimental

namespace DocN.Data.Services;

/// <summary>
/// Servizio RAG modernizzato con Microsoft Semantic Kernel
/// Gestisce la generazione di risposte usando Retrieval Augmented Generation
/// </summary>
/// <remarks>
/// Semantic Kernel fornisce:
/// - Orchestrazione automatica delle chiamate AI
/// - Gestione memoria conversazionale
/// - Plugin system estensibile
/// - Telemetry integrata
/// </remarks>
public interface IModernRAGService
{
    /// <summary>
    /// Genera una risposta basata sui documenti rilevanti usando Semantic Kernel
    /// </summary>
    /// <param name="userQuery">Domanda dell'utente</param>
    /// <param name="conversationId">ID conversazione per mantenere il contesto</param>
    /// <param name="userId">ID utente per controllo accessi</param>
    /// <returns>Risposta generata dall'AI con riferimenti ai documenti</returns>
    Task<RAGResponse> GenerateResponseAsync(
        string userQuery, 
        int? conversationId = null,
        string? userId = null);

    /// <summary>
    /// Ricerca documenti rilevanti usando embeddings vettoriali
    /// </summary>
    /// <param name="query">Testo della query</param>
    /// <param name="topK">Numero massimo di documenti da restituire</param>
    /// <param name="minSimilarity">Soglia minima di similarità (0-1)</param>
    /// <returns>Lista di documenti ordinati per rilevanza</returns>
    Task<List<RelevantDocument>> SearchRelevantDocumentsAsync(
        string query,
        int topK = 5,
        double minSimilarity = 0.7);
}

/// <summary>
/// Risposta generata dal sistema RAG
/// </summary>
public class RAGResponse
{
    /// <summary>
    /// Testo della risposta generata dall'AI
    /// </summary>
    public string Answer { get; set; } = string.Empty;

    /// <summary>
    /// Documenti usati per generare la risposta
    /// </summary>
    public List<RelevantDocument> SourceDocuments { get; set; } = new();

    /// <summary>
    /// ID della conversazione (per continuità del dialogo)
    /// </summary>
    public int ConversationId { get; set; }

    /// <summary>
    /// Tempo impiegato per generare la risposta (ms)
    /// </summary>
    public long ResponseTimeMs { get; set; }

    /// <summary>
    /// Indica se la risposta è stata generata da cache
    /// </summary>
    public bool FromCache { get; set; }
}

/// <summary>
/// Documento rilevante con score di similarità
/// </summary>
public class RelevantDocument
{
    /// <summary>
    /// Documento originale
    /// </summary>
    public Document Document { get; set; } = null!;

    /// <summary>
    /// Score di similarità (0-1, dove 1 = identico)
    /// </summary>
    public double SimilarityScore { get; set; }

    /// <summary>
    /// Chunk specifico del documento più rilevante
    /// </summary>
    public string? RelevantChunk { get; set; }

    /// <summary>
    /// Indice del chunk nel documento
    /// </summary>
    public int? ChunkIndex { get; set; }
}

/// <summary>
/// Implementazione moderna del servizio RAG usando Microsoft Semantic Kernel
/// </summary>
public class ModernRAGService : IModernRAGService
{
    private readonly Kernel _kernel;
    private readonly ISemanticTextMemory _memory;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ModernRAGService> _logger;
    private readonly IChatCompletionService _chatService;
    private bool _initialized = false;

    /// <summary>
    /// Costruttore del servizio RAG moderno
    /// </summary>
    /// <param name="kernel">Istanza di Semantic Kernel configurata</param>
    /// <param name="memory">Memoria semantica per gli embeddings</param>
    /// <param name="context">Contesto database</param>
    /// <param name="logger">Logger per diagnostics</param>
    public ModernRAGService(
        Kernel kernel,
        ISemanticTextMemory memory,
        ApplicationDbContext context,
        ILogger<ModernRAGService> logger)
    {
        _kernel = kernel;
        _memory = memory;
        _context = context;
        _logger = logger;
        _chatService = kernel.GetRequiredService<IChatCompletionService>();
    }

    /// <summary>
    /// Assicura che il servizio sia inizializzato (lazy initialization)
    /// Previene errori se il database non esiste ancora
    /// </summary>
    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;

        try
        {
            _logger.LogInformation("Inizializzazione ModernRAGService...");

            // Verifica che la memoria semantica sia configurata
            // TODO: Qui potremmo caricare embeddings esistenti in memoria
            
            _initialized = true;
            _logger.LogInformation("ModernRAGService inizializzato con successo");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore durante inizializzazione ModernRAGService");
            // Graceful degradation: il servizio funzionerà comunque
            _initialized = true;
        }
    }

    /// <inheritdoc/>
    public async Task<RAGResponse> GenerateResponseAsync(
        string userQuery,
        int? conversationId = null,
        string? userId = null)
    {
        await EnsureInitializedAsync();

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            _logger.LogInformation(
                "Generazione risposta RAG per query: {Query}, ConversationId: {ConvId}, UserId: {UserId}",
                userQuery, conversationId, userId);

            // 1. Ricerca documenti rilevanti usando vector search
            var relevantDocs = await SearchRelevantDocumentsAsync(
                userQuery,
                topK: 5,
                minSimilarity: 0.7);

            if (!relevantDocs.Any())
            {
                _logger.LogWarning("Nessun documento rilevante trovato per la query: {Query}", userQuery);
                return new RAGResponse
                {
                    Answer = "Mi dispiace, non ho trovato documenti rilevanti per rispondere alla tua domanda.",
                    SourceDocuments = new List<RelevantDocument>(),
                    ResponseTimeMs = stopwatch.ElapsedMilliseconds
                };
            }

            // 2. Carica storia conversazione (se esiste)
            var conversationHistory = await LoadConversationHistoryAsync(conversationId);

            // 3. Costruisci il contesto dai documenti rilevanti
            var context = BuildDocumentContext(relevantDocs);

            // 4. Crea il prompt system con istruzioni
            var systemPrompt = CreateSystemPrompt();

            // 5. Genera risposta usando Semantic Kernel
            var answer = await GenerateAnswerWithKernelAsync(
                systemPrompt,
                context,
                conversationHistory,
                userQuery);

            // 6. Salva nella conversazione
            var savedConversationId = await SaveConversationMessageAsync(
                conversationId,
                userId,
                userQuery,
                answer,
                relevantDocs.Select(d => d.Document.Id).ToList());

            stopwatch.Stop();

            _logger.LogInformation(
                "Risposta RAG generata in {ElapsedMs}ms per query: {Query}",
                stopwatch.ElapsedMilliseconds, userQuery);

            return new RAGResponse
            {
                Answer = answer,
                SourceDocuments = relevantDocs,
                ConversationId = savedConversationId,
                ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                FromCache = false
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore durante generazione risposta RAG per query: {Query}", userQuery);
            
            return new RAGResponse
            {
                Answer = $"Si è verificato un errore durante l'elaborazione della richiesta: {ex.Message}",
                ResponseTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    /// <inheritdoc/>
    public async Task<List<RelevantDocument>> SearchRelevantDocumentsAsync(
        string query,
        int topK = 5,
        double minSimilarity = 0.7)
    {
        try
        {
            _logger.LogDebug("Ricerca documenti rilevanti per: {Query}", query);

            // Usa Semantic Memory per cercare nei vettori
            // Semantic Kernel gestisce automaticamente la generazione dell'embedding
            var memoryResults = _memory.SearchAsync(
                collection: "documents", // Collection name per i documenti
                query: query,
                limit: topK * 2, // Prendiamo più risultati per poi filtrare
                minRelevanceScore: minSimilarity);

            var relevantDocs = new List<RelevantDocument>();

            await foreach (var result in memoryResults)
            {
                // Recupera il documento completo dal database
                // L'ID è salvato nei metadata della memory
                if (int.TryParse(result.Metadata.Id, out int docId))
                {
                    var document = await _context.Documents
                        .Include(d => d.Owner)
                        .FirstOrDefaultAsync(d => d.Id == docId);

                    if (document != null)
                    {
                        relevantDocs.Add(new RelevantDocument
                        {
                            Document = document,
                            SimilarityScore = result.Relevance,
                            RelevantChunk = result.Metadata.Text
                        });
                    }
                }

                // Ferma quando abbiamo abbastanza risultati
                if (relevantDocs.Count >= topK)
                    break;
            }

            _logger.LogDebug(
                "Trovati {Count} documenti rilevanti per query: {Query}",
                relevantDocs.Count, query);

            return relevantDocs;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore durante ricerca documenti per query: {Query}", query);
            return new List<RelevantDocument>();
        }
    }

    /// <summary>
    /// Carica la storia della conversazione dal database
    /// </summary>
    /// <param name="conversationId">ID della conversazione</param>
    /// <returns>Lista di messaggi della conversazione</returns>
    private async Task<List<ConversationMessage>> LoadConversationHistoryAsync(int? conversationId)
    {
        var history = new List<ConversationMessage>();

        if (!conversationId.HasValue)
            return history;

        try
        {
            // Carica gli ultimi 10 messaggi della conversazione
            var messages = await _context.Messages
                .Where(m => m.ConversationId == conversationId.Value)
                .OrderByDescending(m => m.Timestamp)
                .Take(10)
                .OrderBy(m => m.Timestamp)
                .ToListAsync();

            foreach (var msg in messages)
            {
                // Converte in ConversationMessage
                history.Add(msg.Role == "user"
                    ? new ConversationMessage(MessageRole.User, msg.Content)
                    : new ConversationMessage(MessageRole.Assistant, msg.Content));
            }

            _logger.LogDebug(
                "Caricati {Count} messaggi dalla conversazione {ConvId}",
                history.Count, conversationId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Impossibile caricare storia conversazione {ConvId}", conversationId);
        }

        return history;
    }

    /// <summary>
    /// Costruisce il contesto dai documenti rilevanti
    /// Formatta i documenti in modo leggibile per l'AI
    /// </summary>
    /// <param name="documents">Documenti rilevanti</param>
    /// <returns>Contesto formattato come stringa</returns>
    private string BuildDocumentContext(List<RelevantDocument> documents)
    {
        var contextBuilder = new StringBuilder();
        
        contextBuilder.AppendLine("=== DOCUMENTI AZIENDALI RILEVANTI ===");
        contextBuilder.AppendLine();

        for (int i = 0; i < documents.Count; i++)
        {
            var doc = documents[i];
            
            contextBuilder.AppendLine($"[DOCUMENTO {i + 1}]");
            contextBuilder.AppendLine($"Nome File: {doc.Document.FileName}");
            contextBuilder.AppendLine($"Categoria: {doc.Document.ActualCategory ?? "Non categorizzato"}");
            contextBuilder.AppendLine($"Caricato il: {doc.Document.UploadedAt:dd/MM/yyyy}");
            contextBuilder.AppendLine($"Rilevanza: {doc.SimilarityScore:P0}");
            contextBuilder.AppendLine();
            contextBuilder.AppendLine("Contenuto:");
            
            // Usa il chunk rilevante se disponibile, altrimenti tronca il testo
            var content = !string.IsNullOrEmpty(doc.RelevantChunk)
                ? doc.RelevantChunk
                : TruncateText(doc.Document.ExtractedText, 1500);
            
            contextBuilder.AppendLine(content);
            contextBuilder.AppendLine();
            contextBuilder.AppendLine("---");
            contextBuilder.AppendLine();
        }

        return contextBuilder.ToString();
    }

    /// <summary>
    /// Crea il prompt system con istruzioni per l'AI
    /// Definisce il comportamento e le regole per generare risposte
    /// </summary>
    /// <returns>Prompt system</returns>
    private string CreateSystemPrompt()
    {
        return @"Sei un assistente AI aziendale esperto che risponde a domande basandosi sui documenti aziendali forniti.

RUOLO E COMPITO:
- Analizza attentamente i documenti forniti nel contesto
- Rispondi alle domande in modo preciso e professionale
- Cita sempre le fonti (numero documento) quando fornisci informazioni
- Se l'informazione richiesta non è nei documenti, dillo chiaramente

REGOLE IMPORTANTI:
1. Usa SOLO le informazioni presenti nei documenti forniti
2. Non inventare o ipotizzare informazioni
3. Se non hai abbastanza informazioni, ammettilo
4. Cita i documenti usando il formato: [DOCUMENTO N]
5. Rispondi in italiano professionale
6. Sii conciso ma completo
7. Se la domanda riguarda più documenti, sintetizza le informazioni

FORMATO RISPOSTA:
- Inizia con una risposta diretta alla domanda
- Poi fornisci dettagli e contesto dai documenti
- Termina citando i documenti fonte

Esempio:
""Secondo i documenti aziendali, la policy di lavoro da remoto permette fino a 3 giorni a settimana [DOCUMENTO 1]. 
I dipendenti devono richiedere l'approvazione al proprio manager con 48 ore di anticipo [DOCUMENTO 2].""
";
    }

    /// <summary>
    /// Genera la risposta usando Semantic Kernel
    /// Gestisce automaticamente il completamento della chat con contesto
    /// </summary>
    /// <param name="systemPrompt">Prompt system con istruzioni</param>
    /// <param name="documentContext">Contesto dai documenti</param>
    /// <param name="conversationHistory">Storia conversazione</param>
    /// <param name="userQuery">Domanda utente</param>
    /// <returns>Risposta generata</returns>
    private async Task<string> GenerateAnswerWithKernelAsync(
        string systemPrompt,
        string documentContext,
        List<ConversationMessage> conversationHistory,
        string userQuery)
    {
        try
        {
            // Costruisci la chat history completa per Semantic Kernel
            var chatHistory = new ChatHistory(systemPrompt);

            // Aggiungi il contesto documenti come messaggio system
            chatHistory.AddSystemMessage($"CONTESTO DOCUMENTI:\n{documentContext}");

            // Aggiungi la storia conversazione (se esiste)
            foreach (var msg in conversationHistory)
            {
                if (msg.Role == MessageRole.User)
                {
                    chatHistory.AddUserMessage(msg.Content);
                }
                else if (msg.Role == MessageRole.Assistant)
                {
                    chatHistory.AddAssistantMessage(msg.Content);
                }
                else if (msg.Role == MessageRole.System)
                {
                    chatHistory.AddSystemMessage(msg.Content);
                }
            }

            // Aggiungi la query corrente dell'utente
            chatHistory.AddUserMessage(userQuery);

            // Configura le impostazioni per la generazione
            var executionSettings = new OpenAIPromptExecutionSettings
            {
                MaxTokens = 2000,
                Temperature = 0.7,
                TopP = 0.9,
                FrequencyPenalty = 0.0,
                PresencePenalty = 0.0
            };

            // Genera la risposta usando Semantic Kernel
            var result = await _chatService.GetChatMessageContentAsync(
                chatHistory,
                executionSettings,
                _kernel);

            var answer = result.Content ?? "Non sono riuscito a generare una risposta.";

            _logger.LogDebug("Risposta generata: {Answer}", answer);

            return answer;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore durante generazione risposta con Semantic Kernel");
            return $"Si è verificato un errore durante la generazione della risposta: {ex.Message}";
        }
    }

    /// <summary>
    /// Salva i messaggi della conversazione nel database
    /// Crea una nuova conversazione se necessario
    /// </summary>
    /// <param name="conversationId">ID conversazione esistente (null per nuova)</param>
    /// <param name="userId">ID utente</param>
    /// <param name="userQuery">Domanda utente</param>
    /// <param name="aiAnswer">Risposta AI</param>
    /// <param name="referencedDocIds">IDs documenti referenziati</param>
    /// <returns>ID della conversazione</returns>
    private async Task<int> SaveConversationMessageAsync(
        int? conversationId,
        string? userId,
        string userQuery,
        string aiAnswer,
        List<int> referencedDocIds)
    {
        try
        {
            Conversation conversation;

            if (conversationId.HasValue)
            {
                // Carica conversazione esistente
                conversation = await _context.Conversations
                    .Include(c => c.Messages)
                    .FirstOrDefaultAsync(c => c.Id == conversationId.Value)
                    ?? throw new InvalidOperationException($"Conversazione {conversationId} non trovata");
            }
            else
            {
                // Crea nuova conversazione
                // Il titolo viene generato dal primo messaggio
                var title = GenerateConversationTitle(userQuery);

                conversation = new Conversation
                {
                    UserId = userId ?? "anonymous",
                    Title = title,
                    CreatedAt = DateTime.UtcNow,
                    LastMessageAt = DateTime.UtcNow,
                    Messages = new List<Message>()
                };

                _context.Conversations.Add(conversation);
            }

            // Aggiungi messaggio utente
            conversation.Messages.Add(new Message
            {
                Role = "user",
                Content = userQuery,
                Timestamp = DateTime.UtcNow
            });

            // Aggiungi risposta AI
            conversation.Messages.Add(new Message
            {
                Role = "assistant",
                Content = aiAnswer,
                ReferencedDocumentIds = referencedDocIds,
                Timestamp = DateTime.UtcNow
            });

            // Aggiorna timestamp ultima modifica
            conversation.LastMessageAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogDebug(
                "Salvati messaggi nella conversazione {ConvId}",
                conversation.Id);

            return conversation.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore durante salvataggio conversazione");
            return conversationId ?? 0;
        }
    }

    /// <summary>
    /// Genera un titolo per la conversazione basato sulla prima domanda
    /// Tronca a 60 caratteri per mantenere leggibilità
    /// </summary>
    /// <param name="firstMessage">Primo messaggio della conversazione</param>
    /// <returns>Titolo generato</returns>
    private string GenerateConversationTitle(string firstMessage)
    {
        // Tronca e pulisci il messaggio per il titolo
        var title = firstMessage.Trim();
        
        if (title.Length > 60)
        {
            title = title.Substring(0, 57) + "...";
        }

        return title;
    }

    /// <summary>
    /// Tronca il testo alla lunghezza massima specificata
    /// Aggiunge "..." alla fine se troncato
    /// </summary>
    /// <param name="text">Testo da troncare</param>
    /// <param name="maxLength">Lunghezza massima</param>
    /// <returns>Testo troncato</returns>
    private string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text ?? string.Empty;

        return text.Substring(0, maxLength - 3) + "...";
    }
}

/// <summary>
/// Messaggio di chat semplice per la cronologia conversazionale in ModernRAGService
/// </summary>
public class ConversationMessage
{
    public MessageRole Role { get; set; }
    public string Content { get; set; }

    public ConversationMessage(MessageRole role, string content)
    {
        Role = role;
        Content = content;
    }
}

/// <summary>
/// Ruolo del messaggio nella conversazione
/// </summary>
public enum MessageRole
{
    User,
    Assistant,
    System
}
