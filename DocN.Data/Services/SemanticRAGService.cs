using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using DocN.Data.Models;
using DocN.Data.Utilities;
using DocN.Core.Interfaces;
using System.Text;

#pragma warning disable SKEXP0110 // Agents are experimental
#pragma warning disable SKEXP0001 // Type is for evaluation purposes only

namespace DocN.Data.Services;

/// <summary>
/// Servizio RAG (Retrieval-Augmented Generation) avanzato basato su Microsoft Semantic Kernel e Agent Framework
/// Implementa retrieval vettoriale semantico e chat AI intelligente su documenti caricati dagli utenti
/// </summary>
/// <remarks>
/// <para><strong>Scopo:</strong> Fornire un sistema RAG completo per Q&A su documenti con AI generativa</para>
/// 
/// <para><strong>Architettura RAG:</strong></para>
/// <list type="number">
/// <item><description><strong>Retrieval:</strong> Ricerca vettoriale semantica con embeddings (pgvector/SQL Server VECTOR)</description></item>
/// <item><description><strong>Augmentation:</strong> Costruzione contesto con documenti rilevanti e cronologia conversazione</description></item>
/// <item><description><strong>Generation:</strong> Generazione risposta AI usando Semantic Kernel Chat Completion</description></item>
/// </list>
/// 
/// <para><strong>Pattern Multi-Agent Semantic Kernel:</strong></para>
/// <list type="bullet">
/// <item><description><strong>RetrievalAgent:</strong> Specializzato nell'identificazione e estrazione informazioni rilevanti</description></item>
/// <item><description><strong>SynthesisAgent:</strong> Specializzato nella sintesi e generazione risposte in linguaggio naturale</description></item>
/// <item><description><strong>Orchestrazione:</strong> Coordinamento agents tramite AgentChat (implementazione futura)</description></item>
/// </list>
/// 
/// <para><strong>Ricerca Vettoriale (Vector Similarity Search):</strong></para>
/// <list type="bullet">
/// <item><description><strong>Embeddings:</strong> text-embedding-ada-002 (OpenAI) - 1536 dimensioni o modelli compatibili (768/1536)</description></item>
/// <item><description><strong>Metrica:</strong> Similarità coseno per confronto semantico tra query e documenti</description></item>
/// <item><description><strong>Strategie:</strong> SQL Server VECTOR_DISTANCE (nativo) o calcolo in-memory ottimizzato</description></item>
/// <item><description><strong>Granularità:</strong> Ricerca sia a livello documento che chunk per massima precisione</description></item>
/// <item><description><strong>Soglia:</strong> minSimilarity 0.7 default (70% similarità minima)</description></item>
/// </list>
/// 
/// <para><strong>Gestione Conversazioni:</strong></para>
/// <list type="bullet">
/// <item><description>Persistenza conversazioni nel database con cronologia messaggi</description></item>
/// <item><description>Caricamento ultimi 10 messaggi per contesto conversazionale</description></item>
/// <item><description>Tracking documenti referenziati per ogni risposta</description></item>
/// <item><description>Supporto multi-turn conversation con memoria contestuale</description></item>
/// </list>
/// 
/// <para><strong>Chat Completion con Semantic Kernel:</strong></para>
/// <list type="bullet">
/// <item><description><strong>Modello:</strong> Configurabile (GPT-4, GPT-3.5-turbo, etc.)</description></item>
/// <item><description><strong>MaxTokens:</strong> 2000 (limite lunghezza risposta)</description></item>
/// <item><description><strong>Temperature:</strong> 0.7 (equilibrio creatività/coerenza)</description></item>
/// <item><description><strong>TopP:</strong> 0.9 (nucleus sampling per diversità)</description></item>
/// <item><description><strong>Streaming:</strong> Supporto generazione incrementale per UX reattiva</description></item>
/// </list>
/// 
/// <para><strong>Ottimizzazioni Implementate:</strong></para>
/// <list type="bullet">
/// <item><description><strong>Async/await:</strong> Pattern asincrono completo per scalabilità e performance</description></item>
/// <item><description><strong>Batch processing:</strong> Limitazione candidati (10x topK) per ridurre memoria e computation</description></item>
/// <item><description><strong>Caching:</strong> Integrazione ICacheService per caching embeddings e risultati (disabilitato di default)</description></item>
/// <item><description><strong>Connection pooling:</strong> Riuso connessioni EF Core per efficienza database</description></item>
/// <item><description><strong>Candidate limiting:</strong> Filtraggio database-side prima del calcolo similarità</description></item>
/// <item><description><strong>Deduplicazione:</strong> HashSet per evitare documenti duplicati nei risultati</description></item>
/// </list>
/// 
/// <para><strong>Integrazioni:</strong></para>
/// <list type="bullet">
/// <item><description><strong>PostgreSQL pgvector:</strong> Estensione vettoriale per ricerca semantica (produzione)</description></item>
/// <item><description><strong>SQL Server VECTOR:</strong> Tipo VECTOR nativo (SQL Server 2025+) con VECTOR_DISTANCE</description></item>
/// <item><description><strong>Semantic Kernel:</strong> Framework Microsoft per orchestrazione AI e agents</description></item>
/// <item><description><strong>ICacheService:</strong> Astrazione caching per embeddings e query results</description></item>
/// <item><description><strong>IEmbeddingService:</strong> Servizio generazione embeddings vettoriali</description></item>
/// </list>
/// 
/// <para><strong>Strategia Fallback Multi-Livello:</strong></para>
/// <list type="number">
/// <item><description>Tenta VECTOR_DISTANCE nativo (SQL Server 2025+)</description></item>
/// <item><description>Fallback a calcolo database-optimized con candidate limiting</description></item>
/// <item><description>Fallback finale a calcolo in-memory completo (testing/compatibilità)</description></item>
/// </list>
/// 
/// <para><strong>Sicurezza e Validazione:</strong></para>
/// <list type="bullet">
/// <item><description>Filtro documenti per userId (isolamento tenant)</description></item>
/// <item><description>Validazione dimensioni embedding (768/1536 supportate)</description></item>
/// <item><description>Whitelist column names per prevenire SQL injection</description></item>
/// <item><description>Gestione errori con logging dettagliato e messaggi user-friendly</description></item>
/// </list>
/// </remarks>
public class SemanticRAGService : ISemanticRAGService
{
    private readonly Kernel _kernel;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SemanticRAGService> _logger;
    private readonly IEmbeddingService _embeddingService;
    private readonly IChatCompletionService? _chatService;
    private readonly ICacheService _cacheService;

    // Semantic Kernel Agents for RAG pipeline
    private ChatCompletionAgent? _retrievalAgent;
    private ChatCompletionAgent? _synthesisAgent;
    // Note: _agentChat is reserved for future multi-agent pipeline implementation

    /// <summary>Moltiplicatore per limite candidati (10x topK) per bilanciare qualità risultati e performance</summary>
    private const int CandidateLimitMultiplier = 10;
    
    /// <summary>Minimo assoluto di candidati da recuperare (100) per garantire copertura sufficiente</summary>
    private const int MinCandidateLimit = 100;
    
    /// <summary>Dimensione vettore embedding standard: 1536 (text-embedding-ada-002 OpenAI)</summary>
    private const int VectorDimension = 1536;

    /// <summary>
    /// Inizializza una nuova istanza del servizio RAG semantico
    /// </summary>
    /// <param name="kernel">Kernel Semantic Kernel configurato con AI provider</param>
    /// <param name="context">DbContext EF Core per accesso database documenti e conversazioni</param>
    /// <param name="logger">Logger per diagnostica e monitoraggio operazioni RAG</param>
    /// <param name="embeddingService">Servizio per generazione embeddings vettoriali (text-embedding-ada-002)</param>
    /// <param name="cacheService">Servizio caching per embeddings e risultati query</param>
    /// <remarks>
    /// <para><strong>Pattern di inizializzazione:</strong></para>
    /// <list type="number">
    /// <item><description>Inietta dipendenze core (Kernel, DbContext, Logger, Services)</description></item>
    /// <item><description>Tenta inizializzazione IChatCompletionService da Kernel</description></item>
    /// <item><description>Inizializza agents Semantic Kernel (RetrievalAgent, SynthesisAgent)</description></item>
    /// <item><description>Se fallisce, logga warning ma non blocca costruzione (graceful degradation)</description></item>
    /// </list>
    /// 
    /// <para><strong>Gestione errori:</strong></para>
    /// Se la configurazione AI manca o è invalida, il servizio resta costruibile ma i metodi
    /// pubblici restituiranno messaggi di errore user-friendly invece di lanciare eccezioni
    /// 
    /// <para><strong>Connection pooling:</strong></para>
    /// Il DbContext iniettato beneficia automaticamente del connection pooling EF Core configurato
    /// a livello di applicazione per performance ottimali
    /// </remarks>
    public SemanticRAGService(
        Kernel kernel,
        ApplicationDbContext context,
        ILogger<SemanticRAGService> logger,
        IEmbeddingService embeddingService,
        ICacheService cacheService)
    {
        _kernel = kernel;
        _context = context;
        _logger = logger;
        _embeddingService = embeddingService;
        _cacheService = cacheService;
        
        // Attempt to initialize chat service and agents during construction
        // If initialization fails (e.g., due to missing AI configuration),
        // the service will return appropriate error messages when called
        try
        {
            _chatService = kernel.GetRequiredService<IChatCompletionService>();
            InitializeAgents();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not initialize SemanticRAGService during construction. AI features will be unavailable.");
        }
    }

