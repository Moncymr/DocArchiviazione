using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.Extensions.Logging;
using DocN.Core.Interfaces;
using System.Text;
using System.Text.Json;

#pragma warning disable SKEXP0010 // ResponseFormat is experimental

namespace DocN.Data.Services;

/// <summary>
/// Implementazione del servizio HyDE (Hypothetical Document Embeddings)
/// Genera documenti ipotetici per migliorare il retrieval
/// </summary>
public class HyDEService : IHyDEService
{
    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chatService;
    private readonly ILogger<HyDEService> _logger;
    private readonly IEmbeddingService _embeddingService;
    private readonly ISemanticRAGService _ragService;
    private readonly HyDEConfiguration _config;

    /// <summary>
    /// Inizializza una nuova istanza del servizio HyDE
    /// </summary>
    public HyDEService(
        Kernel kernel,
        ILogger<HyDEService> logger,
        IEmbeddingService embeddingService,
        ISemanticRAGService ragService,
        HyDEConfiguration? config = null)
    {
        _kernel = kernel;
        _chatService = kernel.GetRequiredService<IChatCompletionService>();
        _logger = logger;
        _embeddingService = embeddingService;
        _ragService = ragService;
        _config = config ?? new HyDEConfiguration();
    }

    /// <inheritdoc/>
    public async Task<string> GenerateHypotheticalDocumentAsync(string query, string? domainContext = null)
    {
        try
        {
            _logger.LogDebug("Generating hypothetical document for query: {Query}", query);

            var prompt = BuildHyDEPrompt(query, domainContext);

            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(@"Sei un esperto scrittore di documenti tecnici. 
Genera un documento ipotetico che potrebbe rispondere alla domanda fornita.
Il documento deve essere scritto in stile formale e professionale, simile ai documenti aziendali reali.");
            chatHistory.AddUserMessage(prompt);

            var settings = new OpenAIPromptExecutionSettings
            {
                MaxTokens = _config.TargetDocumentLength * 2, // Token ≈ parole * 1.3
                Temperature = _config.Temperature,
                TopP = 0.9
            };

            var result = await _chatService.GetChatMessageContentAsync(chatHistory, settings, _kernel);
            var hypotheticalDoc = result.Content?.Trim() ?? string.Empty;

            _logger.LogInformation("Generated hypothetical document ({Length} chars) for query: {Query}",
                hypotheticalDoc.Length, query);

            return hypotheticalDoc;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating hypothetical document for query: {Query}", query);
            // Fallback: usa la query stessa
            return query;
        }
    }

    /// <inheritdoc/>
    public async Task<List<string>> GenerateMultipleHypotheticalDocumentsAsync(
        string query,
        int numVariants = 2,
        string? domainContext = null)
    {
        try
        {
            _logger.LogDebug("Generating {NumVariants} hypothetical document variants for query: {Query}",
                numVariants, query);

            var contextPart = domainContext != null ? $"Contesto: {domainContext}\n\n" : "";
            var prompt = $@"Genera {numVariants} documenti ipotetici DIVERSI che potrebbero rispondere alla seguente domanda.
Ogni documento deve avere una prospettiva o focus leggermente diverso.

Domanda: {query}

{contextPart}Restituisci i documenti in formato JSON:
{{
    ""documents"": [
        ""documento 1..."",
        ""documento 2...""
    ]
}}";

            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage("Sei un esperto scrittore. Genera documenti ipotetici con prospettive diverse ma tutti rilevanti alla domanda.");
            chatHistory.AddUserMessage(prompt);

            var settings = new OpenAIPromptExecutionSettings
            {
                MaxTokens = _config.TargetDocumentLength * numVariants * 2,
                Temperature = _config.Temperature + 0.1, // Più varietà per multiple varianti
                TopP = 0.95,
                ResponseFormat = "json_object"
            };

            var result = await _chatService.GetChatMessageContentAsync(chatHistory, settings, _kernel);
            var jsonResponse = result.Content?.Trim() ?? "{}";

            // Parse JSON
            var documents = new List<string>();
            try
            {
                var jsonDoc = JsonDocument.Parse(jsonResponse);
                if (jsonDoc.RootElement.TryGetProperty("documents", out var docsArray))
                {
                    foreach (var doc in docsArray.EnumerateArray())
                    {
                        var docText = doc.GetString();
                        if (!string.IsNullOrWhiteSpace(docText))
                        {
                            documents.Add(docText);
                        }
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse hypothetical documents JSON");
            }

            // Se non abbiamo abbastanza documenti, genera singolarmente
            while (documents.Count < numVariants)
            {
                var singleDoc = await GenerateHypotheticalDocumentAsync(query, domainContext);
                documents.Add(singleDoc);
            }

            _logger.LogInformation("Generated {Count} hypothetical document variants", documents.Count);
            return documents.Take(numVariants).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating multiple hypothetical documents");
            // Fallback: genera singolarmente
            var documents = new List<string>();
            for (int i = 0; i < numVariants; i++)
            {
                var doc = await GenerateHypotheticalDocumentAsync(query, domainContext);
                documents.Add(doc);
            }
            return documents;
        }
    }

    /// <inheritdoc/>
    public async Task<List<RelevantDocumentResult>> SearchWithHyDEAsync(
        string query,
        string userId,
        int topK = 10,
        double minSimilarity = 0.7)
    {
        try
        {
            _logger.LogDebug("Searching with HyDE for query: {Query}", query);

            if (!_config.Enabled)
            {
                _logger.LogDebug("HyDE is disabled, falling back to standard search");
                return await _ragService.SearchDocumentsAsync(query, userId, topK, minSimilarity);
            }

            // Controlla se HyDE è appropriato per questa query
            if (_config.AutoDecideHyDE)
            {
                var recommendation = await AnalyzeQueryForHyDEAsync(query);
                if (!recommendation.IsRecommended)
                {
                    _logger.LogDebug("HyDE not recommended for this query: {Reason}", recommendation.Reason);
                    return await _ragService.SearchDocumentsAsync(query, userId, topK, minSimilarity);
                }
            }

            // Genera documento(i) ipotetico
            List<string> hypotheticalDocs;
            if (_config.NumHypotheticalDocs > 1)
            {
                hypotheticalDocs = await GenerateMultipleHypotheticalDocumentsAsync(
                    query, _config.NumHypotheticalDocs);
            }
            else
            {
                var singleDoc = await GenerateHypotheticalDocumentAsync(query);
                hypotheticalDocs = new List<string> { singleDoc };
            }

            // Genera embeddings per i documenti ipotetici
            var allResults = new List<RelevantDocumentResult>();

            foreach (var hypotheticalDoc in hypotheticalDocs)
            {
                var embedding = await _embeddingService.GenerateEmbeddingAsync(hypotheticalDoc);
                if (embedding == null)
                {
                    _logger.LogWarning("Failed to generate embedding for hypothetical document");
                    continue;
                }

                // Cerca usando l'embedding del documento ipotetico
                var results = await _ragService.SearchDocumentsWithEmbeddingAsync(
                    embedding, userId, topK, minSimilarity);

                allResults.AddRange(results);
            }

            // Aggrega risultati (rimuovi duplicati e ordina per score)
            var aggregatedResults = allResults
                .GroupBy(r => r.DocumentId)
                .Select(g =>
                {
                    var firstResult = g.First();
                    return new RelevantDocumentResult
                    {
                        DocumentId = g.Key,
                        FileName = firstResult.FileName,
                        Category = firstResult.Category,
                        SimilarityScore = g.Max(r => r.SimilarityScore), // Prendi lo score massimo
                        RelevantChunk = firstResult.RelevantChunk,
                        ChunkIndex = firstResult.ChunkIndex,
                        ExtractedText = firstResult.ExtractedText
                    };
                })
                .OrderByDescending(r => r.SimilarityScore)
                .Take(topK)
                .ToList();

            _logger.LogInformation("HyDE search completed. Found {Count} unique results", aggregatedResults.Count);
            return aggregatedResults;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in HyDE search, falling back to standard search");
            return await _ragService.SearchDocumentsAsync(query, userId, topK, minSimilarity);
        }
    }

    /// <inheritdoc/>
    public async Task<List<RelevantDocumentResult>> SearchHybridWithHyDEAsync(
        string query,
        string userId,
        int topK = 10,
        double hydeWeight = 0.6)
    {
        try
        {
            _logger.LogDebug("Hybrid search (standard + HyDE) for query: {Query}", query);

            // Esegui ricerca standard
            var standardResults = await _ragService.SearchDocumentsAsync(
                query, userId, topK * 2, 0.5); // Prendiamo più risultati con soglia più bassa

            // Esegui ricerca HyDE
            var hydeResults = await SearchWithHyDEAsync(query, userId, topK * 2, 0.5);

            // Combina risultati con pesi
            var combinedScores = new Dictionary<int, (RelevantDocumentResult result, double score)>();

            // Aggiungi risultati standard
            foreach (var result in standardResults)
            {
                var weightedScore = result.SimilarityScore * (1.0 - hydeWeight);
                combinedScores[result.DocumentId] = (result, weightedScore);
            }

            // Aggiungi/combina risultati HyDE
            foreach (var result in hydeResults)
            {
                var weightedScore = result.SimilarityScore * hydeWeight;
                
                if (combinedScores.ContainsKey(result.DocumentId))
                {
                    var existing = combinedScores[result.DocumentId];
                    combinedScores[result.DocumentId] = (existing.result, existing.score + weightedScore);
                }
                else
                {
                    combinedScores[result.DocumentId] = (result, weightedScore);
                }
            }

            // Ordina e restituisci top K
            var finalResults = combinedScores.Values
                .OrderByDescending(x => x.score)
                .Take(topK)
                .Select(x =>
                {
                    x.result.SimilarityScore = x.score;
                    return x.result;
                })
                .ToList();

            _logger.LogInformation("Hybrid search completed. Combined {StandardCount} standard + {HydeCount} HyDE results into {FinalCount} results",
                standardResults.Count, hydeResults.Count, finalResults.Count);

            return finalResults;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in hybrid search, falling back to standard search");
            return await _ragService.SearchDocumentsAsync(query, userId, topK, 0.7);
        }
    }

    /// <inheritdoc/>
    public async Task<HyDERecommendation> AnalyzeQueryForHyDEAsync(string query)
    {
        try
        {
            _logger.LogDebug("Analyzing query for HyDE suitability: {Query}", query);

            var prompt = $@"Analizza la seguente query e determina se HyDE (Hypothetical Document Embeddings) sarebbe utile.

Query: {query}

HyDE è utile per:
- Query concettuali o astratte
- Query che richiedono ragionamento
- Query complesse
- Domini specializzati

HyDE è meno utile per:
- Ricerche keyword semplici
- Ricerche esatte (nomi, codici, date)
- Query molto brevi

Restituisci l'analisi in formato JSON:
{{
    ""isRecommended"": true/false,
    ""confidence"": 0.0-1.0,
    ""reason"": ""spiegazione"",
    ""queryType"": ""Simple"" | ""Conceptual"" | ""Reasoning"" | ""Exact"" | ""Complex"",
    ""suggestedHyDEWeight"": 0.0-1.0
}}";

            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage("Sei un esperto di sistemi RAG. Analizza le query e raccomanda l'uso di HyDE quando appropriato.");
            chatHistory.AddUserMessage(prompt);

            var settings = new OpenAIPromptExecutionSettings
            {
                MaxTokens = 200,
                Temperature = 0.2,
                ResponseFormat = "json_object"
            };

            var result = await _chatService.GetChatMessageContentAsync(chatHistory, settings, _kernel);
            var jsonResponse = result.Content?.Trim() ?? "{}";

            // Parse JSON
            var recommendation = new HyDERecommendation
            {
                IsRecommended = false,
                Confidence = 0.5,
                Reason = "Analisi non disponibile",
                QueryType = QueryType.Simple,
                SuggestedHyDEWeight = 0.6
            };

            try
            {
                var jsonDoc = JsonDocument.Parse(jsonResponse);
                var root = jsonDoc.RootElement;

                if (root.TryGetProperty("isRecommended", out var isRec))
                    recommendation.IsRecommended = isRec.GetBoolean();

                if (root.TryGetProperty("confidence", out var conf))
                    recommendation.Confidence = conf.GetDouble();

                if (root.TryGetProperty("reason", out var reason))
                    recommendation.Reason = reason.GetString() ?? "";

                if (root.TryGetProperty("queryType", out var qType))
                {
                    var queryTypeStr = qType.GetString();
                    if (Enum.TryParse<QueryType>(queryTypeStr, out var parsedType))
                    {
                        recommendation.QueryType = parsedType;
                    }
                }

                if (root.TryGetProperty("suggestedHyDEWeight", out var weight))
                    recommendation.SuggestedHyDEWeight = weight.GetDouble();
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse HyDE recommendation JSON");
            }

            _logger.LogInformation("HyDE analysis: Recommended={Recommended}, Type={Type}, Confidence={Confidence:F2}",
                recommendation.IsRecommended, recommendation.QueryType, recommendation.Confidence);

            return recommendation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing query for HyDE");
            return new HyDERecommendation
            {
                IsRecommended = false,
                Confidence = 0.5,
                Reason = "Errore durante l'analisi",
                QueryType = QueryType.Simple
            };
        }
    }

    /// <summary>
    /// Costruisce il prompt per generare il documento ipotetico
    /// </summary>
    private string BuildHyDEPrompt(string query, string? domainContext)
    {
        var promptBuilder = new StringBuilder();
        promptBuilder.AppendLine("Genera un documento aziendale che potrebbe rispondere alla seguente domanda:");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine($"Domanda: {query}");
        promptBuilder.AppendLine();

        if (!string.IsNullOrWhiteSpace(domainContext))
        {
            promptBuilder.AppendLine($"Contesto del dominio: {domainContext}");
            promptBuilder.AppendLine();
        }

        promptBuilder.AppendLine("Istruzioni:");
        promptBuilder.AppendLine("- Scrivi in stile formale e professionale");
        promptBuilder.AppendLine("- Usa terminologia appropriata al contesto aziendale");
        promptBuilder.AppendLine($"- Lunghezza: circa {_config.TargetDocumentLength} parole");
        promptBuilder.AppendLine("- Struttura il contenuto in modo chiaro e logico");
        promptBuilder.AppendLine("- NON devi essere fattualmente accurato, questo è un documento IPOTETICO");
        promptBuilder.AppendLine("- L'obiettivo è creare un testo stilisticamente simile ai documenti reali");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("Documento:");

        return promptBuilder.ToString();
    }
}
