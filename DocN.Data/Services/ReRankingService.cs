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
/// Implementazione del servizio di re-ranking utilizzando AI
/// Riordina i risultati di ricerca per massimizzare la rilevanza
/// </summary>
public class ReRankingService : IReRankingService
{
    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chatService;
    private readonly ILogger<ReRankingService> _logger;
    private readonly ReRankingConfiguration _config;

    /// <summary>
    /// Inizializza una nuova istanza del servizio di re-ranking
    /// </summary>
    /// <param name="kernel">Kernel di Semantic Kernel configurato</param>
    /// <param name="logger">Logger per diagnostica</param>
    /// <param name="config">Configurazione del re-ranking (opzionale)</param>
    public ReRankingService(
        Kernel kernel,
        ILogger<ReRankingService> logger,
        ReRankingConfiguration? config = null)
    {
        _kernel = kernel;
        _chatService = kernel.GetRequiredService<IChatCompletionService>();
        _logger = logger;
        _config = config ?? new ReRankingConfiguration();
    }

    /// <inheritdoc/>
    public async Task<List<RelevantDocumentResult>> ReRankResultsAsync(
        string query,
        List<RelevantDocumentResult> results,
        int topK)
    {
        try
        {
            if (!_config.Enabled || results.Count == 0)
                return results.Take(topK).ToList();

            _logger.LogDebug("Re-ranking {Count} results for query: {Query}", results.Count, query);

            // Limita il numero di candidati per performance
            var candidates = results.Take(_config.MaxCandidates).ToList();

            // Calcola score di rilevanza per ogni risultato usando cross-encoder
            var rerankedResults = new List<(RelevantDocumentResult result, double newScore)>();

            foreach (var result in candidates)
            {
                var text = result.RelevantChunk ?? result.ExtractedText ?? "";
                if (string.IsNullOrWhiteSpace(text))
                {
                    rerankedResults.Add((result, result.SimilarityScore));
                    continue;
                }

                // Calcola nuovo score usando cross-encoder simulato
                var relevanceScore = await CalculateRelevanceScoreAsync(query, text);
                
                // Combina score originale con nuovo score (weighted average)
                var combinedScore = (result.SimilarityScore * 0.3) + (relevanceScore * 0.7);
                
                rerankedResults.Add((result, combinedScore));
            }

            // Ordina per nuovo score e aggiorna i risultati
            var finalResults = rerankedResults
                .OrderByDescending(x => x.newScore)
                .Take(topK)
                .Select(x =>
                {
                    x.result.SimilarityScore = x.newScore;
                    return x.result;
                })
                .ToList();

            _logger.LogInformation("Re-ranking completed. Top score: {TopScore:F3}, Bottom score: {BottomScore:F3}",
                finalResults.First().SimilarityScore,
                finalResults.Last().SimilarityScore);

            return finalResults;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during re-ranking. Returning original results.");
            return results.Take(topK).ToList();
        }
    }

    /// <inheritdoc/>
    public async Task<double> CalculateRelevanceScoreAsync(string query, string documentText)
    {
        try
        {
            // Tronca il testo se troppo lungo per evitare timeout
            var truncatedText = TruncateText(documentText, 1500);

            var prompt = $@"Valuta la rilevanza del seguente documento rispetto alla query su una scala da 0 a 1.

Query: {query}

Documento: {truncatedText}

Considera:
- Quanto il documento risponde direttamente alla query
- La pertinenza semantica del contenuto
- La presenza di informazioni chiave richieste

Restituisci SOLO un numero decimale tra 0.0 e 1.0 (es: 0.85), senza altro testo.";

            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage("Sei un esperto valutatore di rilevanza documentale. Restituisci solo numeri decimali tra 0 e 1.");
            chatHistory.AddUserMessage(prompt);

            var settings = new OpenAIPromptExecutionSettings
            {
                MaxTokens = 10,
                Temperature = 0.0, // Temperatura zero per consistenza
                TopP = 1.0
            };

            var result = await _chatService.GetChatMessageContentAsync(chatHistory, settings, _kernel);
            var scoreText = result.Content?.Trim() ?? "0.5";

            // Parse il risultato usando InvariantCulture
            if (double.TryParse(scoreText, System.Globalization.NumberStyles.Float, 
                System.Globalization.CultureInfo.InvariantCulture, out double score))
            {
                // Assicurati che sia nel range 0-1
                score = Math.Max(0, Math.Min(1, score));
                return score;
            }

            _logger.LogWarning("Failed to parse relevance score: {ScoreText}", scoreText);
            return 0.5; // Default medio
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating relevance score");
            return 0.5;
        }
    }

