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
/// Servizio RAG (Retrieval-Augmented Generation) modernizzato con Microsoft Semantic Kernel per Q&A intelligente su documenti
/// </summary>
/// <remarks>
/// <para><strong>Scopo:</strong> Implementare un sistema RAG completo usando Semantic Kernel per orchestrazione AI e gestione memoria semantica</para>
/// 
/// <para><strong>Architettura RAG con Semantic Kernel:</strong></para>
/// <list type="number">
/// <item><description><strong>Retrieval:</strong> ISemanticTextMemory per ricerca vettoriale su documenti indicizzati</description></item>
/// <item><description><strong>Augmentation:</strong> Costruzione contesto con documenti rilevanti e cronologia conversazione</description></item>
/// <item><description><strong>Generation:</strong> IChatCompletionService per generazione risposte AI contestualizzate</description></item>
/// </list>
/// 
/// <para><strong>Vantaggi Semantic Kernel:</strong></para>
/// <list type="bullet">
/// <item><description><strong>Orchestrazione automatica:</strong> Gestione chiamate AI con retry e fallback integrati</description></item>
/// <item><description><strong>Memoria conversazionale:</strong> ChatHistory per mantenere contesto multi-turn</description></item>
/// <item><description><strong>Plugin system:</strong> Estensibilità con funzioni e plugin custom</description></item>
/// <item><description><strong>Telemetry integrata:</strong> Logging e monitoring delle operazioni AI</description></item>
/// <item><description><strong>Provider agnostic:</strong> Supporto OpenAI, Azure OpenAI, Anthropic, etc.</description></item>
/// </list>
/// 
/// <para><strong>ISemanticTextMemory (Experimental):</strong></para>
/// <list type="bullet">
/// <item><description>Astrazione memoria semantica per storage e retrieval embeddings</description></item>
/// <item><description>Supporto collezioni multiple (es. "documents", "policies", "manuals")</description></item>
/// <item><description>Ricerca automatica con generazione embedding della query</description></item>
/// <item><description>Filtraggio per relevance score (minRelevanceScore)</description></item>
/// <item><description>Metadata associati per recupero informazioni aggiuntive</description></item>
/// </list>
/// 
/// <para><strong>Pattern Workflow RAG:</strong></para>
/// <list type="number">
/// <item><description>Ricerca documenti rilevanti via ISemanticTextMemory.SearchAsync()</description></item>
/// <item><description>Caricamento cronologia conversazione dal database</description></item>
/// <item><description>Costruzione contesto formattato con documenti e metadata</description></item>
/// <item><description>Creazione system prompt con istruzioni per l'AI</description></item>
/// <item><description>Generazione risposta con IChatCompletionService + ChatHistory</description></item>
/// <item><description>Salvataggio conversazione con tracking documenti referenziati</description></item>
/// </list>
/// 
/// <para><strong>Integrazione con PostgreSQL pgvector:</strong></para>
/// <list type="bullet">
/// <item><description>ISemanticTextMemory può usare PostgresMemoryStore per storage vettoriale</description></item>
/// <item><description>Embeddings salvati in pgvector per ricerca ottimizzata</description></item>
/// <item><description>Indicizzazione IVFFlat o HNSW per performance su dataset grandi</description></item>
/// </list>
/// </remarks>
public interface IModernRAGService
{
    /// <summary>
    /// Genera una risposta AI intelligente basandosi sui documenti rilevanti recuperati da memoria semantica
    /// </summary>
    /// <param name="userQuery">Domanda o richiesta dell'utente in linguaggio naturale</param>
    /// <param name="conversationId">ID conversazione esistente per continuità del dialogo (null per nuova conversazione)</param>
    /// <param name="userId">ID utente per isolamento dati e controllo accessi (null per utente anonimo)</param>
    /// <returns>Task che restituisce RAGResponse con risposta AI, documenti fonte e metadata</returns>
    /// <remarks>
    /// <para><strong>Workflow completo:</strong></para>
    /// <list type="number">
    /// <item><description>Vector search su ISemanticTextMemory per trovare top-K documenti rilevanti</description></item>
    /// <item><description>Caricamento ultimi 10 messaggi conversazione per contesto</description></item>
    /// <item><description>Costruzione prompt con system instructions + document context + history</description></item>
    /// <item><description>Chat completion con Semantic Kernel (Temperature 0.7, MaxTokens 2000)</description></item>
    /// <item><description>Salvataggio messaggi user+assistant nel database</description></item>
    /// <item><description>Tracking tempo risposta e documenti referenziati</description></item>
    /// </list>
    /// 
    /// <para><strong>Gestione errori graceful:</strong> Se nessun documento rilevante o errore AI, restituisce messaggio user-friendly</para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">Se conversationId specificato non esiste nel database</exception>
    Task<RAGResponse> GenerateResponseAsync(
        string userQuery, 
        int? conversationId = null,
        string? userId = null);