    /// <summary>
    /// Inizializza gli agenti Semantic Kernel per il workflow RAG multi-agent
    /// </summary>
    /// <remarks>
    /// <para><strong>Pattern Multi-Agent:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>RetrievalAgent:</strong> Responsabile del recupero e filtraggio informazioni rilevanti</description></item>
    /// <item><description><strong>SynthesisAgent:</strong> Responsabile della sintesi e generazione risposte naturali</description></item>
    /// </list>
    /// 
    /// <para><strong>RetrievalAgent - Compiti:</strong></para>
    /// <list type="number">
    /// <item><description>Comprensione intento domanda utente</description></item>
    /// <item><description>Identificazione concetti chiave ed entità</description></item>
    /// <item><description>Determinazione documenti più rilevanti</description></item>
    /// <item><description>Estrazione informazioni pertinenti</description></item>
    /// <item><description>Strutturazione dati per SynthesisAgent</description></item>
    /// </list>
    /// 
    /// <para><strong>SynthesisAgent - Compiti:</strong></para>
    /// <list type="number">
    /// <item><description>Analisi informazioni da RetrievalAgent</description></item>
    /// <item><description>Generazione risposte in linguaggio naturale</description></item>
    /// <item><description>Citazione fonti con formato [Documento N] (nome_file.pdf)</description></item>
    /// <item><description>Mantenimento contesto conversazionale</description></item>
    /// <item><description>Sintesi concisa ma completa</description></item>
    /// </list>
    /// 
    /// <para><strong>Lingua:</strong></para>
    /// Entrambi gli agenti sono istruiti esplicitamente a comunicare in italiano
    /// per coerenza con l'applicazione e user experience
    /// 
    /// <para><strong>Implementazione futura:</strong></para>
    /// Gli agenti sono attualmente definiti ma l'orchestrazione tramite AgentChat
    /// è riservata per sviluppi futuri. Al momento si usa chat completion diretto
    /// 
    /// <para><strong>Gestione errori:</strong></para>
    /// Errori durante inizializzazione vengono loggati ma non bloccano il servizio.
    /// Il sistema continua a funzionare con modalità fallback
    /// </remarks>
    private void InitializeAgents()
    {
        try
        {
            // Create Retrieval Agent - responsible for finding relevant documents
            _retrievalAgent = new ChatCompletionAgent
            {
                Name = "RetrievalAgent",
                Instructions = @"Sei un agente specializzato nel recupero informazioni. Il tuo ruolo è:
1. Comprendere l'intento della domanda dell'utente
2. Identificare concetti chiave ed entità
3. Determinare quali documenti sono più rilevanti
4. Estrarre le informazioni più pertinenti dai documenti
5. Fornire informazioni strutturate all'agente di sintesi

Sii sempre preciso e concentrati sulla rilevanza.
IMPORTANTE: Comunica sempre in italiano.",
                Kernel = _kernel
            };

            // Create Synthesis Agent - responsible for generating natural language answers
            _synthesisAgent = new ChatCompletionAgent
            {
                Name = "SynthesisAgent",
                Instructions = @"Sei un agente esperto di sintesi. Il tuo ruolo è:
1. Analizzare le informazioni fornite dall'agente di recupero
2. Generare risposte chiare, accurate e in linguaggio naturale
3. Citare le fonti in modo appropriato usando i riferimenti ai documenti
4. Mantenere il contesto e la coerenza della conversazione
5. Essere conciso ma completo

Cita sempre le tue fonti usando il formato [Documento N] in cui N è il numero del documento, e indica il nome del file tra parentesi (nome_file.pdf).
Alla fine della risposta, elenca tutti i documenti consultati.
IMPORTANTE: Rispondi sempre in italiano.",
                Kernel = _kernel
            };

            _logger.LogInformation("Semantic Kernel agents initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing Semantic Kernel agents");
        }
    }

    /// <summary>
    /// Genera una risposta RAG (Retrieval-Augmented Generation) per una query utente
    /// </summary>
    /// <param name="query">Domanda dell'utente in linguaggio naturale</param>
    /// <param name="userId">ID dell'utente che effettua la query (per controllo accesso)</param>
    /// <param name="conversationId">ID conversazione per mantenere il contesto (opzionale)</param>
    /// <param name="specificDocumentIds">Lista di ID documenti specifici su cui cercare (opzionale)</param>
    /// <param name="topK">Numero massimo di documenti rilevanti da recuperare (default: 5)</param>
    /// <returns>SemanticRAGResponse contenente la risposta generata, documenti fonte e metadati</returns>
    /// <remarks>
    /// Scopo: Implementa il flusso completo RAG end-to-end combinando retrieval vettoriale e generazione AI
    /// 
    /// Processo in 5 step:
    /// 1. Ricerca documenti rilevanti tramite vector similarity search
    /// 2. Caricamento cronologia conversazione (se presente)
    /// 3. Costruzione contesto da documenti recuperati
    /// 4. Generazione risposta con Semantic Kernel (orchestrazione AI)
    /// 5. Salvataggio conversazione nel database
    /// 
    /// Output atteso:
    /// - Risposta testuale in linguaggio naturale
    /// - Lista documenti fonte con score di rilevanza
    /// - ID conversazione per follow-up
    /// - Tempo di elaborazione in millisecondi
    /// - Metadati (numero documenti, score massimo, presenza cronologia)
    /// </remarks>
    public async Task<SemanticRAGResponse> GenerateResponseAsync(
        string query,
        string userId,
        int? conversationId = null,
        List<int>? specificDocumentIds = null,
        int topK = 5)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // Ensure services are initialized
            if (_chatService == null)
            {
                _logger.LogError("SemanticRAGService is not properly initialized. Chat completion service is not available.");
                return new SemanticRAGResponse
                {
                    Answer = "AI services are not properly configured. Please check the configuration and try again.",
                    SourceDocuments = new List<RelevantDocumentResult>(),
                    ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                    Metadata = new Dictionary<string, object> { { "error", "Service not initialized" } }
                };
            }

            _logger.LogInformation(
                "Generating RAG response for query: {Query}, User: {UserId}, Conversation: {ConvId}",
                query, userId, conversationId);

            // Check cache first (disabled for now - use specific embedding cache if needed)
            // var cacheKey = $"rag:{userId}:{query}:{string.Join(",", specificDocumentIds ?? new List<int>())}";
            // Can add generic caching later if needed

            // Step 1: Vector-based document retrieval
            var relevantDocs = await SearchDocumentsAsync(query, userId, topK, 0.7);

            if (!relevantDocs.Any())
            {
                _logger.LogWarning("No relevant documents found for query: {Query}. Returning message about internal documents only.", query);
                return new SemanticRAGResponse
                {
                    Answer = @"❌ Non ho trovato documenti rilevanti nel sistema RAG interno per rispondere alla tua domanda.

Questo sistema di chat AI funziona PRINCIPALMENTE sui documenti interni che hai caricato nel sistema. 

📄 Per ottenere risposte accurate:
1. Assicurati di aver caricato i documenti necessari nella sezione 'Upload'
2. Verifica che i documenti siano stati elaborati correttamente (con embedding generati)
3. Riprova la tua domanda una volta caricati i documenti pertinenti

💡 Suggerimento: Puoi verificare i tuoi documenti caricati nella sezione 'Documents' del sistema.

Il sistema non fornisce risposte basate su conoscenze generali, ma solo su informazioni contenute nei documenti che hai caricato.",
                    SourceDocuments = new List<RelevantDocumentResult>(),
                    ResponseTimeMs = stopwatch.ElapsedMilliseconds
                };
            }

            // Step 2: Load conversation history
            var conversationHistory = await LoadConversationHistoryAsync(conversationId);

            // Step 3: Build context from relevant documents
            var documentContext = BuildDocumentContext(relevantDocs);

            // Step 4: Generate response using Semantic Kernel
            var answer = await GenerateAnswerWithSemanticKernelAsync(
                query,
                documentContext,
                conversationHistory);

            // Step 5: Save conversation
            var savedConversationId = await SaveConversationAsync(
                conversationId,
                userId,
                query,
                answer,
                relevantDocs.Select(d => d.DocumentId).ToList());

