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
/// Advanced RAG service using Microsoft Semantic Kernel and Agent Framework
/// Implements vector-based retrieval and intelligent chat on uploaded documents
/// </summary>
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

    // Constants for vector search optimization
    private const int CandidateLimitMultiplier = 10; // Get 10x topK candidates for better results
    private const int MinCandidateLimit = 100; // Always get at least 100 candidates
    private const int VectorDimension = 1536; // Text-embedding-ada-002 dimension (or compatible models)

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
    /// Initialize Semantic Kernel agents for RAG workflow
    /// </summary>
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
    /// Utilizza VECTOR_DISTANCE di SQL Server 2025 quando disponibile, altrimenti calcolo in-memory ottimizzato
    /// </summary>
    /// <param name="queryEmbedding">Vettore embedding della query</param>
    /// <param name="userId">ID utente per controllo accesso</param>
    /// <param name="topK">Numero massimo di risultati da restituire</param>
    /// <param name="minSimilarity">Soglia minima di similarità (0-1)</param>
    /// <returns>Lista di documenti rilevanti ordinati per similarità</returns>
    /// <remarks>
    /// Strategia di ottimizzazione:
    /// 1. Tenta di usare VECTOR_DISTANCE nativo di SQL Server 2025 per prestazioni ottimali
    /// 2. Se non disponibile, usa approccio ottimizzato che limita i candidati al database
    /// 3. Recupera solo i documenti più recenti (candidateLimit) per ridurre carico memoria
    /// 4. Combina risultati a livello documento e chunk, prioritizzando i chunk più specifici
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
    /// Esegue ricerca vettoriale utilizzando la funzione VECTOR_DISTANCE di SQL Server 2025
    /// Fornisce calcolo di similarità vettoriale a livello database per prestazioni ottimali
    /// </summary>
    /// <param name="queryEmbedding">Vettore embedding della query</param>
    /// <param name="userId">ID utente per filtro di accesso</param>
    /// <param name="topK">Numero massimo di risultati da restituire</param>
    /// <param name="minSimilarity">Soglia minima di similarità (0-1)</param>
    /// <returns>Lista di documenti rilevanti ordinati per similarità decrescente</returns>
    /// <remarks>
    /// Questa funzione richiede SQL Server 2025 con supporto nativo per il tipo VECTOR.
    /// Esegue ricerca sia a livello di documento che di chunk, dando priorità ai chunk più specifici.
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

            // Check if using SQL Server for optimization
            var isSqlServer = _context.Database.IsSqlServer();
            
            if (isSqlServer)
            {
                // Try to use SQL Server VECTOR_DISTANCE if available (SQL Server 2025+)
                try
                {
                    return await SearchDocumentsWithVectorDistanceAsync(queryEmbedding, userId, topK, minSimilarity);
                }
                catch (Microsoft.Data.SqlClient.SqlException sqlEx)
                {
                    // SQL error codes for VECTOR type support detection
                    const int SqlErrorInvalidColumnName = 207; // Invalid column name
                    const int SqlErrorInvalidDataType = 8116; // Argument data type is invalid
                    
                    // Check if error is due to VECTOR type not being supported (older SQL Server version)
                    bool isVectorNotSupported = sqlEx.Number == SqlErrorInvalidColumnName || 
                                               sqlEx.Number == SqlErrorInvalidDataType;
                    
                    if (isVectorNotSupported)
                    {
                        _logger.LogInformation(
                            "SQL Server VECTOR_DISTANCE not available (SQL error {ErrorNumber}). " +
                            "Falling back to in-memory calculation. Consider upgrading to SQL Server 2025 for better performance.",
                            sqlEx.Number);
                        // Fall back to in-memory calculation
                        return await SearchDocumentsInMemoryAsync(queryEmbedding, userId, topK, minSimilarity);
                    }
                    else
                    {
                        _logger.LogWarning(sqlEx, "SQL error during VECTOR_DISTANCE search (error {ErrorNumber}), falling back to in-memory calculation", 
                            sqlEx.Number);
                        return await SearchDocumentsInMemoryAsync(queryEmbedding, userId, topK, minSimilarity);
                    }
                }
                catch (ArgumentException argEx)
                {
                    _logger.LogInformation(argEx, "VECTOR_DISTANCE requires 768 or 1536 dimensions, got {Dimension}. Falling back to in-memory calculation.", 
                        queryEmbedding.Length);
                    return await SearchDocumentsInMemoryAsync(queryEmbedding, userId, topK, minSimilarity);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Unexpected error during VECTOR_DISTANCE search, falling back to in-memory calculation");
                    return await SearchDocumentsInMemoryAsync(queryEmbedding, userId, topK, minSimilarity);
                }
            }
            else
            {
                // For non-SQL Server (e.g., in-memory testing), use fallback
                return await SearchDocumentsInMemoryAsync(queryEmbedding, userId, topK, minSimilarity);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching documents with embedding for user: {UserId}", userId);
            return new List<RelevantDocumentResult>();
        }
    }

    /// <summary>
    /// Perform vector search using SQL Server VECTOR_DISTANCE function for optimal performance
    /// </summary>
    private async Task<List<RelevantDocumentResult>> SearchDocumentsWithVectorDistanceAsync(
        float[] queryEmbedding,
        string userId,
        int topK,
        double minSimilarity)
    {
        // Determine which vector field to use based on embedding dimension
        var embeddingDimension = queryEmbedding.Length;
        string docVectorColumn, chunkVectorColumn;
        
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
        
        // Serialize query embedding to JSON format (required for VECTOR type)
        var embeddingJson = System.Text.Json.JsonSerializer.Serialize(queryEmbedding);

        // Use raw SQL with VECTOR_DISTANCE function for document-level search
        var docSql = $@"
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
            ORDER BY SimilarityScore DESC";

        // Use raw SQL with VECTOR_DISTANCE function for chunk-level search
        var chunkSql = $@"
            SELECT TOP (@topK)
                dc.DocumentId,
                dc.ChunkText,
                dc.ChunkIndex,
                d.FileName,
                d.ActualCategory,
                CAST(VECTOR_DISTANCE('cosine', dc.{chunkVectorColumn}, CAST(@queryEmbedding AS VECTOR({embeddingDimension}))) AS FLOAT) AS SimilarityScore
            FROM DocumentChunks dc
            INNER JOIN Documents d ON dc.DocumentId = d.Id
            WHERE d.OwnerId = @userId
                AND dc.{chunkVectorColumn} IS NOT NULL
                AND VECTOR_DISTANCE('cosine', dc.{chunkVectorColumn}, CAST(@queryEmbedding AS VECTOR({embeddingDimension}))) >= @minSimilarity
            ORDER BY SimilarityScore DESC";

        var results = new List<RelevantDocumentResult>();
        var existingDocIds = new HashSet<int>();

        await _context.Database.OpenConnectionAsync();
        var connection = _context.Database.GetDbConnection();

        // Execute chunk-level search (higher priority)
        using (var chunkCommand = connection.CreateCommand())
        {
            chunkCommand.CommandText = chunkSql;
            
            var embeddingParam = chunkCommand.CreateParameter();
            embeddingParam.ParameterName = "@queryEmbedding";
            embeddingParam.Value = embeddingJson;
            chunkCommand.Parameters.Add(embeddingParam);
            
            var topKParam = chunkCommand.CreateParameter();
            topKParam.ParameterName = "@topK";
            topKParam.Value = topK;
            chunkCommand.Parameters.Add(topKParam);
            
            var minSimParam = chunkCommand.CreateParameter();
            minSimParam.ParameterName = "@minSimilarity";
            minSimParam.Value = minSimilarity;
            chunkCommand.Parameters.Add(minSimParam);
            
            var userIdParam = chunkCommand.CreateParameter();
            userIdParam.ParameterName = "@userId";
            userIdParam.Value = userId;
            chunkCommand.Parameters.Add(userIdParam);

            using var chunkReader = await chunkCommand.ExecuteReaderAsync();
            while (await chunkReader.ReadAsync())
            {
                var documentId = chunkReader.GetInt32(0);
                var chunkText = chunkReader.GetString(1);
                var chunkIndex = chunkReader.GetInt32(2);
                var fileName = chunkReader.GetString(3);
                var category = chunkReader.IsDBNull(4) ? null : chunkReader.GetString(4);
                var score = chunkReader.GetDouble(5);

                results.Add(new RelevantDocumentResult
                {
                    DocumentId = documentId,
                    FileName = fileName,
                    Category = category,
                    SimilarityScore = score,
                    RelevantChunk = chunkText,
                    ChunkIndex = chunkIndex
                });
                existingDocIds.Add(documentId);
            }
        }

        // Add document-level results if we don't have enough chunks
        if (results.Count < topK)
        {
            var remaining = topK - results.Count;
            
            using (var docCommand = connection.CreateCommand())
            {
                docCommand.CommandText = docSql;
                
                var embeddingParam = docCommand.CreateParameter();
                embeddingParam.ParameterName = "@queryEmbedding";
                embeddingParam.Value = embeddingJson;
                docCommand.Parameters.Add(embeddingParam);
                
                var topKParam = docCommand.CreateParameter();
                topKParam.ParameterName = "@topK";
                topKParam.Value = remaining * 2; // Get extra to filter out duplicates
                docCommand.Parameters.Add(topKParam);
                
                var minSimParam = docCommand.CreateParameter();
                minSimParam.ParameterName = "@minSimilarity";
                minSimParam.Value = minSimilarity;
                docCommand.Parameters.Add(minSimParam);
                
                var userIdParam = docCommand.CreateParameter();
                userIdParam.ParameterName = "@userId";
                userIdParam.Value = userId;
                docCommand.Parameters.Add(userIdParam);

                using var docReader = await docCommand.ExecuteReaderAsync();
                while (await docReader.ReadAsync() && results.Count < topK)
                {
                    var documentId = docReader.GetInt32(0);
                    
                    // Skip if already included from chunks
                    if (existingDocIds.Contains(documentId))
                        continue;

                    var fileName = docReader.GetString(1);
                    var category = docReader.IsDBNull(2) ? null : docReader.GetString(2);
                    var extractedText = docReader.IsDBNull(3) ? null : docReader.GetString(3);
                    var score = docReader.GetDouble(4);

                    results.Add(new RelevantDocumentResult
                    {
                        DocumentId = documentId,
                        FileName = fileName,
                        Category = category,
                        SimilarityScore = score,
                        ExtractedText = extractedText
                    });
                    existingDocIds.Add(documentId);
                }
            }
        }

        _logger.LogInformation(
            "✅ SQL Server VECTOR_DISTANCE search completed successfully using {VectorField} ({Dimension} dimensions): found {Count} results above {MinSim:P0} threshold", 
            docVectorColumn, embeddingDimension, results.Count, minSimilarity);

        return results;
    }

    /// <summary>
    /// Fallback vector search using in-memory cosine similarity calculation
    /// Used when VECTOR_DISTANCE is not available (older SQL Server or non-SQL Server database)
    /// </summary>
    private async Task<List<RelevantDocumentResult>> SearchDocumentsInMemoryAsync(
        float[] queryEmbedding,
        string userId,
        int topK,
        double minSimilarity)
    {
        // Get all documents with embeddings for the user
        // Query the actual mapped fields: EmbeddingVector768 or EmbeddingVector1536
        var documents = await _context.Documents
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

        _logger.LogDebug("Returning {Count} total results (in-memory calculation)", results.Count);
        return results;
    }

    /// <summary>
    /// Genera una risposta utilizzando Semantic Kernel con contesto dei documenti
    /// Costruisce la chat history includendo system prompt, contesto documenti e cronologia conversazione
    /// </summary>
    /// <param name="query">Query dell'utente</param>
    /// <param name="documentContext">Contesto formattato dei documenti rilevanti</param>
    /// <param name="conversationHistory">Cronologia messaggi della conversazione</param>
    /// <returns>Risposta generata dall'AI basata sul contesto fornito</returns>
    /// <remarks>
    /// Utilizza le seguenti impostazioni:
    /// - MaxTokens: 2000
    /// - Temperature: 0.7 (equilibrio tra creatività e coerenza)
    /// - TopP: 0.9 (nucleus sampling)
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
    /// Carica la cronologia della conversazione dal database
    /// Recupera gli ultimi messaggi per mantenere il contesto conversazionale
    /// </summary>
    /// <param name="conversationId">ID della conversazione da caricare</param>
    /// <returns>Lista di messaggi ordinati cronologicamente (massimo 10 messaggi recenti)</returns>
    /// <remarks>
    /// Limita a 10 messaggi per evitare di sovraccaricare il contesto dell'AI
    /// e mantenere tempi di risposta ottimali
    /// </remarks>
    private async Task<List<Message>> LoadConversationHistoryAsync(int? conversationId)
    {
        if (!conversationId.HasValue)
            return new List<Message>();

        try
        {
            return await _context.Messages
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
    /// Salva la conversazione nel database
    /// Crea una nuova conversazione se necessario o aggiorna quella esistente
    /// </summary>
    /// <param name="conversationId">ID conversazione esistente (null per nuova conversazione)</param>
    /// <param name="userId">ID dell'utente proprietario della conversazione</param>
    /// <param name="query">Domanda dell'utente da salvare</param>
    /// <param name="answer">Risposta dell'AI da salvare</param>
    /// <param name="documentIds">IDs dei documenti referenziati nella risposta</param>
    /// <returns>ID della conversazione (nuovo o esistente)</returns>
    /// <remarks>
    /// Salva sia il messaggio utente che la risposta AI come coppia di messaggi
    /// nella stessa transazione per garantire consistenza
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
    /// Tronca il testo alla lunghezza massima specificata
    /// Aggiunge "..." alla fine se il testo supera la lunghezza massima
    /// </summary>
    /// <param name="text">Testo da troncare</param>
    /// <param name="maxLength">Lunghezza massima desiderata</param>
    /// <returns>Testo troncato con ellipsis se necessario</returns>
    private string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text ?? string.Empty;

        return text.Substring(0, maxLength - 3) + "...";
    }
}