    /// <summary>
    /// Ricerca documenti rilevanti usando vector similarity search su memoria semantica di Semantic Kernel
    /// </summary>
    /// <param name="query">Testo query in linguaggio naturale da convertire in embedding</param>
    /// <param name="topK">Numero massimo documenti da restituire (default: 5, ottimale per contesto RAG)</param>
    /// <param name="minSimilarity">Soglia minima similarità coseno 0.0-1.0 (default: 0.7 = 70% rilevanza)</param>
    /// <returns>Task che restituisce lista documenti ordinati per relevance score decrescente</returns>
    /// <remarks>
    /// <para><strong>Processo ricerca vettoriale:</strong></para>
    /// <list type="number">
    /// <item><description>ISemanticTextMemory genera automaticamente embedding della query</description></item>
    /// <item><description>SearchAsync() esegue vector similarity search su collezione "documents"</description></item>
    /// <item><description>Filtra risultati con relevance score >= minSimilarity</description></item>
    /// <item><description>Recupera Document completi dal database usando metadata.Id</description></item>
    /// <item><description>Include Owner via EF Core per informazioni utente</description></item>
    /// <item><description>Restituisce top-K risultati con SimilarityScore e RelevantChunk</description></item>
    /// </list>
    /// 
    /// <para><strong>Performance:</strong> Richiesta a topK*2 risultati per buffer, poi limitazione finale a topK dopo retrieval database</para>
    /// <para><strong>Gestione errori:</strong> Restituisce lista vuota in caso di errore (graceful degradation)</para>
    /// </remarks>
    Task<List<RelevantDocument>> SearchRelevantDocumentsAsync(
        string query,
        int topK = 5,
        double minSimilarity = 0.7);
}

/// <summary>
/// Risposta completa generata dal sistema RAG con metadata e riferimenti documentali
/// </summary>
/// <remarks>
/// Contiene la risposta AI generata, i documenti fonte utilizzati, e metriche di performance.
/// Usata per restituire risultati completi e tracciabili all'applicazione client.
/// </remarks>
public class RAGResponse
{
    /// <summary>
    /// Testo della risposta generata dall'AI in linguaggio naturale
    /// </summary>
    /// <remarks>
    /// Risposta sintetizzata dall'AI basata sui documenti rilevanti e cronologia conversazione.
    /// Include citazioni ai documenti nel formato [DOCUMENTO N] se configurato nel system prompt.
    /// </remarks>
    public string Answer { get; set; } = string.Empty;

    /// <summary>
    /// Documenti fonte utilizzati per generare la risposta, ordinati per rilevanza
    /// </summary>
    /// <remarks>
    /// Lista documenti recuperati da vector search che hanno contribuito alla risposta.
    /// Ogni documento include score di similarità e chunk rilevante per trasparenza e verifica.
    /// </remarks>
    public List<RelevantDocument> SourceDocuments { get; set; } = new();

    /// <summary>
    /// ID della conversazione associata (per continuità del dialogo multi-turn)
    /// </summary>
    /// <remarks>
    /// Viene creato automaticamente se null in input, altrimenti usa conversazione esistente.
    /// Utilizzare questo ID nelle richieste successive per mantenere contesto conversazionale.
    /// </remarks>
    public int ConversationId { get; set; }

    /// <summary>
    /// Tempo totale impiegato per generare la risposta in millisecondi
    /// </summary>
    /// <remarks>
    /// Include: vector search, database queries, AI generation, e salvataggio conversazione.
    /// Utile per monitoring performance e identificazione bottleneck.
    /// </remarks>
    public long ResponseTimeMs { get; set; }

    /// <summary>
    /// Indica se la risposta è stata recuperata da cache invece che generata
    /// </summary>
    /// <remarks>
    /// Attualmente sempre false. Campo riservato per implementazione futura di caching risposte RAG.
    /// Il caching può migliorare performance per query ripetute identiche.
    /// </remarks>
    public bool FromCache { get; set; }
}

/// <summary>
/// Documento rilevante recuperato da vector similarity search con metadata di rilevanza
/// </summary>
/// <remarks>
/// Rappresenta un documento identificato come rilevante per la query utente, con score di similarità
/// e riferimento al chunk specifico più pertinente. Usato per costruire contesto RAG e citazioni.
/// </remarks>
public class RelevantDocument
{
    /// <summary>
    /// Documento originale completo dal database con metadata e relazioni
    /// </summary>
    /// <remarks>
    /// Include tutte le proprietà del documento (FileName, ActualCategory, UploadedAt, Owner, etc.).
    /// Recuperato via EF Core Join dopo vector search su memoria semantica.
    /// </remarks>
    public Document Document { get; set; } = null!;

    /// <summary>
    /// Score di similarità coseno tra embedding query e embedding documento (range 0.0-1.0)
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><strong>1.0:</strong> Identico semanticamente (rarissimo)</description></item>
    /// <item><description><strong>0.8-0.9:</strong> Molto rilevante</description></item>
    /// <item><description><strong>0.7-0.8:</strong> Rilevante (soglia default minSimilarity)</description></item>
    /// <item><description><strong>&lt;0.7:</strong> Bassa rilevanza, solitamente escluso</description></item>
    /// </list>
    /// Calcolato automaticamente da ISemanticTextMemory (Semantic Kernel).
    /// </remarks>
    public double SimilarityScore { get; set; }

    /// <summary>
    /// Chunk specifico del documento identificato come più rilevante per la query
    /// </summary>
    /// <remarks>
    /// Contiene il testo del chunk che ha ottenuto il punteggio di similarità più alto.
    /// Usato per costruire contesto RAG focalizzato, evitando di passare interi documenti lunghi all'AI.
    /// Può essere null se ISemanticTextMemory non restituisce metadata text dettagliati.
    /// </remarks>
    public string? RelevantChunk { get; set; }