            stopwatch.Stop();

            var response = new SemanticRAGResponse
            {
                Answer = answer,
                SourceDocuments = relevantDocs,
                ConversationId = savedConversationId,
                ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                FromCache = false,
                Metadata = new Dictionary<string, object>
                {
                    ["documentsRetrieved"] = relevantDocs.Count,
                    ["topSimilarityScore"] = relevantDocs.FirstOrDefault()?.SimilarityScore ?? 0,
                    ["hasConversationHistory"] = conversationHistory.Any()
                }
            };

            // Cache disabled for now - can be added with generic caching service later
            // await _cacheService.SetAsync(cacheKey, response, TimeSpan.FromMinutes(5));

            _logger.LogInformation(
                "RAG response generated in {ElapsedMs}ms with {DocCount} documents",
                stopwatch.ElapsedMilliseconds, relevantDocs.Count);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating RAG response for query: {Query}", query);
            return new SemanticRAGResponse
            {
                Answer = $"An error occurred while processing your request: {ex.Message}",
                ResponseTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    /// <summary>
    /// Genera una risposta RAG in modalità streaming (chunk-by-chunk) per feedback real-time
    /// </summary>
    /// <param name="query">Domanda dell'utente in linguaggio naturale</param>
    /// <param name="userId">ID dell'utente che effettua la query</param>
    /// <param name="conversationId">ID conversazione per contesto (opzionale)</param>
    /// <param name="specificDocumentIds">Lista ID documenti su cui cercare (opzionale)</param>
    /// <returns>AsyncEnumerable di stringhe (chunk di risposta) che vengono generate progressivamente</returns>
    /// <remarks>
    /// Scopo: Fornire esperienza utente reattiva mostrando la risposta mentre viene generata
    /// 
    /// Vantaggi dello streaming:
    /// - Feedback immediato all'utente (percezione di velocità)
    /// - Possibilità di interrompere generazione se non necessaria
    /// - Riduce attesa percepita per risposte lunghe
    /// 
    /// Output atteso:
    /// - Stream di chunk testuali che formano la risposta completa
    /// - Ogni chunk viene emesso appena disponibile dall'AI
    /// - Conversazione salvata automaticamente al termine
    /// </remarks>
    public async IAsyncEnumerable<string> GenerateStreamingResponseAsync(
        string query,
        string userId,
        int? conversationId = null,
        List<int>? specificDocumentIds = null)
    {
        // Ensure services are initialized
        if (_chatService == null)
        {
            _logger.LogError("SemanticRAGService is not properly initialized. Chat completion service is not available.");
            yield return "AI services are not properly configured. Please check the configuration and try again.";
            yield break;
        }

        _logger.LogInformation("Generating streaming RAG response for query: {Query}", query);

        // Step 1: Retrieve relevant documents
        var relevantDocs = await SearchDocumentsAsync(query, userId, 5, 0.7);

        if (!relevantDocs.Any())
        {
            yield return "I couldn't find any relevant documents to answer your question.";
            yield break;
        }

        // Step 2: Build context
        var documentContext = BuildDocumentContext(relevantDocs);
        var conversationHistory = await LoadConversationHistoryAsync(conversationId);

        // Step 3: Create chat history
        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(CreateSystemPrompt());
        chatHistory.AddSystemMessage($"DOCUMENT CONTEXT:\n{documentContext}");

        foreach (var msg in conversationHistory)
        {
            if (msg.Role == "user")
                chatHistory.AddUserMessage(msg.Content);
            else if (msg.Role == "assistant")
                chatHistory.AddAssistantMessage(msg.Content);
        }

        chatHistory.AddUserMessage(query);

        // Step 4: Stream the response
        var settings = new OpenAIPromptExecutionSettings
        {
            MaxTokens = 2000,
            Temperature = 0.7,
            TopP = 0.9
        };

        var fullAnswer = new StringBuilder();

        await foreach (var chunk in _chatService.GetStreamingChatMessageContentsAsync(
            chatHistory, settings, _kernel))
        {
            if (!string.IsNullOrEmpty(chunk.Content))
            {
                fullAnswer.Append(chunk.Content);
                yield return chunk.Content;
            }
        }

        // Save conversation after streaming completes
        await SaveConversationAsync(
            conversationId,
            userId,
            query,
            fullAnswer.ToString(),
            relevantDocs.Select(d => d.DocumentId).ToList());
    }

    /// <summary>
    /// Cerca documenti rilevanti per una query utilizzando ricerca vettoriale semantica
    /// </summary>
    /// <param name="query">Query di ricerca in linguaggio naturale</param>
    /// <param name="userId">ID utente per filtraggio documenti accessibili</param>
    /// <param name="topK">Numero massimo di risultati da restituire (default: 10)</param>
    /// <param name="minSimilarity">Soglia minima di similarità coseno 0-1 (default: 0.7)</param>
    /// <returns>Lista di RelevantDocumentResult ordinati per score di rilevanza decrescente</returns>
    /// <remarks>
    /// Scopo: Implementare ricerca semantica basata su embeddings vettoriali
    /// 
    /// Processo:
    /// 1. Genera embedding vettoriale della query
    /// 2. Calcola similarità coseno tra query e documenti/chunk
    /// 3. Filtra risultati sotto soglia minSimilarity
    /// 4. Ordina per score decrescente e limita a topK
    /// 
    /// Output atteso:
    /// - Lista documenti con score >= minSimilarity
    /// - Ordinamento per rilevanza (score più alto = più rilevante)
    /// - Include metadati: titolo, categoria, estratto di testo
    /// - Lista vuota se nessun documento supera la soglia
    /// </remarks>
    public async Task<List<RelevantDocumentResult>> SearchDocumentsAsync(
        string query,
        string userId,
        int topK = 10,
        double minSimilarity = 0.7)
    {
        try
        {
            _logger.LogDebug("Searching documents with vector embeddings for: {Query}", query);

            // Generate query embedding
            var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query);
            if (queryEmbedding == null)
            {
                _logger.LogWarning("Failed to generate query embedding");
                return new List<RelevantDocumentResult>();
            }

            // Use database-level vector search instead of in-memory calculation
            return await SearchDocumentsWithEmbeddingDatabaseAsync(queryEmbedding, userId, topK, minSimilarity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching documents for query: {Query}", query);
            return new List<RelevantDocumentResult>();
        }
    }

    /// <summary>
    /// Esegue ricerca documenti utilizzando calcolo di similarità vettoriale ottimizzato a livello database
    /// Implementa strategia multi-fallback: VECTOR_DISTANCE nativo → calcolo database-optimized → in-memory completo
    /// </summary>
    /// <param name="queryEmbedding">Vettore embedding della query (qualsiasi dimensione supportata)</param>
    /// <param name="userId">ID utente per controllo accesso e isolamento tenant</param>
    /// <param name="topK">Numero massimo di risultati da restituire</param>
    /// <param name="minSimilarity">Soglia minima di similarità coseno 0-1 (default: 0.7 = 70%)</param>
    /// <returns>Lista di documenti rilevanti ordinati per similarità decrescente</returns>
    /// <remarks>
    /// <para><strong>Strategia di ottimizzazione multi-livello:</strong></para>
    /// <list type="number">
    /// <item><description><strong>Livello 1:</strong> Tenta VECTOR_DISTANCE nativo SQL Server 2025+ (performance ottimali)</description></item>
    /// <item><description><strong>Livello 2:</strong> Se fallisce, usa approccio database-optimized con candidate limiting</description></item>
    /// <item><description><strong>Livello 3:</strong> Se anche quello fallisce, fallback completo in-memory</description></item>
    /// </list>
    /// 
    /// <para><strong>Database-optimized approach (Livello 2):</strong></para>
    /// <list type="bullet">
    /// <item><description>Limita candidati a livello database: (topK × 10) o min 100 documenti/chunk</description></item>
    /// <item><description>Recupera solo documenti più recenti (UploadedAt DESC) → spesso più rilevanti</description></item>
    /// <item><description>Usa Select() projection per caricare solo campi necessari (no tracking overhead)</description></item>
    /// <item><description>Calcola similarità in-memory solo sui candidati limitati</description></item>
    /// <item><description>Drastica riduzione memoria e CPU rispetto a full in-memory scan</description></item>
    /// </list>
    /// 
    /// <para><strong>Candidate limit formula:</strong></para>
    /// <code>
    /// candidateLimit = Math.Max(topK * CandidateLimitMultiplier, MinCandidateLimit)
    /// candidateLimit = Math.Max(topK * 10, 100)
    /// 
    /// Esempi:
    /// - topK=5  → candidateLimit=100 (usa MinCandidateLimit)
    /// - topK=20 → candidateLimit=200 (10x multiplier)
    /// - topK=50 → candidateLimit=500 (10x multiplier)
    /// </code>
    /// 
    /// <para><strong>Gestione errori SQL Server VECTOR_DISTANCE:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>SqlException 207:</strong> Invalid column name → VECTOR columns non esistono in schema</description></item>
    /// <item><description><strong>SqlException 8116:</strong> Invalid data type → VECTOR type non riconosciuto (pre-2025)</description></item>
    /// <item><description><strong>ArgumentException:</strong> Dimensione embedding non supportata (non 768/1536)</description></item>
    /// <item><description>Tutti questi errori triggherano graceful fallback senza interrompere ricerca</description></item>
    /// </list>
    /// 
    /// <para><strong>Doppia granularità ricerca:</strong></para>
    /// <list type="number">
    /// <item><description><strong>Document-level:</strong> Cerca su embedding completo documento (EmbeddingVector768/1536)</description></item>
    /// <item><description><strong>Chunk-level:</strong> Cerca su embedding singoli chunk (ChunkEmbedding768/1536)</description></item>
    /// <item><description>Combina risultati prioritizzando chunk (più specifici e precisi)</description></item>
    /// <item><description>Usa HashSet per deduplicazione documenti già presenti via chunks</description></item>
    /// </list>
    /// 
    /// <para><strong>Query projection ottimizzata:</strong></para>
    /// <code>
    /// .Select(d => new { d.Id, d.FileName, d.ActualCategory, d.ExtractedText, d.EmbeddingVector768, d.EmbeddingVector1536 })
    /// </code>
    /// Vantaggi:
    /// <list type="bullet">
    /// <item><description>Carica solo colonne necessarie (no navigation properties inutili)</description></item>
    /// <item><description>Riduce trasferimento dati database → memoria</description></item>
    /// <item><description>EF Core genera SQL ottimizzato con SELECT specifico</description></item>
    /// <item><description>Oggetti anonymous type leggeri (no overhead entity tracking)</description></item>
    /// </list>
    /// 
    /// <para><strong>AsNoTracking() optimization:</strong></para>
    /// Applicato a tutte le query perché:
    /// <list type="bullet">
    /// <item><description>Read-only operation: no modifiche ai documenti/chunk</description></item>
    /// <item><description>Performance: ~30% faster senza change tracking overhead</description></item>
    /// <item><description>Memoria: No snapshot tracking allocations</description></item>
    /// <item><description>Scalabilità: Supporta query su dataset grandi</description></item>
    /// </list>
    /// 
    /// <para><strong>VectorMathHelper.CosineSimilarity:</strong></para>
    /// Calcola similarità vettoriale ottimizzata:
    /// <list type="bullet">
    /// <item><description>Implementazione SIMD-accelerated dove possibile</description></item>
    /// <item><description>Gestisce automaticamente vettori di dimensioni diverse</description></item>
    /// <item><description>Range output: 0.0 (completamente diversi) a 1.0 (identici)</description></item>
    /// <item><description>Threshold tipico: 0.7+ per risultati rilevanti</description></item>
    /// </list>
    /// 
    /// <para><strong>Logging dettagliato:</strong></para>
    /// <list type="bullet">
    /// <item><description>Debug: Strategia utilizzata e numero candidati</description></item>
    /// <item><description>Information: Successo VECTOR_DISTANCE con dettagli tecnici</description></item>
    /// <item><description>Warning: Fallback a strategie alternative con motivo</description></item>
    /// <item><description>Error: Errori gravi con stack trace completo</description></item>
    /// </list>
    /// </remarks>
    private async Task<List<RelevantDocumentResult>> SearchDocumentsWithEmbeddingDatabaseAsync(
        float[] queryEmbedding,
        string userId,
        int topK = 10,
        double minSimilarity = 0.7)
    {
        try
        {
            _logger.LogDebug("Performing database-optimized vector search for user: {UserId}", userId);

            // Check if we're using SQL Server (vs in-memory database for testing)
            var isSqlServer = _context.Database.IsSqlServer();
            
            if (!isSqlServer)
            {
                _logger.LogDebug("Not using SQL Server, falling back to full in-memory search");
                return await SearchDocumentsWithEmbeddingAsync(queryEmbedding, userId, topK, minSimilarity);
            }

            // Try to use SQL Server VECTOR_DISTANCE if available (SQL Server 2025+)
            try
            {
                return await SearchWithVectorDistanceAsync(queryEmbedding, userId, topK, minSimilarity);
            }
            catch (Microsoft.Data.SqlClient.SqlException sqlEx)
            {
                // Check if error is due to VECTOR type not being supported (older SQL Server version)
                // SQL error numbers indicating VECTOR support is not available:
                // - 207: Invalid column name (VECTOR columns don't exist in schema)
                // - 8116: Argument data type is invalid for argument (VECTOR type not recognized)
                bool isVectorNotSupported = sqlEx.Number == 207 || sqlEx.Number == 8116;
                
                if (isVectorNotSupported)
                {
                    _logger.LogInformation(
                        "SQL Server VECTOR_DISTANCE not available (SQL error {ErrorNumber}). " +
                        "This requires SQL Server 2025+. Falling back to optimized in-memory calculation.",
                        sqlEx.Number);
                }
                else
                {
                    _logger.LogWarning(sqlEx, "SQL error during VECTOR_DISTANCE search (error {ErrorNumber}), falling back to in-memory calculation", 
                        sqlEx.Number);
                }
                // Fall through to optimized in-memory approach
            }
            catch (ArgumentException argEx)
            {
                // Unsupported embedding dimension (not 768 or 1536) - fall back to in-memory calculation
                // which can handle any dimension size
                _logger.LogInformation(argEx, "VECTOR_DISTANCE requires 768 or 1536 dimensions, got {Dimension}. Falling back to in-memory calculation.", 
                    queryEmbedding.Length);
                // Fall through to optimized in-memory approach
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unexpected error during VECTOR_DISTANCE search, falling back to in-memory calculation");
                // Fall through to optimized in-memory approach
            }

            // Optimized approach: Limit candidate set at database level before in-memory calculation
            // This significantly reduces memory usage and computation compared to loading ALL documents
            
            var candidateLimit = Math.Max(topK * CandidateLimitMultiplier, MinCandidateLimit); // Get reasonable number of candidates
            
            // Get recent documents with embeddings (most recent are often most relevant)
            // Query the actual mapped fields: EmbeddingVector768 or EmbeddingVector1536
            var documents = await _context.Documents
                .AsNoTracking()
                .Where(d => d.OwnerId == userId && (d.EmbeddingVector768 != null || d.EmbeddingVector1536 != null))
                .OrderByDescending(d => d.UploadedAt)
                .Take(candidateLimit)
                .Select(d => new { d.Id, d.FileName, d.ActualCategory, d.ExtractedText, d.EmbeddingVector768, d.EmbeddingVector1536 })
                .ToListAsync();

            _logger.LogDebug("Retrieved {Count} candidate documents for similarity calculation", documents.Count);

            // Calculate similarity scores for documents
            var scoredDocs = new List<(int id, string fileName, string? category, string? text, double score)>();
            foreach (var doc in documents)
            {
                // Use the populated vector field
                var docEmbedding = doc.EmbeddingVector768 ?? doc.EmbeddingVector1536;
                if (docEmbedding == null) continue;

                var similarity = VectorMathHelper.CosineSimilarity(queryEmbedding, docEmbedding);
                if (similarity >= minSimilarity)
                {
                    scoredDocs.Add((doc.Id, doc.FileName, doc.ActualCategory, doc.ExtractedText, similarity));
                }
            }

            // Get recent chunks with embeddings
            // Query the actual mapped fields: ChunkEmbedding768 or ChunkEmbedding1536
            var chunks = await _context.DocumentChunks
                .AsNoTracking()
                .Include(c => c.Document)
                .Where(c => c.Document!.OwnerId == userId && (c.ChunkEmbedding768 != null || c.ChunkEmbedding1536 != null))
                .OrderByDescending(c => c.CreatedAt)
                .Take(candidateLimit)
                .Select(c => new { 
                    c.DocumentId, 
                    c.Document!.FileName, 
                    c.Document.ActualCategory, 
                    c.ChunkText, 
                    c.ChunkIndex, 
                    c.ChunkEmbedding768,
                    c.ChunkEmbedding1536
                })
                .ToListAsync();

            _logger.LogDebug("Retrieved {Count} candidate chunks for similarity calculation", chunks.Count);

            // Calculate similarity scores for chunks
            var scoredChunks = new List<(int docId, string fileName, string? category, string chunkText, int chunkIndex, double score)>();
            foreach (var chunk in chunks)
            {
                // Use the populated vector field
                var chunkEmbedding = chunk.ChunkEmbedding768 ?? chunk.ChunkEmbedding1536;
                if (chunkEmbedding == null) continue;

                var similarity = VectorMathHelper.CosineSimilarity(queryEmbedding, chunkEmbedding);
                if (similarity >= minSimilarity)
                {
                    scoredChunks.Add((chunk.DocumentId, chunk.FileName, chunk.ActualCategory, chunk.ChunkText, chunk.ChunkIndex, similarity));
                }
            }

            // Combine results - prioritize chunks over full documents
            var results = new List<RelevantDocumentResult>();

            // Add chunk-based results (higher priority due to more granular matching)
            foreach (var (docId, fileName, category, chunkText, chunkIndex, score) in scoredChunks.OrderByDescending(x => x.score).Take(topK))
            {
                results.Add(new RelevantDocumentResult
                {
                    DocumentId = docId,
                    FileName = fileName,
                    Category = category,
                    SimilarityScore = score,
                    RelevantChunk = chunkText,
                    ChunkIndex = chunkIndex
                });
            }

            // Add document-level results if we don't have enough chunks
            if (results.Count < topK)
            {
                var existingDocIds = new HashSet<int>(results.Select(r => r.DocumentId));
                foreach (var (id, fileName, category, text, score) in scoredDocs.OrderByDescending(x => x.score))
                {
                    if (existingDocIds.Contains(id))
                        continue;

                    results.Add(new RelevantDocumentResult
                    {
                        DocumentId = id,
                        FileName = fileName,
                        Category = category,
                        SimilarityScore = score,
                        ExtractedText = text
                    });
                    
                    existingDocIds.Add(id);
                    
                    if (results.Count >= topK)
                        break;
                }
            }

            _logger.LogInformation("Database-optimized search: processed {DocCount} docs + {ChunkCount} chunks, found {ResultCount} results above {MinSim:P0} threshold", 
                documents.Count, chunks.Count, results.Count, minSimilarity);
            return results.Take(topK).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in database-optimized vector search, falling back to full in-memory");
            return await SearchDocumentsWithEmbeddingAsync(queryEmbedding, userId, topK, minSimilarity);
        }
    }

    /// <summary>
    /// Esegue ricerca vettoriale utilizzando la funzione VECTOR_DISTANCE nativa di SQL Server 2025
    /// Fornisce calcolo di similarità vettoriale a livello database per prestazioni ottimali
    /// </summary>
    /// <param name="queryEmbedding">Vettore embedding della query (768 o 1536 dimensioni)</param>
    /// <param name="userId">ID utente per filtro isolamento tenant</param>
    /// <param name="topK">Numero massimo di risultati da restituire</param>
    /// <param name="minSimilarity">Soglia minima di similarità coseno 0-1 (default: 0.7 = 70%)</param>
    /// <returns>Lista di documenti rilevanti ordinati per similarità decrescente</returns>
    /// <exception cref="ArgumentException">Se embedding dimension non è 768 o 1536</exception>
    /// <exception cref="Microsoft.Data.SqlClient.SqlException">Se SQL Server non supporta VECTOR type (pre-2025)</exception>
    /// <remarks>
    /// <para><strong>Requisiti:</strong></para>
    /// <list type="bullet">
    /// <item><description>SQL Server 2025+ con supporto VECTOR type nativo</description></item>
    /// <item><description>Embedding dimension: 768 o 1536 (whitelist per sicurezza)</description></item>
    /// <item><description>Colonne VECTOR popolate nei database (EmbeddingVector768/1536, ChunkEmbedding768/1536)</description></item>
    /// </list>
    /// 
    /// <para><strong>Vantaggi VECTOR_DISTANCE nativo:</strong></para>
    /// <list type="bullet">
    /// <item><description>Calcolo similarità ottimizzato a livello database engine</description></item>
    /// <item><description>Indicizzazione automatica vettori per query veloci</description></item>
    /// <item><description>Riduce trasferimento dati client-server (solo risultati finali)</description></item>
    /// <item><description>Supporto funzioni aggregate e window functions su vettori</description></item>
    /// <item><description>Performance 10-100x superiori rispetto a calcolo in-memory</description></item>
    /// </list>
    /// 
    /// <para><strong>Query SQL - Strategia CTE (Common Table Expression):</strong></para>
    /// <list type="number">
    /// <item><description><strong>DocumentScores CTE:</strong> Calcola similarità a livello documento completo</description></item>
    /// <item><description><strong>ChunkScores CTE:</strong> Calcola similarità a livello chunk granulare</description></item>
    /// <item><description><strong>UNION ALL:</strong> Combina risultati dando priorità ai chunk (più precisi)</description></item>
    /// <item><description><strong>ORDER BY SimilarityScore:</strong> Ordina per rilevanza decrescente</description></item>
    /// </list>
    /// 
    /// <para><strong>Sicurezza SQL Injection:</strong></para>
    /// <list type="bullet">
    /// <item><description>Column names: Derivati da whitelist compile-time (768/1536) → Sicuri</description></item>
    /// <item><description>User input: Parametrizzato tramite SqlParameter → Sicuro</description></item>
    /// <item><description>Embedding JSON: Serializzato con System.Text.Json → Sicuro</description></item>
    /// </list>
    /// 
    /// <para><strong>Gestione errori:</strong></para>
    /// <list type="bullet">
    /// <item><description>SqlException 207/8116: VECTOR type non supportato → fallback in-memory</description></item>
    /// <item><description>ArgumentException: Dimensione non supportata → fallback in-memory</description></item>
    /// <item><description>Altre eccezioni: Logged e fallback a strategia alternativa</description></item>
    /// </list>
    /// 
    /// <para><strong>Formato risultati:</strong></para>
    /// Restituisce mix di:
    /// <list type="bullet">
    /// <item><description>Chunk results (SourceType='CHUNK'): Include ChunkText e ChunkIndex specifici</description></item>
    /// <item><description>Document results (SourceType='DOCUMENT'): Include ExtractedText completo</description></item>
    /// </list>
    /// 
    /// <para><strong>Connection management:</strong></para>
    /// Usa GetDbConnection() del DbContext esistente per beneficiare di:
    /// <list type="bullet">
    /// <item><description>Connection pooling EF Core</description></item>
    /// <item><description>Gestione automatica lifetime connessione</description></item>
    /// <item><description>Integration con transaction scope EF</description></item>
    /// </list>
    /// </remarks>
    private async Task<List<RelevantDocumentResult>> SearchWithVectorDistanceAsync(
        float[] queryEmbedding,
        string userId,
        int topK = 10,
        double minSimilarity = 0.7)
    {
        // Determine which vector field to use based on embedding dimension
        var embeddingDimension = queryEmbedding.Length;
        string docVectorColumn;
        string chunkVectorColumn;
        
        // Use whitelist approach for security - only allow known valid column names
        if (embeddingDimension == Utilities.EmbeddingValidationHelper.SupportedDimension768)
        {
            docVectorColumn = "EmbeddingVector768";
            chunkVectorColumn = "ChunkEmbedding768";
        }
        else if (embeddingDimension == Utilities.EmbeddingValidationHelper.SupportedDimension1536)
        {
            docVectorColumn = "EmbeddingVector1536";
            chunkVectorColumn = "ChunkEmbedding1536";
        }
        else
        {
            throw new ArgumentException(
                $"Unsupported embedding dimension: {embeddingDimension}. " +
                $"Expected {Utilities.EmbeddingValidationHelper.SupportedDimension768} or " +
                $"{Utilities.EmbeddingValidationHelper.SupportedDimension1536}.");
        }
        
        // Column names are validated above from a whitelist, safe to use in SQL
        // Security model: Column names come from compile-time constants (SupportedDimension768/1536)
        // mapped to hardcoded strings. No user input or external data can reach the column name variables.
        // This is a safe and common pattern for schema-level parameterization.

        // Serialize query embedding to JSON format (required for VECTOR type)
        var embeddingJson = System.Text.Json.JsonSerializer.Serialize(queryEmbedding);

        // Use raw SQL with VECTOR_DISTANCE function for document-level search
        // Note: This requires SQL Server 2025 with VECTOR type support
        var sql = $@"
            WITH DocumentScores AS (
                SELECT TOP (@topK)
                    d.Id,
                    d.FileName,
                    d.ActualCategory,
                    d.ExtractedText,
                    CAST(VECTOR_DISTANCE('cosine', d.{docVectorColumn}, CAST(@queryEmbedding AS VECTOR({embeddingDimension}))) AS FLOAT) AS SimilarityScore
                FROM Documents d
                WHERE d.OwnerId = @userId
                    AND d.{docVectorColumn} IS NOT NULL
                    AND VECTOR_DISTANCE('cosine', d.{docVectorColumn}, CAST(@queryEmbedding AS VECTOR({embeddingDimension}))) >= @minSimilarity
                ORDER BY SimilarityScore DESC
            ),
            ChunkScores AS (
                SELECT TOP (@topK)
                    dc.DocumentId AS Id,
                    d.FileName,
                    d.ActualCategory,
                    dc.ChunkText,
                    dc.ChunkIndex,
                    CAST(VECTOR_DISTANCE('cosine', dc.{chunkVectorColumn}, CAST(@queryEmbedding AS VECTOR({embeddingDimension}))) AS FLOAT) AS SimilarityScore
                FROM DocumentChunks dc
                INNER JOIN Documents d ON dc.DocumentId = d.Id
                WHERE d.OwnerId = @userId
                    AND dc.{chunkVectorColumn} IS NOT NULL
                    AND VECTOR_DISTANCE('cosine', dc.{chunkVectorColumn}, CAST(@queryEmbedding AS VECTOR({embeddingDimension}))) >= @minSimilarity
                ORDER BY SimilarityScore DESC
            )
            SELECT 
                Id, 
                FileName, 
                ActualCategory, 
                CAST(NULL AS NVARCHAR(MAX)) AS ExtractedText, 
                ChunkText, 
                ChunkIndex, 
                SimilarityScore,
                'CHUNK' AS SourceType
            FROM ChunkScores
            UNION ALL
            SELECT 
                Id, 
                FileName, 
                ActualCategory, 
                ExtractedText, 
                CAST(NULL AS NVARCHAR(MAX)) AS ChunkText, 
                CAST(NULL AS INT) AS ChunkIndex, 
                SimilarityScore,
                'DOCUMENT' AS SourceType
            FROM DocumentScores
            ORDER BY SimilarityScore DESC";

        // Execute the query
        using var command = _context.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        
        var embeddingParam = command.CreateParameter();
        embeddingParam.ParameterName = "@queryEmbedding";
        embeddingParam.Value = embeddingJson;
        command.Parameters.Add(embeddingParam);
        
        var userIdParam = command.CreateParameter();
        userIdParam.ParameterName = "@userId";
        userIdParam.Value = userId;
        command.Parameters.Add(userIdParam);
        
        var topKParam = command.CreateParameter();
        topKParam.ParameterName = "@topK";
        topKParam.Value = topK * CandidateLimitMultiplier; // Get more candidates for merging
        command.Parameters.Add(topKParam);
        
        var minSimParam = command.CreateParameter();
        minSimParam.ParameterName = "@minSimilarity";
        minSimParam.Value = minSimilarity;
        command.Parameters.Add(minSimParam);

        await _context.Database.OpenConnectionAsync();

        var results = new List<RelevantDocumentResult>();
        var existingDocIds = new HashSet<int>();

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var id = reader.GetInt32(0);
            var fileName = reader.GetString(1);
            var category = reader.IsDBNull(2) ? null : reader.GetString(2);
            var extractedText = reader.IsDBNull(3) ? null : reader.GetString(3);
            var chunkText = reader.IsDBNull(4) ? null : reader.GetString(4);
            var chunkIndex = reader.IsDBNull(5) ? (int?)null : reader.GetInt32(5);
            var score = reader.GetDouble(6);
            var sourceType = reader.GetString(7);

            // Prioritize chunks, avoid duplicate documents
            if (sourceType == "CHUNK" || !existingDocIds.Contains(id))
            {
                results.Add(new RelevantDocumentResult
                {
                    DocumentId = id,
                    FileName = fileName,
                    Category = category,
                    SimilarityScore = score,
                    RelevantChunk = chunkText,
                    ChunkIndex = chunkIndex,
                    ExtractedText = extractedText
                });
                
                if (sourceType == "DOCUMENT")
                    existingDocIds.Add(id);

                if (results.Count >= topK)
                    break;
            }
        }

        _logger.LogInformation(
            "✅ SQL Server VECTOR_DISTANCE search completed successfully using {VectorField} ({Dimension} dimensions): found {Count} results above {MinSim:P0} threshold", 
            docVectorColumn, embeddingDimension, results.Count, minSimilarity);
        return results;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <para><strong>Implementazione fallback completa:</strong></para>
    /// Questo metodo implementa ricerca vettoriale in-memory quando VECTOR_DISTANCE non è disponibile
    /// 
    /// <para><strong>Strategia:</strong></para>
    /// <list type="number">
    /// <item><description>Carica TUTTI i documenti con embeddings per l'utente dal database</description></item>
    /// <item><description>Carica TUTTI i chunk con embeddings per l'utente dal database</description></item>
    /// <item><description>Calcola similarità coseno in-memory per ogni documento</description></item>
    /// <item><description>Calcola similarità coseno in-memory per ogni chunk</description></item>
    /// <item><description>Filtra risultati >= minSimilarity threshold</description></item>
    /// <item><description>Combina e prioritizza chunk (più precisi) su documenti completi</description></item>
    /// </list>
    /// 
    /// <para><strong>Vantaggi:</strong></para>
    /// <list type="bullet">
    /// <item><description>Compatibilità universale (qualsiasi database, anche in-memory per testing)</description></item>
    /// <item><description>Supporta qualsiasi dimensione embedding (non limitato a 768/1536)</description></item>
    /// <item><description>Logica trasparente e debuggabile</description></item>
    /// </list>
    /// 
    /// <para><strong>Svantaggi:</strong></para>
    /// <list type="bullet">
    /// <item><description>Carica tutti i dati in memoria (potenzialmente alto consumo RAM)</description></item>
    /// <item><description>Calcolo CPU-intensive per dataset grandi</description></item>
    /// <item><description>Non scala bene oltre 10k+ documenti</description></item>
    /// <item><description>Performance 10-100x inferiori rispetto a VECTOR_DISTANCE nativo</description></item>
    /// </list>
    /// 
    /// <para><strong>Ottimizzazione AsNoTracking():</strong></para>
    /// Usa AsNoTracking() perché:
    /// <list type="bullet">
    /// <item><description>Documenti e chunk sono read-only per questa operazione</description></item>
    /// <item><description>Riduce overhead EF Core change tracking (~30% performance gain)</description></item>
    /// <item><description>Riduce memoria allocata per snapshot tracking</description></item>
    /// </list>
    /// 
    /// <para><strong>Gestione Include():</strong></para>
    /// Per chunk, usa Include(c => c.Document) per:
    /// <list type="bullet">
    /// <item><description>Eager loading relazione Document (evita N+1 queries)</description></item>
    /// <item><description>Accesso a FileName e Category del documento parent</description></item>
    /// <item><description>Single query con JOIN invece di multiple queries</description></item>
    /// </list>
    /// 
    /// <para><strong>VectorMathHelper.CosineSimilarity:</strong></para>
    /// Calcola similarità coseno tra due vettori:
    /// <list type="bullet">
    /// <item><description>Formula: cos(θ) = (A · B) / (||A|| × ||B||)</description></item>
    /// <item><description>Range: -1 (opposti) a 1 (identici), 0 (ortogonali)</description></item>
    /// <item><description>Per embeddings semantici: tipicamente 0.5-1.0</description></item>
    /// </list>
    /// 
    /// <para><strong>Combinazione risultati:</strong></para>
    /// <list type="number">
    /// <item><description>Prima: Aggiungi top chunk results (fino a topK)</description></item>
    /// <item><description>Poi: Riempi con document results se chunk < topK</description></item>
    /// <item><description>Usa HashSet per deduplicazione documenti (evita duplicati)</description></item>
    /// <item><description>Priorità chunk perché più granulari e precisi</description></item>
    /// </list>
    /// </remarks>
    public async Task<List<RelevantDocumentResult>> SearchDocumentsWithEmbeddingAsync(
        float[] queryEmbedding,
        string userId,
        int topK = 10,
        double minSimilarity = 0.7)
    {
        try
        {
            _logger.LogDebug("Searching documents with pre-generated embedding for user: {UserId}", userId);

            if (queryEmbedding == null || queryEmbedding.Length == 0)
            {
                _logger.LogWarning("Query embedding is null or empty");
                return new List<RelevantDocumentResult>();
            }

            // Get all documents with embeddings for the user
            // Query the actual mapped fields: EmbeddingVector768 or EmbeddingVector1536
            var documents = await _context.Documents
                .AsNoTracking()
                .Where(d => d.OwnerId == userId && (d.EmbeddingVector768 != null || d.EmbeddingVector1536 != null))
                .ToListAsync();

            _logger.LogDebug("Found {Count} documents with embeddings for user {UserId}", documents.Count, userId);
            _logger.LogDebug("Query embedding dimension: {Length}", queryEmbedding.Length);

            // Calculate similarity scores for documents
            var scoredDocs = new List<(Document doc, double score)>();
            foreach (var doc in documents)
            {
                // Use the EmbeddingVector property getter which returns the populated field
                var docEmbedding = doc.EmbeddingVector;
                if (docEmbedding == null)
                {
                    _logger.LogDebug("Document {FileName} (ID: {Id}) has NULL embedding vector - skipping", doc.FileName, doc.Id);
                    continue;
                }

                var similarity = VectorMathHelper.CosineSimilarity(queryEmbedding, docEmbedding);
                
                if (similarity >= minSimilarity)
                {
                    _logger.LogDebug("Document {FileName} similarity: {Similarity:P2}", doc.FileName, similarity);
                    scoredDocs.Add((doc, similarity));
                }
            }

            _logger.LogDebug("Found {Count} documents above similarity threshold {Threshold:P0}", scoredDocs.Count, minSimilarity);

            // Get chunks for better precision
            // Query the actual mapped fields: ChunkEmbedding768 or ChunkEmbedding1536
            var chunks = await _context.DocumentChunks
                .AsNoTracking()
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
            foreach (var (chunk, score) in scoredChunks.OrderByDescending(x => x.score).Take(topK))
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
            }

            // Add document-level results if we don't have enough chunks
            if (results.Count < topK)
            {
                var remaining = topK - results.Count;
                var existingDocIds = new HashSet<int>(results.Select(r => r.DocumentId));
                
                foreach (var (doc, score) in scoredDocs.OrderByDescending(x => x.score).Take(remaining))
                {
                    // Avoid duplicates using HashSet for O(1) lookup
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

    /// <summary>
    /// Genera una risposta AI utilizzando Semantic Kernel con contesto dei documenti
    /// Costruisce la chat history includendo system prompt, contesto documenti e cronologia conversazione
    /// </summary>
    /// <param name="query">Query dell'utente in linguaggio naturale</param>
    /// <param name="documentContext">Contesto formattato dei documenti rilevanti (output di BuildDocumentContext)</param>
    /// <param name="conversationHistory">Cronologia messaggi della conversazione (ultimi 10 messaggi)</param>
    /// <returns>Risposta generata dall'AI basata sul contesto fornito</returns>
    /// <exception cref="Exception">Se la generazione AI fallisce (loggata e ritorna messaggio errore)</exception>
    /// <remarks>
    /// <para><strong>Pattern Chat Completion con Semantic Kernel:</strong></para>
    /// <list type="number">
    /// <item><description>Crea ChatHistory con system prompt (istruzioni AI)</description></item>
    /// <item><description>Aggiunge document context come messaggio system</description></item>
    /// <item><description>Aggiunge conversazione storica (user/assistant alternati)</description></item>
    /// <item><description>Aggiunge query corrente come user message</description></item>
    /// <item><description>Invoca IChatCompletionService per generazione</description></item>
    /// </list>
    /// 
    /// <para><strong>Parametri AI (OpenAIPromptExecutionSettings):</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>MaxTokens:</strong> 2000 - Limita lunghezza risposta per costi e tempi ragionevoli</description></item>
    /// <item><description><strong>Temperature:</strong> 0.7 - Bilanciamento tra creatività (alto) e coerenza (basso)</description></item>
    /// <item><description><strong>TopP:</strong> 0.9 - Nucleus sampling per diversità lessicale mantenendo qualità</description></item>
    /// </list>
    /// 
    /// <para><strong>Spiegazione Temperature (0.0-2.0):</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>0.0-0.3:</strong> Deterministico, preciso, ripetibile (ideale per facts)</description></item>
    /// <item><description><strong>0.4-0.7:</strong> Bilanciato, naturale, leggermente creativo (ideale per Q&A)</description></item>
    /// <item><description><strong>0.8-1.2:</strong> Creativo, variegato, meno prevedibile</description></item>
    /// <item><description><strong>1.3-2.0:</strong> Molto creativo, rischio incoerenza</description></item>
    /// </list>
    /// 
    /// <para><strong>Spiegazione TopP/Nucleus Sampling (0.0-1.0):</strong></para>
    /// Considera solo i token la cui probabilità cumulativa raggiunge TopP:
    /// <list type="bullet">
    /// <item><description><strong>0.9:</strong> Usa top 90% probabilità → Buona diversità, mantiene qualità</description></item>
    /// <item><description><strong>1.0:</strong> Considera tutti i token → Massima diversità</description></item>
    /// <item><description><strong>0.5:</strong> Solo top 50% probabilità → Più conservativo</description></item>
    /// </list>
    /// 
    /// <para><strong>Gestione Chat History:</strong></para>
    /// <list type="bullet">
    /// <item><description>System messages: Istruzioni per l'AI (non visibili all'utente)</description></item>
    /// <item><description>User messages: Domande/input utente</description></item>
    /// <item><description>Assistant messages: Risposte AI precedenti</description></item>
    /// <item><description>Ordine cronologico: Mantiene coerenza contestuale</description></item>
    /// </list>
    /// 
    /// <para><strong>Gestione errori:</strong></para>
    /// <list type="bullet">
    /// <item><description>Verifica _chatService disponibile (null check)</description></item>
    /// <item><description>Catch generico con logging dettagliato</description></item>
    /// <item><description>Ritorna messaggio user-friendly in caso di errore</description></item>
    /// <item><description>Non blocca l'applicazione (graceful degradation)</description></item>
    /// </list>
    /// 
    /// <para><strong>Integration Semantic Kernel:</strong></para>
    /// Usa il Kernel iniettato per:
    /// <list type="bullet">
    /// <item><description>Dependency injection IChatCompletionService configurato</description></item>
    /// <item><description>Plugin e funzioni registrati (estensibilità futura)</description></item>
    /// <item><description>Logging e telemetria centralizzati</description></item>
    /// </list>
    /// </remarks>
    private async Task<string> GenerateAnswerWithSemanticKernelAsync(
        string query,
        string documentContext,
        List<Message> conversationHistory)
    {
        try
        {
            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(CreateSystemPrompt());
            chatHistory.AddSystemMessage($"DOCUMENT CONTEXT:\n{documentContext}");

            // Add conversation history
            foreach (var msg in conversationHistory)
            {
                if (msg.Role == "user")
                    chatHistory.AddUserMessage(msg.Content);
                else if (msg.Role == "assistant")
                    chatHistory.AddAssistantMessage(msg.Content);
            }

            // Add current query
            chatHistory.AddUserMessage(query);

            // Generate response
            var settings = new OpenAIPromptExecutionSettings
            {
                MaxTokens = 2000,
                Temperature = 0.7,
                TopP = 0.9
            };

            if (_chatService == null)
            {
                _logger.LogError("Chat service not available");
                return "Chat service is not configured. Please check AI provider configuration.";
            }

            var result = await _chatService.GetChatMessageContentAsync(
                chatHistory, settings, _kernel);

            return result.Content ?? "I couldn't generate a response.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating answer with Semantic Kernel");
            return $"Error generating response: {ex.Message}";
        }
    }

    /// <summary>
    /// Costruisce il contesto formattato dai documenti rilevanti per l'AI
    /// Formatta i documenti in un formato leggibile e strutturato includendo metadati e contenuto
    /// </summary>
    /// <param name="documents">Lista di documenti rilevanti con score di similarità</param>
    /// <returns>Stringa formattata contenente tutti i documenti con metadati e contenuto</returns>
    /// <remarks>
    /// Il formato include per ogni documento:
    /// - Numero documento (per citazioni)
    /// - Nome file
    /// - Categoria
    /// - Score di rilevanza
    /// - Contenuto (chunk rilevante o testo completo)
    /// </remarks>
    private string BuildDocumentContext(List<RelevantDocumentResult> documents)
    {
        var builder = new StringBuilder();
        builder.AppendLine("=== RELEVANT DOCUMENTS ===");
        builder.AppendLine();

        for (int i = 0; i < documents.Count; i++)
        {
            var doc = documents[i];
            builder.AppendLine($"[DOCUMENT {i + 1}]");
            builder.AppendLine($"File: {doc.FileName}");
            builder.AppendLine($"Category: {doc.Category ?? "Uncategorized"}");
            builder.AppendLine($"Relevance: {doc.SimilarityScore:P0}");
            builder.AppendLine();
            builder.AppendLine("Content:");
            builder.AppendLine(doc.RelevantChunk ?? doc.ExtractedText ?? "No content available");
            builder.AppendLine();
            builder.AppendLine("---");
            builder.AppendLine();
        }

        return builder.ToString();
    }

    /// <summary>
    /// Crea il system prompt con istruzioni dettagliate per l'AI
    /// Definisce il ruolo, le regole e il formato di risposta atteso dall'assistente AI
    /// </summary>
    /// <returns>System prompt completo con linee guida per l'AI</returns>
    /// <remarks>
    /// Il prompt istruisce l'AI a:
    /// - Usare solo informazioni dai documenti forniti
    /// - Citare le fonti usando formato [Document N]
    /// - Ammettere quando non ha informazioni sufficienti
    /// - Mantenere un tono professionale e utile
    /// - Essere conciso ma completo
    /// </remarks>
    private string CreateSystemPrompt()
    {
        return @"Sei un assistente documentale intelligente basato su RAG (Retrieval-Augmented Generation).
Il tuo ruolo è rispondere accuratamente alle domande basandoti sui documenti forniti.

LINEE GUIDA:
- Usa SOLO le informazioni presenti nei documenti forniti
- Cita le fonti usando il formato [Documento N] e indica il nome del file tra parentesi: (nome_file.pdf)
- Se l'informazione non è presente nei documenti, dichiaralo chiaramente
- Sii conciso ma completo
- Mantieni un tono professionale e disponibile
- Se vengono richiesti più documenti, sintetizza le informazioni in modo appropriato
- IMPORTANTE: Rispondi sempre in italiano

FORMATO DELLA RISPOSTA:
1. Fornisci una risposta diretta alla domanda
2. Supporta con dettagli rilevanti dai documenti
3. Cita chiaramente le fonti con [Documento N] e il nome del file tra parentesi (nome_file.pdf)
4. Alla fine della risposta, elenca i documenti consultati in formato: 'Documenti consultati: (file1.pdf), (file2.docx)'
5. Se non sei sicuro, riconosci i limiti";
    }

    /// <summary>
    /// Carica la cronologia della conversazione dal database per mantenere il contesto conversazionale
    /// </summary>
    /// <param name="conversationId">ID della conversazione da caricare (null se nuova conversazione)</param>
    /// <returns>Lista di messaggi ordinati cronologicamente (massimo 10 messaggi recenti)</returns>
    /// <remarks>
    /// <para><strong>Strategia limitazione contesto:</strong></para>
    /// Limita a 10 messaggi per bilanciare:
    /// <list type="bullet">
    /// <item><description>Contesto sufficiente per conversazioni coerenti</description></item>
    /// <item><description>Token budget AI (evitare superamento limiti)</description></item>
    /// <item><description>Performance query database</description></item>
    /// <item><description>Tempi risposta ottimali</description></item>
    /// </list>
    /// 
    /// <para><strong>Ordinamento:</strong></para>
    /// <list type="number">
    /// <item><description>Query con OrderByDescending per efficienza (usa indice Timestamp)</description></item>
    /// <item><description>Take(10) server-side per limitare trasferimento dati</description></item>
    /// <item><description>Re-ordinamento client-side con OrderBy per cronologia corretta</description></item>
    /// </list>
    /// 
    /// <para><strong>Ottimizzazione:</strong></para>
    /// Usa AsNoTracking() poiché i messaggi storici sono read-only e non necessitano tracking modifiche
    /// 
    /// <para><strong>Gestione errori:</strong></para>
    /// Errori vengono loggati come warning e ritorna lista vuota (fallback graceful senza bloccare RAG)
    /// </remarks>
    private async Task<List<Message>> LoadConversationHistoryAsync(int? conversationId)
    {
        if (!conversationId.HasValue)
            return new List<Message>();

        try
        {
            return await _context.Messages
                .AsNoTracking()
                .Where(m => m.ConversationId == conversationId.Value)
                .OrderByDescending(m => m.Timestamp)
                .Take(10)
                .OrderBy(m => m.Timestamp)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error loading conversation history");
            return new List<Message>();
        }
    }

    /// <summary>
    /// Salva la conversazione nel database creando nuova conversazione o aggiornando esistente
    /// Persiste sia il messaggio utente che la risposta AI come coppia atomica
    /// </summary>
    /// <param name="conversationId">ID conversazione esistente (null per nuova conversazione)</param>
    /// <param name="userId">ID dell'utente proprietario della conversazione</param>
    /// <param name="query">Domanda dell'utente da persistere</param>
    /// <param name="answer">Risposta dell'AI da persistere</param>
    /// <param name="documentIds">IDs dei documenti referenziati nella risposta (per tracciamento)</param>
    /// <returns>ID della conversazione (nuovo se creata, esistente se aggiornata)</returns>
    /// <remarks>
    /// <para><strong>Pattern transazionale:</strong></para>
    /// <list type="bullet">
    /// <item><description>User message e assistant message salvati nella stessa transazione EF</description></item>
    /// <item><description>Garantisce consistenza: o entrambi salvati o nessuno (atomicità)</description></item>
    /// <item><description>Una sola chiamata SaveChangesAsync() per performance</description></item>
    /// </list>
    /// 
    /// <para><strong>Logica conversazione:</strong></para>
    /// <list type="number">
    /// <item><description><strong>Se conversationId presente:</strong> Carica conversazione esistente con Include(c => c.Messages)</description></item>
    /// <item><description><strong>Se conversationId null:</strong> Crea nuova conversazione con titolo = primi 60 caratteri query</description></item>
    /// <item><description>Aggiunge user message (Role="user", Content=query)</description></item>
    /// <item><description>Aggiunge assistant message (Role="assistant", Content=answer, ReferencedDocumentIds)</description></item>
    /// <item><description>Aggiorna LastMessageAt a DateTime.UtcNow</description></item>
    /// </list>
    /// 
    /// <para><strong>Tracking documenti referenziati:</strong></para>
    /// Il campo ReferencedDocumentIds permette:
    /// <list type="bullet">
    /// <item><description>Tracciare quali documenti hanno contribuito alla risposta</description></item>
    /// <item><description>Analytics su documenti più citati</description></item>
    /// <item><description>Audit trail per compliance</description></item>
    /// <item><description>Possibile navigazione documenti → conversazioni</description></item>
    /// </list>
    /// 
    /// <para><strong>Timestamp UTC:</strong></para>
    /// Usa DateTime.UtcNow per:
    /// <list type="bullet">
    /// <item><description>Consistenza timezone-independent</description></item>
    /// <item><description>Corretto ordinamento globale</description></item>
    /// <item><description>Best practice per sistemi distribuiti</description></item>
    /// </list>
    /// 
    /// <para><strong>Gestione errori:</strong></para>
    /// <list type="bullet">
    /// <item><description>InvalidOperationException se conversationId specificato non esiste</description></item>
    /// <item><description>Altre eccezioni loggiate come errore</description></item>
    /// <item><description>Fallback: ritorna conversationId originale o 0 (non blocca flusso)</description></item>
    /// </list>
    /// 
    /// <para><strong>Include() per eager loading:</strong></para>
    /// Include(c => c.Messages) per:
    /// <list type="bullet">
    /// <item><description>Evitare lazy loading che causerebbe N+1 queries</description></item>
    /// <item><description>Permettere modifica collection Messages in-memory</description></item>
    /// <item><description>Tracking EF Core della relazione parent-child</description></item>
    /// </list>
    /// </remarks>
    private async Task<int> SaveConversationAsync(
        int? conversationId,
        string userId,
        string query,
        string answer,
        List<int> documentIds)
    {
        try
        {
            Conversation conversation;

            if (conversationId.HasValue)
            {
                conversation = await _context.Conversations
                    .Include(c => c.Messages)
                    .FirstOrDefaultAsync(c => c.Id == conversationId.Value)
                    ?? throw new InvalidOperationException($"Conversation {conversationId} not found");
            }
            else
            {
                conversation = new Conversation
                {
                    UserId = userId,
                    Title = TruncateText(query, 60),
                    CreatedAt = DateTime.UtcNow,
                    LastMessageAt = DateTime.UtcNow,
                    Messages = new List<Message>()
                };
                _context.Conversations.Add(conversation);
            }

            // Add user message
            conversation.Messages.Add(new Message
            {
                Role = "user",
                Content = query,
                Timestamp = DateTime.UtcNow
            });

            // Add assistant message
            conversation.Messages.Add(new Message
            {
                Role = "assistant",
                Content = answer,
                ReferencedDocumentIds = documentIds,
                Timestamp = DateTime.UtcNow
            });

            conversation.LastMessageAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return conversation.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving conversation");
            return conversationId ?? 0;
        }
    }

    /// <summary>
    /// Tronca il testo alla lunghezza massima specificata aggiungendo ellipsis
    /// </summary>
    /// <param name="text">Testo da troncare</param>
    /// <param name="maxLength">Lunghezza massima desiderata (incluso ellipsis)</param>
    /// <returns>Testo troncato con "..." alla fine se supera maxLength, altrimenti testo originale</returns>
    /// <remarks>
    /// <para><strong>Utilizzo:</strong></para>
    /// Principalmente usato per creare titoli conversazione dai primi caratteri della query utente
    /// 
    /// <para><strong>Logica:</strong></para>
    /// <list type="bullet">
    /// <item><description>Se text è null o empty, ritorna stringa vuota</description></item>
    /// <item><description>Se text.Length ≤ maxLength, ritorna text inalterato</description></item>
    /// <item><description>Altrimenti, prende (maxLength - 3) caratteri + "..." per raggiungere esattamente maxLength</description></item>
    /// </list>
    /// 
    /// <para><strong>Esempio:</strong></para>
    /// TruncateText("Come funziona il sistema RAG?", 20) → "Come funziona il..."
    /// </remarks>
    private string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text ?? string.Empty;

        return text.Substring(0, maxLength - 3) + "...";
    }
}
