using DocN.Core.Interfaces;
using DocN.Core.AI.Configuration;
using DocN.Data.Models;
using DocN.Data.Services.Agents;
using DocN.Data.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using System.Text;
using System.Linq;

#pragma warning disable SKEXP0110 // Agents are experimental

namespace DocN.Data.Services;

/// <summary>
/// Enhanced Agent RAG Service with Microsoft Agent Framework integration
/// Implements multi-agent orchestration with HyDE, Cross-Encoder ReRanking, 
/// Contextual Compression, Progressive Streaming, and Intelligent Caching
/// </summary>
public class EnhancedAgentRAGService : ISemanticRAGService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<EnhancedAgentRAGService> _logger;
    private readonly IKernelProvider _kernelProvider;
    private readonly IEmbeddingService _embeddingService;
    private readonly ICacheService _cacheService;
    private readonly EnhancedRAGConfiguration _config;

    // Agent-specific cache keys
    private const string CACHE_PREFIX_QUERY_ANALYSIS = "agent:query_analysis:";
    private const string CACHE_PREFIX_RETRIEVAL = "agent:retrieval:";
    private const string CACHE_PREFIX_RERANKING = "agent:reranking:";

    public EnhancedAgentRAGService(
        ApplicationDbContext context,
        ILogger<EnhancedAgentRAGService> logger,
        IKernelProvider kernelProvider,
        IEmbeddingService embeddingService,
        ICacheService cacheService,
        IOptions<EnhancedRAGConfiguration> config)
    {
        _context = context;
        _logger = logger;
        _kernelProvider = kernelProvider;
        _embeddingService = embeddingService;
        _cacheService = cacheService;
        _config = config.Value;
    }

    /// <summary>
    /// Creates an instance of HyDEService on-demand
    /// </summary>
    private async Task<IHyDEService> CreateHyDEServiceAsync()
    {
        var kernel = await _kernelProvider.GetKernelAsync();
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<HyDEService>.Instance;
        
        // Create a dummy RAG service for HyDE (it won't be used in our implementation)
        // HyDE will use embeddings directly through this service
        var ragService = new NoOpSemanticRAGService();
        
        return new HyDEService(kernel, logger, _embeddingService, ragService);
    }

    /// <summary>
    /// Creates an instance of ReRankingService on-demand
    /// </summary>
    private async Task<IReRankingService> CreateReRankingServiceAsync()
    {
        var kernel = await _kernelProvider.GetKernelAsync();
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<ReRankingService>.Instance;
        
        return new ReRankingService(kernel, logger);
    }

    /// <summary>
    /// Creates an instance of ContextualCompressionService on-demand
    /// </summary>
    private IContextualCompressionService CreateCompressionService()
    {
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<ContextualCompressionService>.Instance;
        
        return new ContextualCompressionService(logger, _embeddingService, Options.Create(_config.Synthesis.EnableContextualCompression 
            ? new ContextualCompressionConfiguration { Enabled = true }
            : new ContextualCompressionConfiguration { Enabled = false }));
    }

    // Simplified NoOp RAG service for HyDE dependency
    private class NoOpSemanticRAGService : ISemanticRAGService
    {
        public Task<SemanticRAGResponse> GenerateResponseAsync(string query, string userId, int? conversationId = null, List<int>? specificDocumentIds = null, int topK = 5)
            => Task.FromResult(new SemanticRAGResponse());

        public async IAsyncEnumerable<string> GenerateStreamingResponseAsync(string query, string userId, int? conversationId = null, List<int>? specificDocumentIds = null)
        {
            yield break;
        }

        public Task<List<RelevantDocumentResult>> SearchDocumentsAsync(string query, string userId, int topK = 10, double minSimilarity = 0.7)
            => Task.FromResult(new List<RelevantDocumentResult>());

        public Task<List<RelevantDocumentResult>> SearchDocumentsWithEmbeddingAsync(float[] queryEmbedding, string userId, int topK = 10, double minSimilarity = 0.7)
            => Task.FromResult(new List<RelevantDocumentResult>());
    }

    /// <inheritdoc/>
    public async Task<SemanticRAGResponse> GenerateResponseAsync(
        string query,
        string userId,
        int? conversationId = null,
        List<int>? specificDocumentIds = null,
        int topK = 5)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var metadata = new Dictionary<string, object>();

        try
        {
            _logger.LogInformation(
                "EnhancedAgentRAG: Generating response for query: {Query}, User: {UserId}",
                query, userId);

            // Phase 1: Query Analysis with Caching
            var (analyzedQuery, hydeDoc) = await QueryAnalysisPhaseAsync(query, metadata);

            // Phase 2: Retrieval with HyDE Integration
            var relevantDocs = await RetrievalPhaseAsync(analyzedQuery, hydeDoc, userId, topK, metadata);

            // Phase 3: ReRanking with Cross-Encoder
            var rerankedDocs = await ReRankingPhaseAsync(query, relevantDocs, topK, metadata);

            // Phase 4: Contextual Compression
            var compressedContext = await CompressionPhaseAsync(query, rerankedDocs, metadata);

            // Phase 5: Response Synthesis
            var answer = await SynthesisPhaseAsync(
                query, compressedContext, conversationId, metadata);

            stopwatch.Stop();

            return new SemanticRAGResponse
            {
                Answer = answer,
                SourceDocuments = rerankedDocs,
                ConversationId = conversationId ?? 0,
                ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                FromCache = false,
                Metadata = metadata
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in EnhancedAgentRAG.GenerateResponseAsync");
            stopwatch.Stop();

            return new SemanticRAGResponse
            {
                Answer = "Si è verificato un errore durante l'elaborazione della richiesta. Riprova più tardi.",
                SourceDocuments = new List<RelevantDocumentResult>(),
                ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                Metadata = metadata
            };
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<string> GenerateStreamingResponseAsync(
        string query,
        string userId,
        int? conversationId = null,
        List<int>? specificDocumentIds = null)
    {
        var metadata = new Dictionary<string, object>();

        // Progressive Streaming: Stream feedback in real-time
        yield return "🔍 Analyzing query...\n";

        // Phase 1: Query Analysis
        var (analyzedQuery, hydeDoc) = await QueryAnalysisPhaseAsync(query, metadata);
        yield return "✓ Query analyzed\n";
        yield return "📚 Retrieving documents...\n";

        // Phase 2: Retrieval
        var topK = _config.Retrieval.DefaultTopK;
        var relevantDocs = await RetrievalPhaseAsync(analyzedQuery, hydeDoc, userId, topK, metadata);
        yield return $"✓ Found {relevantDocs.Count} relevant documents\n";
        yield return "⚖️ Re-ranking results...\n";

        // Phase 3: ReRanking
        var rerankedDocs = await ReRankingPhaseAsync(query, relevantDocs, topK, metadata);
        yield return "✓ Re-ranking complete\n";
        yield return "🗜️ Compressing context...\n";

        // Phase 4: Compression
        var compressedContext = await CompressionPhaseAsync(query, rerankedDocs, metadata);
        yield return "✓ Context compressed\n";
        yield return "✍️ Generating response...\n\n";

        // Phase 5: Synthesis with Streaming
        var kernel = await _kernelProvider.GetKernelAsync();
        var chatService = kernel.GetRequiredService<IChatCompletionService>();

        var systemPrompt = CreateSystemPrompt();
        var userPrompt = BuildUserPrompt(query, compressedContext);

        var chatHistory = new Microsoft.SemanticKernel.ChatCompletion.ChatHistory();
        chatHistory.AddSystemMessage(systemPrompt);
        chatHistory.AddUserMessage(userPrompt);

        await foreach (var chunk in chatService.GetStreamingChatMessageContentsAsync(chatHistory))
        {
            if (!string.IsNullOrEmpty(chunk.Content))
            {
                yield return chunk.Content;
            }
        }
    }

    /// <inheritdoc/>
    public async Task<List<RelevantDocumentResult>> SearchDocumentsAsync(
        string query,
        string userId,
        int topK = 10,
        double minSimilarity = 0.7)
    {
        try
        {
            // Use HyDE if enabled
            if (_config.QueryAnalysis.EnableHyDE)
            {
                var hydeService = await CreateHyDEServiceAsync();
                var hydeRecommendation = await hydeService.AnalyzeQueryForHyDEAsync(query);
                
                if (hydeRecommendation.IsRecommended)
                {
                    _logger.LogInformation("Using HyDE for search (confidence: {Confidence:F2})", 
                        hydeRecommendation.Confidence);
                    return await hydeService.SearchWithHyDEAsync(query, userId, topK, minSimilarity);
                }
            }

            // Standard semantic search
            var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query);
            if (queryEmbedding == null)
            {
                _logger.LogWarning("Failed to generate embedding for query: {Query}", query);
                return new List<RelevantDocumentResult>();
            }

            return await SearchDocumentsWithEmbeddingAsync(queryEmbedding, userId, topK, minSimilarity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SearchDocumentsAsync");
            return new List<RelevantDocumentResult>();
        }
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
            var results = new List<RelevantDocumentResult>();

            // Get all chunks with embeddings
            var chunks = await _context.DocumentChunks
                .Include(c => c.Document)
                .Where(c => c.ChunkEmbedding != null && c.Document != null && c.Document.OwnerId == userId)
                .ToListAsync();

            foreach (var chunk in chunks)
            {
                if (chunk.ChunkEmbedding == null) continue;

                var similarity = VectorMathHelper.CosineSimilarity(queryEmbedding, chunk.ChunkEmbedding);
                
                if (similarity >= minSimilarity)
                {
                    results.Add(new RelevantDocumentResult
                    {
                        DocumentId = chunk.DocumentId,
                        FileName = chunk.Document?.FileName ?? "Unknown",
                        Category = chunk.Document?.ActualCategory ?? chunk.Document?.SuggestedCategory,
                        SimilarityScore = similarity,
                        RelevantChunk = chunk.ChunkText,
                        ChunkIndex = chunk.ChunkIndex,
                        ExtractedText = chunk.ChunkText
                    });
                }
            }

            return results.OrderByDescending(r => r.SimilarityScore).Take(topK).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SearchDocumentsWithEmbeddingAsync");
            return new List<RelevantDocumentResult>();
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // PRIVATE PHASE METHODS - Agent-based Pipeline with Intelligent Caching
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Phase 1: Query Analysis with HyDE and intelligent caching
    /// </summary>
    private async Task<(string analyzedQuery, string? hydeDocument)> QueryAnalysisPhaseAsync(
        string query,
        Dictionary<string, object> metadata)
    {
        var phaseStopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // Check cache first if enabled
            if (_config.Caching.EnableQueryAnalysisCache)
            {
                var cacheKey = CACHE_PREFIX_QUERY_ANALYSIS + query;
                var cached = await _cacheService.GetCachedSearchResultsAsync<EnhancedQueryAnalysisResult>(cacheKey);
                if (cached != null && cached.Any())
                {
                    _logger.LogDebug("Query analysis cache hit for: {Query}", query);
                    metadata["query_analysis_cached"] = true;
                    var cachedResult = cached.First();
                    return (cachedResult.AnalyzedQuery, cachedResult.HydeDocument);
                }
            }

            string? hydeDocument = null;

            // Apply HyDE if enabled
            if (_config.QueryAnalysis.EnableHyDE)
            {
                var hydeService = await CreateHyDEServiceAsync();
                var hydeRecommendation = await hydeService.AnalyzeQueryForHyDEAsync(query);
                
                if (hydeRecommendation.IsRecommended)
                {
                    hydeDocument = await hydeService.GenerateHypotheticalDocumentAsync(query);
                    _logger.LogInformation("HyDE document generated (confidence: {Confidence:F2})", 
                        hydeRecommendation.Confidence);
                    metadata["hyde_used"] = true;
                    metadata["hyde_confidence"] = hydeRecommendation.Confidence;
                }
            }

            var result = new EnhancedQueryAnalysisResult
            {
                AnalyzedQuery = query,
                HydeDocument = hydeDocument
            };

            // Cache the result
            if (_config.Caching.EnableQueryAnalysisCache)
            {
                var cacheKey = CACHE_PREFIX_QUERY_ANALYSIS + query;
                await _cacheService.SetCachedSearchResultsAsync(
                    cacheKey,
                    new List<EnhancedQueryAnalysisResult> { result },
                    TimeSpan.FromHours(_config.Caching.CacheExpirationHours));
            }

            phaseStopwatch.Stop();
            metadata["query_analysis_time_ms"] = phaseStopwatch.ElapsedMilliseconds;

            return (result.AnalyzedQuery, result.HydeDocument);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in QueryAnalysisPhase");
            return (query, null);
        }
    }

    /// <summary>
    /// Phase 2: Retrieval with HyDE integration and caching
    /// </summary>
    private async Task<List<RelevantDocumentResult>> RetrievalPhaseAsync(
        string query,
        string? hydeDocument,
        string userId,
        int topK,
        Dictionary<string, object> metadata)
    {
        var phaseStopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // Check cache
            if (_config.Caching.EnableRetrievalCache)
            {
                var cacheKey = CACHE_PREFIX_RETRIEVAL + query + userId;
                var cached = await _cacheService.GetCachedSearchResultsAsync<RelevantDocumentResult>(cacheKey);
                if (cached != null && cached.Any())
                {
                    _logger.LogDebug("Retrieval cache hit for: {Query}", query);
                    metadata["retrieval_cached"] = true;
                    return cached.Take(topK).ToList();
                }
            }

            List<RelevantDocumentResult> results;

            // Retrieve more candidates for re-ranking
            var candidateMultiplier = _config.Retrieval.CandidateMultiplier;
            var retrievalTopK = topK * candidateMultiplier;

            if (hydeDocument != null)
            {
                // Use HyDE document for retrieval
                var hydeEmbedding = await _embeddingService.GenerateEmbeddingAsync(hydeDocument);
                if (hydeEmbedding != null)
                {
                    results = await SearchDocumentsWithEmbeddingAsync(
                        hydeEmbedding, userId, retrievalTopK, _config.Retrieval.MinSimilarity);
                    metadata["retrieval_method"] = "hyde";
                }
                else
                {
                    // Fallback to standard
                    results = await SearchDocumentsAsync(query, userId, retrievalTopK, _config.Retrieval.MinSimilarity);
                    metadata["retrieval_method"] = "standard_fallback";
                }
            }
            else
            {
                results = await SearchDocumentsAsync(query, userId, retrievalTopK, _config.Retrieval.MinSimilarity);
                metadata["retrieval_method"] = "standard";
            }

            // Cache results
            if (_config.Caching.EnableRetrievalCache)
            {
                var cacheKey = CACHE_PREFIX_RETRIEVAL + query + userId;
                await _cacheService.SetCachedSearchResultsAsync(
                    cacheKey, results, TimeSpan.FromHours(_config.Caching.CacheExpirationHours));
            }

            phaseStopwatch.Stop();
            metadata["retrieval_time_ms"] = phaseStopwatch.ElapsedMilliseconds;
            metadata["retrieval_count"] = results.Count;

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in RetrievalPhase");
            return new List<RelevantDocumentResult>();
        }
    }

    /// <summary>
    /// Phase 3: Cross-Encoder ReRanking
    /// </summary>
    private async Task<List<RelevantDocumentResult>> ReRankingPhaseAsync(
        string query,
        List<RelevantDocumentResult> results,
        int topK,
        Dictionary<string, object> metadata)
    {
        var phaseStopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            if (!_config.Reranking.Enabled || results.Count == 0)
            {
                metadata["reranking_enabled"] = false;
                return results.Take(topK).ToList();
            }

            _logger.LogDebug("Re-ranking {Count} results", results.Count);

            var reRankingService = await CreateReRankingServiceAsync();
            var reranked = await reRankingService.ReRankResultsAsync(query, results, topK);

            phaseStopwatch.Stop();
            metadata["reranking_enabled"] = true;
            metadata["reranking_time_ms"] = phaseStopwatch.ElapsedMilliseconds;
            metadata["reranked_count"] = reranked.Count;

            return reranked;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ReRankingPhase, returning original results");
            return results.Take(topK).ToList();
        }
    }

    /// <summary>
    /// Phase 4: Contextual Compression for token reduction
    /// </summary>
    private async Task<string> CompressionPhaseAsync(
        string query,
        List<RelevantDocumentResult> documents,
        Dictionary<string, object> metadata)
    {
        var phaseStopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            if (!_config.Synthesis.EnableContextualCompression || documents.Count == 0)
            {
                metadata["compression_enabled"] = false;
                return BuildDocumentContext(documents);
            }

            var chunks = documents
                .Select(d => d.RelevantChunk ?? d.ExtractedText ?? "")
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .ToList();

            var compressionService = CreateCompressionService();
            var targetTokens = _config.Synthesis.MaxContextLength;
            var compressedChunks = await compressionService.CompressChunksAsync(query, chunks, targetTokens);

            var compressedContext = string.Join("\n\n", compressedChunks.Select(c => c.Content));

            phaseStopwatch.Stop();
            metadata["compression_enabled"] = true;
            metadata["compression_time_ms"] = phaseStopwatch.ElapsedMilliseconds;
            metadata["original_tokens"] = chunks.Sum(c => compressionService.EstimateTokenCount(c));
            metadata["compressed_tokens"] = compressionService.EstimateTokenCount(compressedContext);

            return compressedContext;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in CompressionPhase, using uncompressed context");
            metadata["compression_enabled"] = false;
            return BuildDocumentContext(documents);
        }
    }

    /// <summary>
    /// Phase 5: Response Synthesis with Agent Framework
    /// </summary>
    private async Task<string> SynthesisPhaseAsync(
        string query,
        string documentContext,
        int? conversationId,
        Dictionary<string, object> metadata)
    {
        var phaseStopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            if (string.IsNullOrWhiteSpace(documentContext))
            {
                return "Non ho trovato documenti rilevanti per rispondere alla tua domanda.";
            }

            var kernel = await _kernelProvider.GetKernelAsync();
            var chatService = kernel.GetRequiredService<IChatCompletionService>();

            var systemPrompt = CreateSystemPrompt();
            var userPrompt = BuildUserPrompt(query, documentContext);

            var chatHistory = new Microsoft.SemanticKernel.ChatCompletion.ChatHistory();
            chatHistory.AddSystemMessage(systemPrompt);
            chatHistory.AddUserMessage(userPrompt);

            var response = await chatService.GetChatMessageContentAsync(chatHistory);
            var answer = response.Content ?? "Unable to generate response";

            phaseStopwatch.Stop();
            metadata["synthesis_time_ms"] = phaseStopwatch.ElapsedMilliseconds;

            return answer;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SynthesisPhase");
            return "Si è verificato un errore durante la generazione della risposta.";
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // HELPER METHODS
    // ═══════════════════════════════════════════════════════════════════

    private string BuildDocumentContext(List<RelevantDocumentResult> documents)
    {
        if (!documents.Any())
            return "No relevant documents found.";

        var builder = new StringBuilder();
        for (int i = 0; i < documents.Count; i++)
        {
            var doc = documents[i];
            var text = doc.RelevantChunk ?? doc.ExtractedText ?? "";
            
            builder.AppendLine($"[Document {i + 1}: {doc.FileName}]");
            builder.AppendLine(text);
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private string CreateSystemPrompt()
    {
        return @"Sei un assistente AI esperto che risponde a domande basandosi esclusivamente sui documenti forniti.

ISTRUZIONI:
1. Rispondi SOLO usando le informazioni presenti nei documenti forniti
2. Se la risposta non è presente nei documenti, dillo chiaramente
3. Cita i documenti quando fornisci informazioni (es: ""Secondo il Documento 1..."")
4. Sii preciso, conciso e professionale
5. Se ci sono informazioni contrastanti nei documenti, segnalalo
6. Non inventare o dedurre informazioni non presenti nei documenti";
    }

    private string BuildUserPrompt(string query, string documentContext)
    {
        return $@"DOCUMENTI:
{documentContext}

DOMANDA: {query}

Rispondi alla domanda basandoti esclusivamente sui documenti forniti.";
    }

}

/// <summary>
/// Class for caching query analysis results
/// </summary>
public class EnhancedQueryAnalysisResult
{
    public string AnalyzedQuery { get; set; } = string.Empty;
    public string? HydeDocument { get; set; }
}