    /// <summary>
    /// Indice posizionale del chunk all'interno del documento (0-based)
    /// </summary>
    /// <remarks>
    /// Permette di identificare la posizione esatta del chunk rilevante nel documento originale.
    /// Utile per navigazione e visualizzazione evidenziata nel client UI.
    /// Può essere null se informazione chunk index non disponibile da memoria semantica.
    /// </remarks>
    public int? ChunkIndex { get; set; }
}

/// <summary>
/// Implementazione moderna del servizio RAG usando Microsoft Semantic Kernel per orchestrazione AI e gestione memoria semantica
/// </summary>
/// <remarks>
/// <para><strong>Scopo:</strong> Fornire un servizio RAG production-ready con Semantic Kernel invece di implementazione custom</para>
/// 
/// <para><strong>Componenti Semantic Kernel utilizzati:</strong></para>
/// <list type="bullet">
/// <item><description><strong>Kernel:</strong> Orchestratore principale per AI operations e dependency injection</description></item>
/// <item><description><strong>ISemanticTextMemory:</strong> Astrazione memoria vettoriale per storage e retrieval embeddings</description></item>
/// <item><description><strong>IChatCompletionService:</strong> Servizio chat AI (OpenAI, Azure OpenAI, etc.) per generazione risposte</description></item>
/// <item><description><strong>ChatHistory:</strong> Gestione cronologia conversazionale multi-turn con ruoli (system, user, assistant)</description></item>
/// </list>
/// 
/// <para><strong>Differenze con SemanticRAGService:</strong></para>
/// <list type="bullet">
/// <item><description><strong>Vector search:</strong> ISemanticTextMemory gestisce automaticamente embeddings invece di query SQL manuali</description></item>
/// <item><description><strong>Orchestrazione:</strong> Semantic Kernel coordina chiamate AI con retry e fallback integrati</description></item>
/// <item><description><strong>Estensibilità:</strong> Supporto plugin e functions per arricchimento funzionalità</description></item>
/// <item><description><strong>Provider agnostic:</strong> Facile switch tra provider AI senza refactoring codice</description></item>
/// </list>
/// 
/// <para><strong>Lazy Initialization Pattern:</strong></para>
/// <list type="bullet">
/// <item><description>EnsureInitializedAsync() previene errori se database non esiste ancora</description></item>
/// <item><description>Chiamato automaticamente prima di ogni operazione</description></item>
/// <item><description>Graceful degradation: servizio funziona comunque anche se init fallisce</description></item>
/// </list>
/// 
/// <para><strong>Workflow RAG step-by-step:</strong></para>
/// <list type="number">
/// <item><description>SearchRelevantDocumentsAsync() - Vector search con ISemanticTextMemory (top-5, minSimilarity 0.7)</description></item>
/// <item><description>LoadConversationHistoryAsync() - Recupero ultimi 10 messaggi conversazione</description></item>
/// <item><description>BuildDocumentContext() - Formattazione documenti con metadata per AI</description></item>
/// <item><description>CreateSystemPrompt() - Istruzioni AI per generazione risposte RAG</description></item>
/// <item><description>GenerateAnswerWithKernelAsync() - Chat completion con Semantic Kernel</description></item>
/// <item><description>SaveConversationMessageAsync() - Persistenza messaggi con tracking documenti</description></item>
/// </list>
/// 
/// <para><strong>Ottimizzazioni implementate:</strong></para>
/// <list type="bullet">
/// <item><description><strong>Async/await:</strong> Pattern asincrono completo per scalabilità</description></item>
/// <item><description><strong>AsNoTracking():</strong> EF Core read-only queries per performance (dove applicato)</description></item>
/// <item><description><strong>Connection pooling:</strong> Riuso connessioni database via EF Core</description></item>
/// <item><description><strong>Graceful degradation:</strong> Gestione errori con messaggi user-friendly</description></item>
/// <item><description><strong>Logging strutturato:</strong> Diagnostica dettagliata con correlazione query-risposta</description></item>
/// </list>
/// 
/// <para><strong>Chat Completion Settings (OpenAI):</strong></para>
/// <list type="bullet">
/// <item><description><strong>MaxTokens:</strong> 2000 - Limite lunghezza risposta (bilanciamento completezza/costi)</description></item>
/// <item><description><strong>Temperature:</strong> 0.7 - Creatività moderata (0=deterministico, 1=creativo, 0.7=equilibrato)</description></item>
/// <item><description><strong>TopP:</strong> 0.9 - Nucleus sampling per diversità lessicale</description></item>
/// <item><description><strong>FrequencyPenalty:</strong> 0.0 - Nessuna penalizzazione ripetizioni (neutralità)</description></item>
/// <item><description><strong>PresencePenalty:</strong> 0.0 - Nessuna penalizzazione topic coverage</description></item>
/// </list>
/// 
/// <para><strong>Integrazioni:</strong></para>
/// <list type="bullet">
/// <item><description><strong>PostgreSQL/pgvector:</strong> Backend vettoriale per ISemanticTextMemory (via MemoryBuilder)</description></item>
/// <item><description><strong>ApplicationDbContext:</strong> EF Core per persistenza conversazioni e recupero documenti</description></item>
/// <item><description><strong>ILogger:</strong> Logging strutturato per monitoring e troubleshooting</description></item>
/// </list>
/// </remarks>
public class ModernRAGService : IModernRAGService
{
    private readonly Kernel _kernel;
    private readonly ISemanticTextMemory _memory;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ModernRAGService> _logger;
    private readonly IChatCompletionService _chatService;
    private bool _initialized = false;

