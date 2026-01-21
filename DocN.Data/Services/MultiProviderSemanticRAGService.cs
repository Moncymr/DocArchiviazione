using DocN.Core.Interfaces;
using DocN.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.RegularExpressions;

namespace DocN.Data.Services;

/// <summary>
/// Implementation of ISemanticRAGService using MultiProviderAIService.
/// Supports Gemini, OpenAI, and Azure OpenAI configured in the database.
/// </summary>
/// <remarks>
/// Provider AI viene iniettato via DI e carica configurazione automaticamente.
/// Per dettagli: Vedi RAG_PROVIDER_INITIALIZATION_GUIDE.md
/// </remarks>
public class MultiProviderSemanticRAGService : ISemanticRAGService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<MultiProviderSemanticRAGService> _logger;
    private readonly IMultiProviderAIService _aiService;
    
    // Compiled regex for better performance
    private static readonly Regex NumberExtractorRegex = new Regex(@"\d+", RegexOptions.Compiled);
    
    // Constants for fallback search
    private const double FallbackSearchSimilarityScore = 0.6;

    public MultiProviderSemanticRAGService(
        ApplicationDbContext context,
        ILogger<MultiProviderSemanticRAGService> logger,
        IMultiProviderAIService aiService)
    {
        _context = context;
        _logger = logger;
        _aiService = aiService;
    }

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
            _logger.LogInformation(
                "Generating RAG response using MultiProvider for query: {Query}, User: {UserId}",
                query, userId);

            // Step 1: Search for relevant documents with improved strategy
            // Try with lower threshold first (0.5 instead of 0.7) to find more documents
            var relevantDocs = await SearchDocumentsAsync(query, userId, topK, 0.5);
            
            // If no results found, try keyword/fallback search
            if (!relevantDocs.Any())
            {
                _logger.LogInformation("No documents found with semantic search, trying keyword fallback for query: {Query}", query);
                relevantDocs = await FallbackKeywordSearch(query, userId, topK);
            }

            // Step 2: Load conversation history
            var conversationHistory = await LoadConversationHistoryAsync(conversationId);

            // Step 3: Generate response
            string answer;
            string documentContext;

            if (!relevantDocs.Any())
            {
                _logger.LogWarning("No relevant documents found for query: {Query}. Returning message about internal documents only.", query);

                // Don't generate AI response - inform user about internal document focus
                answer = @"❌ Non ho trovato documenti rilevanti nel sistema RAG interno per rispondere alla tua domanda.

Questo sistema di chat AI funziona PRINCIPALMENTE sui documenti interni che hai caricato nel sistema. 

📄 Per ottenere risposte accurate:
1. Assicurati di aver caricato i documenti necessari nella sezione 'Upload'
2. Verifica che i documenti siano stati elaborati correttamente (con embedding generati)
3. Riprova la tua domanda una volta caricati i documenti pertinenti

💡 Suggerimento: Puoi verificare i tuoi documenti caricati nella sezione 'Documents' del sistema.

Il sistema non fornisce risposte basate su conoscenze generali, ma solo su informazioni contenute nei documenti che hai caricato.";
                
                documentContext = "No relevant documents found";
            }
            else
            {
                // Build context from relevant documents
                documentContext = BuildDocumentContext(relevantDocs);

                // Step 4: Generate response using MultiProviderAIService with document context
                var systemPrompt = CreateSystemPrompt();
                var userPrompt = BuildUserPrompt(query, documentContext, conversationHistory);
                answer = await _aiService.GenerateChatCompletionAsync(systemPrompt, userPrompt);
            }

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
                    ["hasConversationHistory"] = conversationHistory.Any(),
                    ["provider"] = _aiService.GetCurrentChatProvider()
                }
            };

            _logger.LogInformation(
                "RAG response generated in {ElapsedMs}ms with {DocCount} documents using {Provider}",
                stopwatch.ElapsedMilliseconds, relevantDocs.Count, _aiService.GetCurrentChatProvider());

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

    public async IAsyncEnumerable<string> GenerateStreamingResponseAsync(
        string query,
        string userId,
        int? conversationId = null,
        List<int>? specificDocumentIds = null)
    {
        _logger.LogInformation("Streaming not supported in MultiProvider mode, using non-streaming response");

        // Fallback to non-streaming
        var response = await GenerateResponseAsync(query, userId, conversationId, specificDocumentIds, 5);
        yield return response.Answer;
    }

    public async Task<List<RelevantDocumentResult>> SearchDocumentsAsync(
        string query,
        string userId,
        int topK = 10,
        double minSimilarity = 0.7)
    {
        try
        {
            _logger.LogInformation("Searching documents for query: '{Query}', User: {UserId}, TopK: {TopK}, MinSimilarity: {MinSimilarity}",
                query, userId, topK, minSimilarity);

            // Generate query embedding using MultiProviderAIService (with automatic fallback)
            var queryEmbedding = await _aiService.GenerateEmbeddingAsync(query);
            if (queryEmbedding == null)
            {
                _logger.LogError("Failed to generate query embedding - all providers failed or returned null");
                return new List<RelevantDocumentResult>();
            }

            _logger.LogInformation("Successfully generated query embedding with {Dimensions} dimensions using provider: {Provider}",
                queryEmbedding.Length, _aiService.GetCurrentEmbeddingProvider());

            var results = await SearchDocumentsWithEmbeddingAsync(queryEmbedding, userId, topK, minSimilarity);

            _logger.LogInformation("Document search completed: Found {Count} relevant documents", results.Count);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching documents for query: {Query}", query);
            return new List<RelevantDocumentResult>();
        }
    }

    public async Task<List<RelevantDocumentResult>> SearchDocumentsWithEmbeddingAsync(
        float[] queryEmbedding,
        string userId,
        int topK = 10,
        double minSimilarity = 0.7)
    {
        try
        {
            _logger.LogInformation("Searching documents with embedding for user: {UserId}, Embedding dimensions: {Dimensions}",
                userId, queryEmbedding?.Length ?? 0);

            if (queryEmbedding == null || queryEmbedding.Length == 0)
            {
                _logger.LogWarning("Query embedding is null or empty");
                return new List<RelevantDocumentResult>();
            }

            // Check if we're using SQL Server (vs in-memory database for testing)
            var isSqlServer = _context.Database.IsSqlServer();
            
            if (!isSqlServer)
            {
                _logger.LogDebug("Not using SQL Server, falling back to full in-memory search");
                return await SearchDocumentsInMemoryAsync(queryEmbedding, userId, topK, minSimilarity);
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
                bool isVectorNotSupported = sqlEx.Number == 207 || sqlEx.Number == 8116 || sqlEx.Number == 102;
                
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
                
                // Fallback to in-memory calculation
                return await SearchDocumentsInMemoryAsync(queryEmbedding, userId, topK, minSimilarity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during SQL vector search, falling back to in-memory calculation");
                return await SearchDocumentsInMemoryAsync(queryEmbedding, userId, topK, minSimilarity);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching documents with embedding");
            return new List<RelevantDocumentResult>();
        }
    }

    /// <summary>
    /// SQL-optimized vector search using VECTOR_DISTANCE function (SQL Server 2025+)
    /// </summary>
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
        if (embeddingDimension == 768)
        {
            docVectorColumn = "EmbeddingVector768";
            chunkVectorColumn = "ChunkEmbedding768";
        }
        else if (embeddingDimension == 1536)
        {
            docVectorColumn = "EmbeddingVector1536";
            chunkVectorColumn = "ChunkEmbedding1536";
        }
        else
        {
            throw new ArgumentException(
                $"Unsupported embedding dimension: {embeddingDimension}. " +
                $"Expected 768 or 1536.");
        }
        
        // Serialize query embedding to JSON format (required for VECTOR type)
        var embeddingJson = System.Text.Json.JsonSerializer.Serialize(queryEmbedding);

        // Use raw SQL with VECTOR_DISTANCE function for document-level and chunk-level search
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
        topKParam.Value = topK * 2; // Get more candidates for better results
        command.Parameters.Add(topKParam);
        
        var minSimParam = command.CreateParameter();
        minSimParam.ParameterName = "@minSimilarity";
        minSimParam.Value = minSimilarity;
        command.Parameters.Add(minSimParam);

        await _context.Database.OpenConnectionAsync();

        var results = new List<RelevantDocumentResult>();
        using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var sourceType = reader.GetString(reader.GetOrdinal("SourceType"));
                
                results.Add(new RelevantDocumentResult
                {
                    DocumentId = reader.GetInt32(reader.GetOrdinal("Id")),
                    FileName = reader.GetString(reader.GetOrdinal("FileName")),
                    Category = reader.IsDBNull(reader.GetOrdinal("ActualCategory")) ? null : reader.GetString(reader.GetOrdinal("ActualCategory")),
                    SimilarityScore = reader.GetDouble(reader.GetOrdinal("SimilarityScore")),
                    RelevantChunk = sourceType == "CHUNK" && !reader.IsDBNull(reader.GetOrdinal("ChunkText")) 
                        ? reader.GetString(reader.GetOrdinal("ChunkText")) 
                        : null,
                    ChunkIndex = sourceType == "CHUNK" && !reader.IsDBNull(reader.GetOrdinal("ChunkIndex")) 
                        ? reader.GetInt32(reader.GetOrdinal("ChunkIndex")) 
                        : (int?)null,
                    ExtractedText = sourceType == "DOCUMENT" && !reader.IsDBNull(reader.GetOrdinal("ExtractedText")) 
                        ? reader.GetString(reader.GetOrdinal("ExtractedText")) 
                        : null
                });
            }
        }

        _logger.LogInformation(
            "SQL-optimized search completed. Found {Count} results using VECTOR_DISTANCE (threshold: {Threshold:P0})",
            results.Count, minSimilarity);

        return results.Take(topK).ToList();
    }

    /// <summary>
    /// In-memory vector search (fallback for non-SQL Server or older versions)
    /// </summary>
    private async Task<List<RelevantDocumentResult>> SearchDocumentsInMemoryAsync(
        float[] queryEmbedding,
        string userId,
        int topK = 10,
        double minSimilarity = 0.7)
    {
        // Get all documents with embeddings for the user
        // Note: EmbeddingVector is a computed property, so we check the actual DB fields
        var documents = await _context.Documents
            .Where(d => d.OwnerId == userId && (d.EmbeddingVector768 != null || d.EmbeddingVector1536 != null))
            .ToListAsync();

        _logger.LogInformation("In-memory search: Found {Count} documents with embeddings for user {UserId}", documents.Count, userId);

        if (!documents.Any())
        {
            _logger.LogWarning("No documents with embeddings found for user {UserId}. User needs to upload documents or wait for embeddings to be generated.", userId);
            
            // Check if user has ANY documents (even without embeddings)
            var totalDocs = await _context.Documents
                .Where(d => d.OwnerId == userId)
                .CountAsync();
                
            if (totalDocs > 0)
            {
                _logger.LogWarning("User {UserId} has {TotalDocs} documents but NONE have embeddings generated. Documents need to be processed.", userId, totalDocs);
            }
            
            return new List<RelevantDocumentResult>();
        }

        // Calculate similarity scores for documents
        var scoredDocs = new List<(Document doc, double score)>();
        var allScores = new List<double>(); // Track all scores for diagnostics
        
        foreach (var doc in documents)
        {
            if (doc.EmbeddingVector == null) continue;

            var similarity = CalculateCosineSimilarity(queryEmbedding, doc.EmbeddingVector);
            allScores.Add(similarity);
            
            if (similarity >= minSimilarity)
            {
                scoredDocs.Add((doc, similarity));
                _logger.LogDebug("Document {FileName} (ID: {DocId}) matched with similarity {Score:P1}", 
                    doc.FileName, doc.Id, similarity);
            }
        }

        // Log diagnostic info about scores
        if (allScores.Any())
        {
            _logger.LogInformation(
                "Similarity scores - Min: {Min:P1}, Max: {Max:P1}, Avg: {Avg:P1}, Threshold: {Threshold:P0}", 
                allScores.Min(), allScores.Max(), allScores.Average(), minSimilarity);
            
            // Log top documents by score for debugging
            var topDocs = documents
                .Select(d => new { Doc = d, Score = d.EmbeddingVector != null ? CalculateCosineSimilarity(queryEmbedding, d.EmbeddingVector) : 0 })
                .OrderByDescending(x => x.Score)
                .Take(5)
                .ToList();
            
            _logger.LogInformation("Top 5 documents by similarity score:");
            foreach (var item in topDocs)
            {
                _logger.LogInformation("  - '{FileName}' (ID:{DocId}): Score={Score:P1} {Status}",
                    item.Doc.FileName, item.Doc.Id, item.Score, 
                    item.Score >= minSimilarity ? "✓ ABOVE threshold" : "✗ below threshold");
            }
        }

        _logger.LogInformation("Found {Count} documents above similarity threshold {Threshold:P0}", scoredDocs.Count, minSimilarity);

        // Get chunks for better precision
        // Note: ChunkEmbedding is a computed property, so we check the actual DB fields
        var chunks = await _context.DocumentChunks
            .Include(c => c.Document)
            .Where(c => c.Document!.OwnerId == userId && (c.ChunkEmbedding768 != null || c.ChunkEmbedding1536 != null))
            .ToListAsync();

        _logger.LogInformation("Found {Count} chunks with embeddings for user {UserId}", chunks.Count, userId);

        var scoredChunks = new List<(DocumentChunk chunk, double score)>();
        foreach (var chunk in chunks)
        {
            if (chunk.ChunkEmbedding == null) continue;

            var similarity = CalculateCosineSimilarity(queryEmbedding, chunk.ChunkEmbedding);
            if (similarity >= minSimilarity)
            {
                scoredChunks.Add((chunk, similarity));
            }
        }

        _logger.LogInformation("Found {Count} chunks above similarity threshold {Threshold:P0}", scoredChunks.Count, minSimilarity);

        // Combine document-level and chunk-level results
        var results = new List<RelevantDocumentResult>();

        // Add chunk-based results (higher priority)
        var topChunks = scoredChunks.OrderByDescending(x => x.score).Take(topK).ToList();
        var existingDocIds = new HashSet<int>();

        foreach (var (chunk, score) in topChunks)
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
            existingDocIds.Add(chunk.DocumentId);
        }

        // Add document-level results if we don't have enough chunks
        if (results.Count < topK)
        {
            foreach (var (doc, score) in scoredDocs.OrderByDescending(x => x.score))
            {
                if (results.Count >= topK)
                    break;

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

        _logger.LogInformation("In-memory search: Returning {Count} total results (threshold: {Threshold:P0})", results.Count, minSimilarity);

        if (!results.Any())
        {
            _logger.LogWarning("No documents matched the similarity threshold of {Threshold:P0}. Consider lowering the threshold or checking if document embeddings are compatible with query embeddings.", minSimilarity);
        }

        return results;
    }

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

    private string BuildUserPrompt(string query, string documentContext, List<Message> conversationHistory)
    {
        var builder = new StringBuilder();

        builder.AppendLine(documentContext);
        builder.AppendLine();

        if (conversationHistory.Any())
        {
            builder.AppendLine("=== CONVERSATION HISTORY ===");
            foreach (var msg in conversationHistory)
            {
                builder.AppendLine($"{msg.Role.ToUpper()}: {msg.Content}");
            }
            builder.AppendLine();
        }

        builder.AppendLine("=== CURRENT QUESTION ===");
        builder.AppendLine(query);

        return builder.ToString();
    }

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

    private double CalculateCosineSimilarity(float[] vector1, float[] vector2)
    {
        if (vector1.Length != vector2.Length)
        {
            _logger.LogWarning(
                "Vector dimension mismatch: vector1 has {Length1} dimensions, vector2 has {Length2} dimensions",
                vector1.Length, vector2.Length);
            return 0;
        }

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

    /// <summary>
    /// Fallback keyword search quando la ricerca semantica non trova risultati.
    /// Cerca in: nome file, testo, tag, metadati, categoria, note.
    /// Supporta query diverse tipo: "doc 775", "fattura n775", "documento 775 di Rossi"
    /// </summary>
    private async Task<List<RelevantDocumentResult>> FallbackKeywordSearch(string query, string userId, int topK)
    {
        try
        {
            _logger.LogInformation("Executing enhanced multi-field fallback search for query: {Query}", query);
            
            var queryLower = query.ToLower();
            var results = new List<RelevantDocumentResult>();
            
            // Estrai numeri dalla query (es: "documento 775", "fattura n775" -> "775")
            var numbers = NumberExtractorRegex.Matches(query)
                .Select(m => m.Value)
                .ToList();
            
            // Estrai parole chiave significative (escludendo stop words comuni)
            var stopWords = new HashSet<string> { "il", "lo", "la", "i", "gli", "le", "un", "uno", "una", 
                "di", "da", "in", "con", "su", "per", "tra", "fra", "doc", "documento", "documenti",
                "dammi", "trova", "cerca", "mostra", "voglio", "n", "nr", "num", "numero" };
            
            var keywords = query.ToLower()
                .Split(new[] { ' ', ',', '.', ';', ':', '-', '_' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => !stopWords.Contains(w) && !numbers.Contains(w) && w.Length > 1) // Changed from > 2 to > 1 to allow 2-char keywords
                .ToList();
            
            _logger.LogInformation("Extracted {NumCount} numbers and {KeywordCount} keywords from query", 
                numbers.Count, keywords.Count);
            
            // Carica TUTTI i documenti dell'utente con Tags
            // Faremo il filtro in memoria per garantire case-insensitive funzionante
            var allUserDocuments = await _context.Documents
                .Include(d => d.Tags)
                .Where(d => d.OwnerId == userId)
                .ToListAsync();
            
            _logger.LogInformation("Loaded {Count} total documents for user {UserId}", allUserDocuments.Count, userId);
            
            // Log document details for debugging
            if (allUserDocuments.Any())
            {
                foreach (var doc in allUserDocuments)
                {
                    var extractedTextPreview = doc.ExtractedText != null 
                        ? (doc.ExtractedText.Length > 100 ? doc.ExtractedText.Substring(0, 100) + "..." : doc.ExtractedText)
                        : "[NULL]";
                    
                    _logger.LogDebug(
                        "Document {DocId}: FileName='{FileName}', Category='{Category}', ExtractedTextLength={TextLength}, ExtractedTextPreview='{Preview}'",
                        doc.Id, doc.FileName, doc.ActualCategory ?? "[NULL]", doc.ExtractedText?.Length ?? 0, extractedTextPreview);
                }
            }
            
            if (!allUserDocuments.Any())
            {
                _logger.LogWarning("User {UserId} has no documents", userId);
                return new List<RelevantDocumentResult>();
            }
            
            // Filtra documenti in memoria con case-insensitive
            List<Document> filteredDocuments;
            
            if (numbers.Any() && keywords.Any())
            {
                // Query combinata: numero + keywords (es: "n 5 Rossi")
                _logger.LogInformation("Using combined search: numbers {Numbers} + keywords {Keywords}", 
                    string.Join(", ", numbers), string.Join(", ", keywords));
                
                filteredDocuments = allUserDocuments.Where(d =>
                {
                    // Check se contiene almeno un numero
                    bool hasNumber = numbers.Any(num =>
                        d.FileName.Contains(num, StringComparison.OrdinalIgnoreCase) ||
                        (d.ExtractedText != null && d.ExtractedText.Contains(num, StringComparison.OrdinalIgnoreCase)) ||
                        (d.AITagsJson != null && d.AITagsJson.Contains(num, StringComparison.OrdinalIgnoreCase)) ||
                        (d.ExtractedMetadataJson != null && d.ExtractedMetadataJson.Contains(num, StringComparison.OrdinalIgnoreCase)) ||
                        (d.Notes != null && d.Notes.Contains(num, StringComparison.OrdinalIgnoreCase)));
                    
                    if (!hasNumber) return false;
                    
                    // Check se contiene almeno una keyword
                    bool hasKeyword = keywords.Any(kw =>
                        d.FileName.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                        (d.ExtractedText != null && d.ExtractedText.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                        (d.ActualCategory != null && d.ActualCategory.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                        (d.SuggestedCategory != null && d.SuggestedCategory.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                        (d.Notes != null && d.Notes.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                        (d.AITagsJson != null && d.AITagsJson.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                        (d.ExtractedMetadataJson != null && d.ExtractedMetadataJson.Contains(kw, StringComparison.OrdinalIgnoreCase)));
                    
                    return hasKeyword;
                }).ToList();
            }
            else if (numbers.Any())
            {
                // Solo numeri
                _logger.LogInformation("Using number-only search for numbers: {Numbers}", string.Join(", ", numbers));
                
                filteredDocuments = allUserDocuments.Where(d =>
                    numbers.Any(num =>
                        d.FileName.Contains(num, StringComparison.OrdinalIgnoreCase) ||
                        (d.ExtractedText != null && d.ExtractedText.Contains(num, StringComparison.OrdinalIgnoreCase)) ||
                        (d.AITagsJson != null && d.AITagsJson.Contains(num, StringComparison.OrdinalIgnoreCase)) ||
                        (d.ExtractedMetadataJson != null && d.ExtractedMetadataJson.Contains(num, StringComparison.OrdinalIgnoreCase)) ||
                        (d.Notes != null && d.Notes.Contains(num, StringComparison.OrdinalIgnoreCase)))
                ).ToList();
            }
            else if (keywords.Any())
            {
                // Solo keywords
                _logger.LogInformation("Using keyword-only search for keywords: {Keywords}", string.Join(", ", keywords));
                
                // Log each document check for debugging
                var matchResults = new List<string>();
                
                filteredDocuments = allUserDocuments.Where(d =>
                {
                    var matches = new List<string>();
                    bool hasMatch = false;
                    
                    foreach (var kw in keywords)
                    {
                        if (d.FileName.Contains(kw, StringComparison.OrdinalIgnoreCase))
                        {
                            matches.Add($"FileName matches '{kw}'");
                            hasMatch = true;
                        }
                        if (d.ExtractedText != null && d.ExtractedText.Contains(kw, StringComparison.OrdinalIgnoreCase))
                        {
                            matches.Add($"ExtractedText matches '{kw}'");
                            hasMatch = true;
                        }
                        if (d.ActualCategory != null && d.ActualCategory.Contains(kw, StringComparison.OrdinalIgnoreCase))
                        {
                            matches.Add($"ActualCategory matches '{kw}'");
                            hasMatch = true;
                        }
                        if (d.SuggestedCategory != null && d.SuggestedCategory.Contains(kw, StringComparison.OrdinalIgnoreCase))
                        {
                            matches.Add($"SuggestedCategory matches '{kw}'");
                            hasMatch = true;
                        }
                        if (d.Notes != null && d.Notes.Contains(kw, StringComparison.OrdinalIgnoreCase))
                        {
                            matches.Add($"Notes matches '{kw}'");
                            hasMatch = true;
                        }
                        if (d.AITagsJson != null && d.AITagsJson.Contains(kw, StringComparison.OrdinalIgnoreCase))
                        {
                            matches.Add($"AITagsJson matches '{kw}'");
                            hasMatch = true;
                        }
                        if (d.ExtractedMetadataJson != null && d.ExtractedMetadataJson.Contains(kw, StringComparison.OrdinalIgnoreCase))
                        {
                            matches.Add($"ExtractedMetadataJson matches '{kw}'");
                            hasMatch = true;
                        }
                    }
                    
                    if (hasMatch)
                    {
                        matchResults.Add($"Document '{d.FileName}' (ID:{d.Id}): {string.Join(", ", matches)}");
                    }
                    else
                    {
                        _logger.LogDebug("Document '{FileName}' (ID:{DocId}) - NO MATCH for keywords: {Keywords}", 
                            d.FileName, d.Id, string.Join(", ", keywords));
                    }
                    
                    return hasMatch;
                }).ToList();
                
                // Log all matches found
                if (matchResults.Any())
                {
                    _logger.LogInformation("Keyword search found {Count} matching documents:", matchResults.Count);
                    foreach (var result in matchResults)
                    {
                        _logger.LogInformation("  - {Result}", result);
                    }
                }
                else
                {
                    _logger.LogWarning("Keyword search found NO matching documents for keywords: {Keywords}", 
                        string.Join(", ", keywords));
                    _logger.LogInformation("Checked {DocCount} documents total", allUserDocuments.Count);
                }
            }
            else
            {
                // Fallback generico: cerca query completa
                _logger.LogInformation("Using full-text fallback search for query: {Query}", query);
                
                filteredDocuments = allUserDocuments.Where(d =>
                    d.FileName.Contains(queryLower, StringComparison.OrdinalIgnoreCase) ||
                    (d.ExtractedText != null && d.ExtractedText.Contains(queryLower, StringComparison.OrdinalIgnoreCase)) ||
                    (d.ActualCategory != null && d.ActualCategory.Contains(queryLower, StringComparison.OrdinalIgnoreCase)) ||
                    (d.Notes != null && d.Notes.Contains(queryLower, StringComparison.OrdinalIgnoreCase)) ||
                    (d.AITagsJson != null && d.AITagsJson.Contains(queryLower, StringComparison.OrdinalIgnoreCase)) ||
                    (d.ExtractedMetadataJson != null && d.ExtractedMetadataJson.Contains(queryLower, StringComparison.OrdinalIgnoreCase))
                ).ToList();
            }
            
            var documents = filteredDocuments.Take(topK * 2).ToList();
            
            _logger.LogInformation("Fallback search found {Count} candidate documents", documents.Count);
            
            // Rankare i risultati in base a quanti campi matchano
            var scoredDocs = new List<(Document doc, double score, string matchedFields)>();
            
            foreach (var doc in documents)
            {
                double score = 0;
                var matchedFields = new List<string>();
                
                // Calcola score basato su dove è stato trovato il match
                foreach (var num in numbers)
                {
                    if (doc.FileName.Contains(num, StringComparison.OrdinalIgnoreCase))
                    {
                        score += 0.15;
                        matchedFields.Add("FileName");
                    }
                    if (doc.ExtractedText != null && doc.ExtractedText.Contains(num, StringComparison.OrdinalIgnoreCase))
                    {
                        score += 0.10;
                        matchedFields.Add("Text");
                    }
                    if (doc.ExtractedMetadataJson != null && doc.ExtractedMetadataJson.Contains(num, StringComparison.OrdinalIgnoreCase))
                    {
                        score += 0.12;
                        matchedFields.Add("Metadata");
                    }
                }
                
                foreach (var kw in keywords)
                {
                    if (doc.FileName.Contains(kw, StringComparison.OrdinalIgnoreCase))
                    {
                        score += 0.10;
                        if (!matchedFields.Contains("FileName")) matchedFields.Add("FileName");
                    }
                    if (doc.ActualCategory != null && doc.ActualCategory.Contains(kw, StringComparison.OrdinalIgnoreCase))
                    {
                        score += 0.08;
                        matchedFields.Add("Category");
                    }
                    if (doc.AITagsJson != null && doc.AITagsJson.Contains(kw, StringComparison.OrdinalIgnoreCase))
                    {
                        score += 0.07;
                        matchedFields.Add("Tags");
                    }
                    if (doc.ExtractedText != null && doc.ExtractedText.Contains(kw, StringComparison.OrdinalIgnoreCase))
                    {
                        score += 0.05;
                        if (!matchedFields.Contains("Text")) matchedFields.Add("Text");
                    }
                }
                
                // Bonus se ha Tags che matchano
                if (doc.Tags.Any(t => keywords.Any(kw => t.Name.Contains(kw, StringComparison.OrdinalIgnoreCase))))
                {
                    score += 0.10;
                    matchedFields.Add("DocumentTags");
                }
                
                if (score > 0)
                {
                    scoredDocs.Add((doc, score, string.Join(", ", matchedFields.Distinct())));
                }
            }
            
            // Ordina per score e prendi top K
            var topDocs = scoredDocs.OrderByDescending(x => x.score).Take(topK).ToList();
            
            _logger.LogInformation("Ranked and selected top {Count} documents", topDocs.Count);
            
            foreach (var (doc, score, matchedFields) in topDocs)
            {
                _logger.LogInformation("Document {FileName} (ID: {DocId}) - Score: {Score:F2}, Matched: {Fields}",
                    doc.FileName, doc.Id, score, matchedFields);
                
                // Cerca chunk più rilevante
                DocumentChunk? relevantChunk = null;
                
                if (numbers.Any())
                {
                    relevantChunk = await _context.DocumentChunks
                        .Where(c => c.DocumentId == doc.Id)
                        .Where(c => numbers.Any(num => c.ChunkText.Contains(num, StringComparison.OrdinalIgnoreCase)))
                        .OrderBy(c => c.ChunkIndex)
                        .FirstOrDefaultAsync();
                }
                
                if (relevantChunk == null && keywords.Any())
                {
                    relevantChunk = await _context.DocumentChunks
                        .Where(c => c.DocumentId == doc.Id)
                        .Where(c => keywords.Any(kw => c.ChunkText.Contains(kw, StringComparison.OrdinalIgnoreCase)))
                        .OrderBy(c => c.ChunkIndex)
                        .FirstOrDefaultAsync();
                }
                
                results.Add(new RelevantDocumentResult
                {
                    DocumentId = doc.Id,
                    FileName = doc.FileName,
                    Category = doc.ActualCategory,
                    SimilarityScore = Math.Min(FallbackSearchSimilarityScore + score, 0.95), // Max 0.95
                    RelevantChunk = relevantChunk?.ChunkText,
                    ChunkIndex = relevantChunk?.ChunkIndex,
                    ExtractedText = doc.ExtractedText
                });
            }
            
            if (results.Any())
            {
                _logger.LogInformation("Fallback search successful: returning {Count} documents with avg score {AvgScore:F2}", 
                    results.Count, results.Average(r => r.SimilarityScore));
            }
            else
            {
                _logger.LogWarning("Fallback search found no matching documents for query: {Query}. Extracted {NumCount} numbers, {KeywordCount} keywords", 
                    query, numbers.Count, keywords.Count);
            }
            
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in fallback keyword search for query: {Query}", query);
            return new List<RelevantDocumentResult>();
        }
    }

    private string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text ?? string.Empty;

        return text.Substring(0, maxLength - 3) + "...";
    }
}