    /// <inheritdoc/>
    public async Task<List<RelevantDocumentResult>> ReRankWithLLMAsync(
        string query,
        List<RelevantDocumentResult> results,
        int topK)
    {
        try
        {
            if (results.Count == 0)
                return results;

            _logger.LogDebug("Re-ranking {Count} results with LLM for query: {Query}", results.Count, query);

            // Limita candidati
            var candidates = results.Take(_config.MaxCandidates).ToList();

            // Costruisci prompt per valutazione batch
            var prompt = BuildBatchRelevancePrompt(query, candidates);

            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(@"Sei un esperto valutatore di rilevanza documentale. 
Valuta quanto ogni documento è rilevante per la query e restituisci gli score in formato JSON.");
            chatHistory.AddUserMessage(prompt);

            var settings = new OpenAIPromptExecutionSettings
            {
                MaxTokens = 500,
                Temperature = 0.1,
                TopP = 0.95,
                ResponseFormat = "json_object"
            };

            var result = await _chatService.GetChatMessageContentAsync(chatHistory, settings, _kernel);
            var jsonResponse = result.Content?.Trim() ?? "{}";

            // Parse scores
            var scores = ParseRelevanceScores(jsonResponse, candidates.Count);

            // Combina score cross-encoder e LLM
            var rerankedResults = new List<(RelevantDocumentResult result, double finalScore)>();
            for (int i = 0; i < candidates.Count && i < scores.Count; i++)
            {
                var crossEncoderScore = candidates[i].SimilarityScore;
                var llmScore = scores[i];
                
                var finalScore = (crossEncoderScore * _config.CrossEncoderWeight) + 
                                 (llmScore * _config.LLMWeight);
                
                rerankedResults.Add((candidates[i], finalScore));
            }

            // Ordina e restituisci top K
            var finalResults = rerankedResults
                .OrderByDescending(x => x.finalScore)
                .Take(topK)
                .Select(x =>
                {
                    x.result.SimilarityScore = x.finalScore;
                    return x.result;
                })
                .ToList();

            _logger.LogInformation("LLM re-ranking completed. Processed {Count} documents", finalResults.Count);
            return finalResults;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during LLM re-ranking. Falling back to standard re-ranking.");
            return await ReRankResultsAsync(query, results, topK);
        }
    }

    /// <inheritdoc/>
    public async Task<List<RelevantDocumentResult>> FilterByRelevanceThresholdAsync(
        string query,
        List<RelevantDocumentResult> results,
        double minRelevanceScore = 0.5)
    {
        try
        {
            _logger.LogDebug("Filtering {Count} results by relevance threshold: {Threshold:F2}",
                results.Count, minRelevanceScore);

            var filteredResults = new List<RelevantDocumentResult>();

            foreach (var result in results)
            {
                // Se ha già uno score alto, mantienilo
                if (result.SimilarityScore >= minRelevanceScore)
                {
                    filteredResults.Add(result);
                    continue;
                }

                // Altrimenti ricalcola con cross-encoder
                var text = result.RelevantChunk ?? result.ExtractedText ?? "";
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                var relevanceScore = await CalculateRelevanceScoreAsync(query, text);
                
                if (relevanceScore >= minRelevanceScore)
                {
                    result.SimilarityScore = relevanceScore;
                    filteredResults.Add(result);
                }
            }

            _logger.LogInformation("Filtered to {Count} results above threshold {Threshold:F2}",
                filteredResults.Count, minRelevanceScore);

            return filteredResults;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error filtering by relevance threshold");
            return results; // Fallback a tutti i risultati
        }
    }

    /// <summary>
    /// Costruisce il prompt per valutazione batch di rilevanza
    /// </summary>
    private string BuildBatchRelevancePrompt(string query, List<RelevantDocumentResult> candidates)
    {
        var promptBuilder = new StringBuilder();
        promptBuilder.AppendLine($"Query: {query}");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("Valuta la rilevanza di ogni documento (0.0-1.0):");
        promptBuilder.AppendLine();

        for (int i = 0; i < candidates.Count; i++)
        {
            var text = candidates[i].RelevantChunk ?? candidates[i].ExtractedText ?? "";
            var truncated = TruncateText(text, 300);
            promptBuilder.AppendLine($"[Doc {i}]: {truncated}");
            promptBuilder.AppendLine();
        }

        promptBuilder.AppendLine("Restituisci gli score in formato JSON:");
        promptBuilder.AppendLine(@"{""scores"": [0.85, 0.72, 0.91, ...]}");

        return promptBuilder.ToString();
    }

    /// <summary>
    /// Parse scores dal JSON response
    /// </summary>
    private List<double> ParseRelevanceScores(string jsonResponse, int expectedCount)
    {
        var scores = new List<double>();
        
        try
        {
            var jsonDoc = JsonDocument.Parse(jsonResponse);
            if (jsonDoc.RootElement.TryGetProperty("scores", out var scoresArray))
            {
                foreach (var scoreElement in scoresArray.EnumerateArray())
                {
                    if (scoreElement.TryGetDouble(out double score))
                    {
                        scores.Add(Math.Max(0, Math.Min(1, score)));
                    }
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse relevance scores JSON");
        }

        // Se non abbiamo abbastanza scores, riempi con default
        while (scores.Count < expectedCount)
        {
            scores.Add(0.5);
        }

        return scores;
    }

    /// <summary>
    /// Tronca il testo alla lunghezza massima
    /// </summary>
    private string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text ?? string.Empty;

        return text.Substring(0, maxLength) + "...";
    }
}