    /// <summary>
    /// Costruttore del servizio RAG moderno con dependency injection di componenti Semantic Kernel
    /// </summary>
    /// <param name="kernel">Istanza Kernel configurata con AI services (chat, embeddings) e plugin</param>
    /// <param name="memory">Memoria semantica vettoriale per storage e retrieval embeddings documenti</param>
    /// <param name="context">Contesto database EF Core per accesso documenti, conversazioni e messaggi</param>
    /// <param name="logger">Logger per diagnostica, monitoring e troubleshooting operazioni RAG</param>
    /// <remarks>
    /// <para><strong>Semantic Kernel Kernel:</strong></para>
    /// <list type="bullet">
    /// <item><description>Configurato in KernelProvider con AI services (OpenAI/Azure OpenAI)</description></item>
    /// <item><description>Include IChatCompletionService per generazione risposte</description></item>
    /// <item><description>Include ITextEmbeddingGenerationService per embeddings query</description></item>
    /// <item><description>Può includere plugin e functions custom per estensioni</description></item>
    /// </list>
    /// 
    /// <para><strong>ISemanticTextMemory (SKEXP0001 - Experimental):</strong></para>
    /// <list type="bullet">
    /// <item><description>Costruito in KernelProvider con MemoryBuilder</description></item>
    /// <item><description>Backend storage: VolatileMemoryStore (in-memory) o PostgresMemoryStore (produzione)</description></item>
    /// <item><description>Gestisce automaticamente collezioni documenti ("documents", "chunks", etc.)</description></item>
    /// <item><description>Fornisce SearchAsync() per vector similarity search trasparente</description></item>
    /// </list>
    /// 
    /// <para><strong>IChatCompletionService:</strong> Estratto dal Kernel via GetRequiredService() per accesso diretto chat API</para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">Se Kernel non ha IChatCompletionService configurato</exception>
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
    /// Assicura che il servizio sia inizializzato con lazy initialization pattern
    /// </summary>
    /// <remarks>
    /// <para><strong>Scopo:</strong> Prevenire errori se database non esiste ancora (es. prima migrazione)</para>
    /// 
    /// <para><strong>Lazy initialization pattern:</strong></para>
    /// <list type="bullet">
    /// <item><description>Chiamato automaticamente all'inizio di ogni operazione pubblica</description></item>
    /// <item><description>Eseguito una sola volta (_initialized flag)</description></item>
    /// <item><description>Verifiche future saltano il processo (performance)</description></item>
    /// </list>
    /// 
    /// <para><strong>Graceful degradation:</strong></para>
    /// <list type="bullet">
    /// <item><description>Se init fallisce (es. database offline), servizio funziona comunque</description></item>
    /// <item><description>Errori loggati ma non propagati (resilienza)</description></item>
    /// <item><description>Flag _initialized impostato anche su errore per evitare retry continui</description></item>
    /// </list>
    /// 
    /// <para><strong>TODO futuro:</strong> Caricare embeddings esistenti da database in memoria per performance</para>
    /// </remarks>
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
    /// <remarks>
    /// <para><strong>Workflow completo RAG (6 step):</strong></para>
    /// <list type="number">
    /// <item><description><strong>Vector search:</strong> SearchRelevantDocumentsAsync() recupera top-5 documenti con similarità >= 0.7</description></item>
    /// <item><description><strong>Conversation history:</strong> LoadConversationHistoryAsync() carica ultimi 10 messaggi per contesto</description></item>
    /// <item><description><strong>Context building:</strong> BuildDocumentContext() formatta documenti con metadata leggibili per AI</description></item>
    /// <item><description><strong>System prompt:</strong> CreateSystemPrompt() crea istruzioni comportamentali per AI</description></item>
    /// <item><description><strong>AI generation:</strong> GenerateAnswerWithKernelAsync() usa Semantic Kernel chat completion</description></item>
    /// <item><description><strong>Persistence:</strong> SaveConversationMessageAsync() salva user query + AI response con tracking documenti</description></item>
    /// </list>
    /// 
    /// <para><strong>Gestione caso nessun documento rilevante:</strong></para>
    /// <list type="bullet">
    /// <item><description>Se SearchRelevantDocumentsAsync() restituisce lista vuota, ritorna messaggio cortese</description></item>
    /// <item><description>Evita chiamate inutili all'AI per risparmiare token e costi</description></item>
    /// <item><description>Logging warning per monitoring qualità embedding/dataset</description></item>
    /// </list>
    /// 
    /// <para><strong>Performance tracking:</strong></para>
    /// <list type="bullet">
    /// <item><description>Stopwatch misura tempo end-to-end (retrieval + generation + persistence)</description></item>
    /// <item><description>ResponseTimeMs incluso in RAGResponse per SLA monitoring</description></item>
    /// <item><description>Logging ElapsedMilliseconds per identificazione bottleneck</description></item>
    /// </list>
    /// 
    /// <para><strong>Error handling:</strong></para>
    /// <list type="bullet">
    /// <item><description>Try-catch generale cattura errori AI, database o rete</description></item>
    /// <item><description>Restituisce RAGResponse con messaggio errore user-friendly invece di exception</description></item>
    /// <item><description>Logging errore dettagliato per troubleshooting (stack trace + query)</description></item>
    /// </list>
    /// </remarks>
    /// <exception cref="InvalidOperationException">Propagato da SaveConversationMessageAsync se conversationId non esiste</exception>
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
    /// <remarks>
    /// <para><strong>Processo vector similarity search con ISemanticTextMemory:</strong></para>
    /// <list type="number">
    /// <item><description><strong>Query embedding:</strong> ISemanticTextMemory genera automaticamente embedding della query usando ITextEmbeddingGenerationService</description></item>
    /// <item><description><strong>SearchAsync():</strong> Ricerca vettoriale su collezione "documents" con similarità coseno</description></item>
    /// <item><description><strong>Filtering:</strong> Filtra risultati con relevance >= minRelevanceScore (0.7 default)</description></item>
    /// <item><description><strong>Database join:</strong> Recupera Document completi dal database usando metadata.Id</description></item>
    /// <item><description><strong>Include Owner:</strong> Carica relazione User via EF Core per informazioni autore</description></item>
    /// <item><description><strong>Limiting:</strong> Stoppa a topK risultati per evitare overhead</description></item>
    /// </list>
    /// 
    /// <para><strong>Strategia over-fetching:</strong></para>
    /// <list type="bullet">
    /// <item><description>Richiede topK*2 risultati da ISemanticTextMemory (buffer)</description></item>
    /// <item><description>Compensa eventuali documenti non trovati in database (IDs obsoleti)</description></item>
    /// <item><description>Limita finale a topK dopo retrieval database per rispettare contratto API</description></item>
    /// </list>
    /// 
    /// <para><strong>Metadata ISemanticTextMemory:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Id:</strong> Document.Id come stringa per join database</description></item>
    /// <item><description><strong>Text:</strong> Chunk testuale più rilevante (RelevantChunk)</description></item>
    /// <item><description><strong>Relevance:</strong> Score similarità coseno normalizzato 0.0-1.0</description></item>
    /// </list>
    /// 
    /// <para><strong>AsNoTracking() optimization:</strong></para>
    /// <para>CRITICA: Aggiungere .AsNoTracking() alla query FirstOrDefaultAsync per performance! Query read-only non necessita change tracking.</para>
    /// 
    /// <para><strong>Gestione errori graceful:</strong></para>
    /// <list type="bullet">
    /// <item><description>Cattura exception da ISemanticTextMemory (es. embedding API offline)</description></item>
    /// <item><description>Restituisce lista vuota invece di propagare errore</description></item>
    /// <item><description>Logging dettagliato per troubleshooting (query + stack trace)</description></item>
    /// </list>
    /// </remarks>
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
                        .AsNoTracking() // OPTIMIZATION: Read-only query, no change tracking needed
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
    /// Carica la cronologia della conversazione dal database per contesto multi-turn
    /// </summary>
    /// <param name="conversationId">ID conversazione esistente (null se nuova conversazione)</param>
    /// <returns>Task che restituisce lista messaggi ordinati cronologicamente (più vecchi prima)</returns>
    /// <remarks>
    /// <para><strong>Strategia caricamento:</strong></para>
    /// <list type="bullet">
    /// <item><description>Carica ultimi 10 messaggi (limite per token budget e contesto rilevante)</description></item>
    /// <item><description>OrderByDescending + Take(10) + OrderBy: Ottiene ultimi 10 in ordine cronologico</description></item>
    /// <item><description>Conversione Message -> ConversationMessage per compatibilità Semantic Kernel</description></item>
    /// </list>
    /// 
    /// <para><strong>AsNoTracking() optimization:</strong></para>
    /// <para>CRITICA: Aggiungere .AsNoTracking() per performance! Messaggi caricati solo per lettura.</para>
    /// 
    /// <para><strong>Ruoli messaggi:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>user:</strong> Messaggi utente (domande/richieste)</description></item>
    /// <item><description><strong>assistant:</strong> Risposte AI generate</description></item>
    /// <item><description>Convertiti in MessageRole enum per Semantic Kernel ChatHistory</description></item>
    /// </list>
    /// 
    /// <para><strong>Gestione errori:</strong></para>
    /// <list type="bullet">
    /// <item><description>Se conversationId null, restituisce lista vuota (nuova conversazione)</description></item>
    /// <item><description>Se caricamento fallisce, restituisce lista vuota e logga warning</description></item>
    /// <item><description>Graceful degradation: genera risposta senza contesto conversazionale</description></item>
    /// </list>
    /// </remarks>
    private async Task<List<ConversationMessage>> LoadConversationHistoryAsync(int? conversationId)
    {
        var history = new List<ConversationMessage>();

        if (!conversationId.HasValue)
            return history;

        try
        {
            // Carica gli ultimi 10 messaggi della conversazione
            var messages = await _context.Messages
                .AsNoTracking() // OPTIMIZATION: Read-only query, no change tracking needed
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
    /// Costruisce contesto formattato dai documenti rilevanti per prompt AI
    /// </summary>
    /// <param name="documents">Lista documenti rilevanti recuperati da vector search</param>
    /// <returns>Stringa formattata con metadata e contenuti documenti per AI</returns>
    /// <remarks>
    /// <para><strong>Formato contesto:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Header:</strong> "=== DOCUMENTI AZIENDALI RILEVANTI ===" per delimitare sezione</description></item>
    /// <item><description><strong>Numerazione:</strong> [DOCUMENTO 1], [DOCUMENTO 2], etc. per riferimenti citazioni</description></item>
    /// <item><description><strong>Metadata:</strong> Nome File, Categoria, Data caricamento, Rilevanza percentuale</description></item>
    /// <item><description><strong>Contenuto:</strong> RelevantChunk se disponibile, altrimenti ExtractedText troncato a 1500 caratteri</description></item>
    /// <item><description><strong>Separatore:</strong> "---" tra documenti per leggibilità AI</description></item>
    /// </list>
    /// 
    /// <para><strong>Ottimizzazione chunk:</strong></para>
    /// <list type="bullet">
    /// <item><description>Priorità a RelevantChunk (già selezionato da vector search) per precisione</description></item>
    /// <item><description>Fallback a ExtractedText troncato se chunk non disponibile</description></item>
    /// <item><description>Truncate a 1500 caratteri per rispettare token budget AI (~375 token con encoding GPT)</description></item>
    /// </list>
    /// 
    /// <para><strong>Perché formattazione strutturata:</strong></para>
    /// <list type="bullet">
    /// <item><description>AI può facilmente identificare e citare documenti con [DOCUMENTO N]</description></item>
    /// <item><description>Metadata forniscono contesto (data, categoria) per risposte più accurate</description></item>
    /// <item><description>Score rilevanza aiuta AI a pesare importanza relativa documenti</description></item>
    /// </list>
    /// </remarks>
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
    /// Crea il system prompt con istruzioni comportamentali per l'AI
    /// </summary>
    /// <returns>Stringa contenente istruzioni dettagliate per generazione risposte RAG</returns>
    /// <remarks>
    /// <para><strong>Ruolo AI definito:</strong> Assistente aziendale esperto che risponde basandosi SOLO su documenti forniti</para>
    /// 
    /// <para><strong>Regole chiave imposte:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Grounding:</strong> Usare SOLO informazioni nei documenti (no invenzioni/ipotesi)</description></item>
    /// <item><description><strong>Citazioni:</strong> Sempre citare fonte con formato [DOCUMENTO N]</description></item>
    /// <item><description><strong>Trasparenza:</strong> Ammettere se informazioni insufficienti</description></item>
    /// <item><description><strong>Sintesi multi-documento:</strong> Aggregare informazioni da più fonti quando necessario</description></item>
    /// </list>
    /// 
    /// <para><strong>Formato risposta richiesto:</strong></para>
    /// <list type="number">
    /// <item><description>Risposta diretta alla domanda (executive summary)</description></item>
    /// <item><description>Dettagli e contesto dai documenti (evidenze)</description></item>
    /// <item><description>Citazioni documenti fonte (tracciabilità)</description></item>
    /// </list>
    /// 
    /// <para><strong>Esempio risposta conforme:</strong></para>
    /// <para>"Secondo i documenti aziendali, la policy di lavoro da remoto permette fino a 3 giorni a settimana [DOCUMENTO 1]. 
    /// I dipendenti devono richiedere l'approvazione al proprio manager con 48 ore di anticipo [DOCUMENTO 2]."</para>
    /// 
    /// <para><strong>Perché system prompt dettagliato:</strong></para>
    /// <list type="bullet">
    /// <item><description>Riduce allucinazioni AI (invenzione informazioni non presenti)</description></item>
    /// <item><description>Garantisce risposte verificabili e tracciabili</description></item>
    /// <item><description>Mantiene professionalità e coerenza tono</description></item>
    /// <item><description>Ottimizza qualità risposte senza fine-tuning modello</description></item>
    /// </list>
    /// </remarks>
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
    /// Genera la risposta AI usando Semantic Kernel ChatCompletion con contesto RAG completo
    /// </summary>
    /// <param name="systemPrompt">System prompt con istruzioni comportamentali AI</param>
    /// <param name="documentContext">Contesto formattato con documenti rilevanti e metadata</param>
    /// <param name="conversationHistory">Cronologia messaggi conversazione per contesto multi-turn</param>
    /// <param name="userQuery">Domanda corrente utente</param>
    /// <returns>Task che restituisce risposta generata dall'AI come stringa</returns>
    /// <remarks>
    /// <para><strong>Costruzione ChatHistory per Semantic Kernel:</strong></para>
    /// <list type="number">
    /// <item><description><strong>System prompt:</strong> Inizializzazione con istruzioni comportamentali</description></item>
    /// <item><description><strong>Document context:</strong> Aggiunto come system message per grounding</description></item>
    /// <item><description><strong>Conversation history:</strong> Messaggi user/assistant precedenti per contesto</description></item>
    /// <item><description><strong>Current query:</strong> Domanda corrente utente come user message</description></item>
    /// </list>
    /// 
    /// <para><strong>OpenAIPromptExecutionSettings configurati:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>MaxTokens: 2000</strong> - Limite risposta (~500 parole) bilanciamento completezza/costi</description></item>
    /// <item><description><strong>Temperature: 0.7</strong> - Creatività moderata (0=deterministico, 1=molto creativo, 0.7=equilibrato professionale)</description></item>
    /// <item><description><strong>TopP: 0.9</strong> - Nucleus sampling per diversità lessicale senza randomness eccessivo</description></item>
    /// <item><description><strong>FrequencyPenalty: 0.0</strong> - Nessuna penalizzazione ripetizioni parole (neutralità)</description></item>
    /// <item><description><strong>PresencePenalty: 0.0</strong> - Nessuna penalizzazione topic già menzionati (completezza)</description></item>
    /// </list>
    /// 
    /// <para><strong>IChatCompletionService.GetChatMessageContentAsync():</strong></para>
    /// <list type="bullet">
    /// <item><description>API Semantic Kernel per chat completion (OpenAI, Azure OpenAI, etc.)</description></item>
    /// <item><description>Gestisce automaticamente retry su rate limiting (exponential backoff)</description></item>
    /// <item><description>Supporta streaming (non usato qui, risposta singola)</description></item>
    /// <item><description>Telemetry integrata per monitoring chiamate AI</description></item>
    /// </list>
    /// 
    /// <para><strong>Gestione ruoli messaggi:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>MessageRole.System:</strong> Istruzioni e contesto documenti</description></item>
    /// <item><description><strong>MessageRole.User:</strong> Domande/richieste utente</description></item>
    /// <item><description><strong>MessageRole.Assistant:</strong> Risposte AI precedenti</description></item>
    /// </list>
    /// 
    /// <para><strong>Error handling:</strong></para>
    /// <list type="bullet">
    /// <item><description>Cattura exception da AI API (rate limiting, network, quota exceeded)</description></item>
    /// <item><description>Restituisce messaggio errore user-friendly invece di crash</description></item>
    /// <item><description>Logging dettagliato per troubleshooting (stack trace + settings)</description></item>
    /// </list>
    /// 
    /// <para><strong>Fallback content:</strong> Se result.Content è null/empty, restituisce messaggio default professionale</para>
    /// </remarks>
    /// <exception cref="HttpRequestException">Errori rete/connessione con AI provider</exception>
    /// <exception cref="InvalidOperationException">Se Kernel non ha IChatCompletionService configurato</exception>
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
    /// Salva i messaggi user + assistant della conversazione nel database con tracking documenti referenziati
    /// </summary>
    /// <param name="conversationId">ID conversazione esistente (null per creare nuova conversazione)</param>
    /// <param name="userId">ID utente proprietario conversazione (default "anonymous" se null)</param>
    /// <param name="userQuery">Domanda utente da salvare come messaggio "user"</param>
    /// <param name="aiAnswer">Risposta AI da salvare come messaggio "assistant"</param>
    /// <param name="referencedDocIds">Lista IDs documenti citati nella risposta per tracking e audit</param>
    /// <returns>Task che restituisce ID conversazione (creato se nuova, esistente se aggiornata)</returns>
    /// <remarks>
    /// <para><strong>Workflow salvataggio:</strong></para>
    /// <list type="number">
    /// <item><description><strong>Carica o crea conversazione:</strong> Se conversationId non null carica esistente, altrimenti crea nuova</description></item>
    /// <item><description><strong>Genera titolo:</strong> Per nuove conversazioni, usa primi 60 caratteri query come titolo</description></item>
    /// <item><description><strong>Aggiungi messaggi:</strong> Crea Message per user query e AI answer con timestamp UTC</description></item>
    /// <item><description><strong>Tracking documenti:</strong> ReferencedDocumentIds salvato su messaggio assistant per audit</description></item>
    /// <item><description><strong>Aggiorna LastMessageAt:</strong> Timestamp ultima modifica conversazione per ordinamento UI</description></item>
    /// <item><description><strong>SaveChangesAsync:</strong> Persist transazionale nel database</description></item>
    /// </list>
    /// 
    /// <para><strong>Conversazione esistente vs nuova:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Esistente:</strong> Include(c => c.Messages) carica collezione per aggiunta messaggi</description></item>
    /// <item><description><strong>Nuova:</strong> Crea Conversation con Title auto-generato e lista Messages vuota</description></item>
    /// <item><description><strong>Exception:</strong> Se conversationId specificato ma non trovato, lancia InvalidOperationException</description></item>
    /// </list>
    /// 
    /// <para><strong>Generazione titolo conversazione:</strong></para>
    /// <list type="bullet">
    /// <item><description>Usa primi 60 caratteri query utente per preview rapida</description></item>
    /// <item><description>Aggiunge "..." se troncato per indicare continuazione</description></item>
    /// <item><description>Trim() rimuove spazi leading/trailing per pulizia</description></item>
    /// </list>
    /// 
    /// <para><strong>ReferencedDocumentIds tracking:</strong></para>
    /// <list type="bullet">
    /// <item><description>Salvato solo su messaggio assistant (risposta AI)</description></item>
    /// <item><description>Permette audit: quali documenti hanno informato ogni risposta</description></item>
    /// <item><description>Utile per compliance e verifica qualità risposte</description></item>
    /// </list>
    /// 
    /// <para><strong>Error handling:</strong></para>
    /// <list type="bullet">
    /// <item><description>Try-catch generale cattura errori database (constraint violation, connection lost)</description></item>
    /// <item><description>Restituisce conversationId esistente o 0 su errore (graceful degradation)</description></item>
    /// <item><description>Logging errore dettagliato per troubleshooting</description></item>
    /// <item><description>Non propaga exception per evitare crash - conversazione non salvata ma risposta comunque generata</description></item>
    /// </list>
    /// </remarks>
    /// <exception cref="InvalidOperationException">Se conversationId specificato non esiste nel database</exception>
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
    /// Genera un titolo descrittivo per la conversazione basandosi sulla prima domanda utente
    /// </summary>
    /// <param name="firstMessage">Primo messaggio utente della conversazione</param>
    /// <returns>Titolo troncato a 60 caratteri per leggibilità in UI</returns>
    /// <remarks>
    /// <para><strong>Strategia titolo:</strong></para>
    /// <list type="bullet">
    /// <item><description>Usa prime parole query come preview conversazione</description></item>
    /// <item><description>Limite 60 caratteri per compatibilità UI (sidebar, liste)</description></item>
    /// <item><description>Aggiunge "..." se troncato per indicare testo continua</description></item>
    /// <item><description>Trim() rimuove spazi leading/trailing per pulizia</description></item>
    /// </list>
    /// 
    /// <para><strong>Alternativa futura:</strong> Usare AI (GPT-3.5) per generare titoli più descrittivi (es. "Policy lavoro remoto" invece di "Quanti giorni posso lavorare da remoto?...")</para>
    /// </remarks>
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
    /// Tronca il testo alla lunghezza massima specificata con ellipsis
    /// </summary>
    /// <param name="text">Testo da troncare (può essere null)</param>
    /// <param name="maxLength">Lunghezza massima incluso ellipsis "..." (deve essere >= 3)</param>
    /// <returns>Testo troncato con "..." se supera maxLength, altrimenti testo originale</returns>
    /// <remarks>
    /// <para><strong>Uso nei contesti RAG:</strong></para>
    /// <list type="bullet">
    /// <item><description>Limitare ExtractedText documenti per rispettare token budget AI</description></item>
    /// <item><description>Preview chunk in UI senza sovraccaricare interfaccia</description></item>
    /// <item><description>Evitare context window overflow con documenti molto lunghi</description></item>
    /// </list>
    /// 
    /// <para><strong>Calcolo lunghezza:</strong></para>
    /// <list type="bullet">
    /// <item><description>Sottrae 3 caratteri per "..." da maxLength</description></item>
    /// <item><description>Preserva semantica: meglio troncare che corrompere encoding</description></item>
    /// <item><description>Null-safe: restituisce string.Empty se input null</description></item>
    /// </list>
    /// 
    /// <para><strong>Esempio:</strong> TruncateText("Documento molto lungo...", 20) -> "Documento molto l..."</para>
    /// </remarks>
    private string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text ?? string.Empty;

        return text.Substring(0, maxLength - 3) + "...";
    }
}

/// <summary>
/// Messaggio singolo nella cronologia conversazionale per compatibilità con Semantic Kernel ChatHistory
/// </summary>
/// <remarks>
/// <para><strong>Scopo:</strong> Adapter tra Message entity del database e MessageRole/Content richiesti da ChatHistory</para>
/// 
/// <para><strong>Differenze con Message entity:</strong></para>
/// <list type="bullet">
/// <item><description><strong>Message:</strong> Entity EF Core con FK, timestamp, ReferencedDocumentIds, ConversationId</description></item>
/// <item><description><strong>ConversationMessage:</strong> DTO lightweight per ChatHistory con solo Role + Content</description></item>
/// </list>
/// 
/// <para><strong>Conversione Message -> ConversationMessage:</strong></para>
/// <para>Vedi LoadConversationHistoryAsync() per mapping da database a questa struttura</para>
/// </remarks>
public class ConversationMessage
{
    /// <summary>
    /// Ruolo del messaggio nella conversazione (User, Assistant, System)
    /// </summary>
    /// <remarks>
    /// Usato da Semantic Kernel ChatHistory per differenziare tipi messaggio e applicare logica appropriata
    /// </remarks>
    public MessageRole Role { get; set; }
    
    /// <summary>
    /// Contenuto testuale del messaggio
    /// </summary>
    /// <remarks>
    /// Per User: domanda/richiesta originale. Per Assistant: risposta AI generata. Per System: istruzioni/contesto.
    /// </remarks>
    public string Content { get; set; }

    /// <summary>
    /// Costruttore per creare messaggio conversazionale con ruolo e contenuto
    /// </summary>
    /// <param name="role">Ruolo messaggio (User/Assistant/System)</param>
    /// <param name="content">Contenuto testuale messaggio</param>
    public ConversationMessage(MessageRole role, string content)
    {
        Role = role;
        Content = content;
    }
}

/// <summary>
/// Ruolo del messaggio nella conversazione per compatibilità con Semantic Kernel ChatHistory
/// </summary>
/// <remarks>
/// <para><strong>Ruoli standard chat AI:</strong></para>
/// <list type="bullet">
/// <item><description><strong>User:</strong> Messaggi inviati dall'utente (domande, richieste, follow-up)</description></item>
/// <item><description><strong>Assistant:</strong> Messaggi generati dall'AI (risposte, chiarimenti, suggerimenti)</description></item>
/// <item><description><strong>System:</strong> Istruzioni e contesto per AI (system prompt, document context, metadata)</description></item>
/// </list>
/// 
/// <para><strong>Mapping ChatHistory ruoli:</strong></para>
/// <list type="bullet">
/// <item><description>User -> ChatHistory.AddUserMessage()</description></item>
/// <item><description>Assistant -> ChatHistory.AddAssistantMessage()</description></item>
/// <item><description>System -> ChatHistory.AddSystemMessage()</description></item>
/// </list>
/// 
/// <para><strong>Persistenza database:</strong> Role enum convertito in stringa ("user"/"assistant"/"system") per Message.Role column</para>
/// </remarks>
public enum MessageRole
{
    /// <summary>
    /// Messaggio inviato dall'utente (domanda o richiesta)
    /// </summary>
    User,
    
    /// <summary>
    /// Messaggio generato dall'AI (risposta o chiarimento)
    /// </summary>
    Assistant,
    
    /// <summary>
    /// Messaggio di sistema con istruzioni o contesto per AI
    /// </summary>
    System
}
