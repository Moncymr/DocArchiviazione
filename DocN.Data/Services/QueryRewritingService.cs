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
/// Implementazione del servizio di Query Rewriting utilizzando AI
/// Riformula e ottimizza le query utente per migliorare i risultati RAG
/// </summary>
public class QueryRewritingService : IQueryRewritingService
{
    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chatService;
    private readonly ILogger<QueryRewritingService> _logger;

    /// <summary>
    /// Inizializza una nuova istanza del servizio di query rewriting
    /// </summary>
    /// <param name="kernel">Kernel di Semantic Kernel configurato</param>
    /// <param name="logger">Logger per diagnostica</param>
    public QueryRewritingService(
        Kernel kernel,
        ILogger<QueryRewritingService> logger)
    {
        _kernel = kernel;
        _chatService = kernel.GetRequiredService<IChatCompletionService>();
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<string> RewriteQueryAsync(string originalQuery, string? conversationContext = null)
    {
        try
        {
            _logger.LogDebug("Rewriting query: {Query}", originalQuery);

            var prompt = BuildRewritePrompt(originalQuery, conversationContext);
            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage("Sei un esperto di query rewriting per sistemi RAG. Il tuo compito è riformulare le query ambigue in versioni più chiare e specifiche.");
            chatHistory.AddUserMessage(prompt);

            var settings = new OpenAIPromptExecutionSettings
            {
                MaxTokens = 200,
                Temperature = 0.3, // Bassa temperatura per maggiore precisione
                TopP = 0.9
            };

            var result = await _chatService.GetChatMessageContentAsync(chatHistory, settings, _kernel);
            var rewrittenQuery = result.Content?.Trim() ?? originalQuery;

            _logger.LogInformation("Query rewritten: '{Original}' → '{Rewritten}'", originalQuery, rewrittenQuery);
            return rewrittenQuery;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rewriting query: {Query}", originalQuery);
            return originalQuery; // Fallback alla query originale in caso di errore
        }
    }

    /// <inheritdoc/>
    public async Task<string> ExpandQueryAsync(string query, int maxExpansions = 3)
    {
        try
        {
            _logger.LogDebug("Expanding query: {Query}", query);

            var prompt = $@"Espandi la seguente query aggiungendo {maxExpansions} sinonimi o termini correlati in italiano.
Restituisci solo i termini aggiuntivi separati da virgola, senza la query originale.

Query: {query}

Termini aggiuntivi:";

            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage("Sei un esperto di linguistica italiana. Genera sinonimi e termini correlati per migliorare le ricerche.");
            chatHistory.AddUserMessage(prompt);

            var settings = new OpenAIPromptExecutionSettings
            {
                MaxTokens = 100,
                Temperature = 0.5,
                TopP = 0.9
            };

            var result = await _chatService.GetChatMessageContentAsync(chatHistory, settings, _kernel);
            var expansions = result.Content?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(expansions))
                return query;

            // Split and clean expansion terms
            var expansionTerms = expansions.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t));

            var expandedQuery = $"{query} OR {string.Join(" OR ", expansionTerms)}";
            _logger.LogDebug("Query expanded: '{Original}' → '{Expanded}'", query, expandedQuery);
            
            return expandedQuery;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error expanding query: {Query}", query);
            return query;
        }
    }

    /// <inheritdoc/>
    public async Task<List<string>> GenerateMultiQueryVariantsAsync(string query, int numVariants = 3)
    {
        try
        {
            _logger.LogDebug("Generating {NumVariants} query variants for: {Query}", numVariants, query);

            var prompt = $@"Genera {numVariants} varianti diverse della seguente query in italiano, 
usando prospettive e formulazioni differenti ma mantenendo lo stesso intento di ricerca.

Query originale: {query}

Restituisci le varianti in formato JSON come array di stringhe:
{{""variants"": [""variante 1"", ""variante 2"", ""variante 3""]}}";

            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage("Sei un esperto di query reformulation per sistemi RAG. Genera varianti semanticamente equivalenti ma linguisticamente diverse.");
            chatHistory.AddUserMessage(prompt);

            var settings = new OpenAIPromptExecutionSettings
            {
                MaxTokens = 300,
                Temperature = 0.7,
                TopP = 0.9,
                ResponseFormat = "json_object"
            };

            var result = await _chatService.GetChatMessageContentAsync(chatHistory, settings, _kernel);
            var jsonResponse = result.Content?.Trim() ?? "";

            // Parse JSON response
            var variants = new List<string> { query }; // Include sempre la query originale
            
            try
            {
                var jsonDoc = JsonDocument.Parse(jsonResponse);
                if (jsonDoc.RootElement.TryGetProperty("variants", out var variantsArray))
                {
                    foreach (var variant in variantsArray.EnumerateArray())
                    {
                        var variantText = variant.GetString();
                        if (!string.IsNullOrWhiteSpace(variantText))
                        {
                            variants.Add(variantText);
                        }
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse JSON response for query variants");
            }

            _logger.LogInformation("Generated {Count} query variants for: {Query}", variants.Count, query);
            return variants;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating query variants: {Query}", query);
            return new List<string> { query }; // Fallback alla query originale
        }
    }

    /// <inheritdoc/>
    public async Task<List<string>> DecomposeComplexQueryAsync(string complexQuery)
    {
        try
        {
            _logger.LogDebug("Decomposing complex query: {Query}", complexQuery);

            var prompt = $@"Analizza la seguente query complessa e scomponila in sotto-query più semplici.
Se la query contiene multiple domande o concetti, separali.
Se è già semplice, restituisci solo la query originale.

Query: {complexQuery}

Restituisci le sotto-query in formato JSON come array di stringhe:
{{""subqueries"": [""sotto-query 1"", ""sotto-query 2""]}}";

            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage("Sei un esperto di analisi linguistica. Scomponi query complesse in parti più semplici mantenendo il significato.");
            chatHistory.AddUserMessage(prompt);

            var settings = new OpenAIPromptExecutionSettings
            {
                MaxTokens = 300,
                Temperature = 0.3,
                TopP = 0.9,
                ResponseFormat = "json_object"
            };

            var result = await _chatService.GetChatMessageContentAsync(chatHistory, settings, _kernel);
            var jsonResponse = result.Content?.Trim() ?? "";

            var subqueries = new List<string>();
            
            try
            {
                var jsonDoc = JsonDocument.Parse(jsonResponse);
                if (jsonDoc.RootElement.TryGetProperty("subqueries", out var subqueriesArray))
                {
                    foreach (var subquery in subqueriesArray.EnumerateArray())
                    {
                        var subqueryText = subquery.GetString();
                        if (!string.IsNullOrWhiteSpace(subqueryText))
                        {
                            subqueries.Add(subqueryText);
                        }
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse JSON response for query decomposition");
            }

            // Se non abbiamo sotto-query, usa la query originale
            if (subqueries.Count == 0)
                subqueries.Add(complexQuery);

            _logger.LogInformation("Decomposed query into {Count} subqueries: {Query}", subqueries.Count, complexQuery);
            return subqueries;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error decomposing complex query: {Query}", complexQuery);
            return new List<string> { complexQuery };
        }
    }

    /// <inheritdoc/>
    public async Task<QueryAnalysisResult> AnalyzeQueryQualityAsync(string query)
    {
        try
        {
            _logger.LogDebug("Analyzing query quality: {Query}", query);

            var prompt = $@"Analizza la qualità della seguente query per un sistema di ricerca documentale:

Query: {query}

Valuta:
1. Chiarezza (è specifica o troppo vaga?)
2. Complessità (contiene multiple domande?)
3. Ambiguità (ha riferimenti poco chiari?)

Restituisci l'analisi in formato JSON:
{{
    ""qualityScore"": 0.0-1.0,
    ""isAmbiguous"": true/false,
    ""isComplex"": true/false,
    ""isTooGeneric"": true/false,
    ""suggestions"": [""suggerimento 1"", ""suggerimento 2""],
    ""suggestedRewrite"": ""query riformulata (opzionale)""
}}";

            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage("Sei un esperto di analisi di query. Valuta la qualità delle query e suggerisci miglioramenti.");
            chatHistory.AddUserMessage(prompt);

            var settings = new OpenAIPromptExecutionSettings
            {
                MaxTokens = 400,
                Temperature = 0.3,
                TopP = 0.9,
                ResponseFormat = "json_object"
            };

            var result = await _chatService.GetChatMessageContentAsync(chatHistory, settings, _kernel);
            var jsonResponse = result.Content?.Trim() ?? "";

            // Parse JSON response
            var analysisResult = new QueryAnalysisResult
            {
                QualityScore = 0.7, // Default medio
                IsAmbiguous = false,
                IsComplex = false,
                IsTooGeneric = false,
                Suggestions = new List<string>()
            };

            try
            {
                var jsonDoc = JsonDocument.Parse(jsonResponse);
                var root = jsonDoc.RootElement;

                if (root.TryGetProperty("qualityScore", out var scoreElement))
                    analysisResult.QualityScore = scoreElement.GetDouble();

                if (root.TryGetProperty("isAmbiguous", out var ambiguousElement))
                    analysisResult.IsAmbiguous = ambiguousElement.GetBoolean();

                if (root.TryGetProperty("isComplex", out var complexElement))
                    analysisResult.IsComplex = complexElement.GetBoolean();

                if (root.TryGetProperty("isTooGeneric", out var genericElement))
                    analysisResult.IsTooGeneric = genericElement.GetBoolean();

                if (root.TryGetProperty("suggestions", out var suggestionsElement))
                {
                    foreach (var suggestion in suggestionsElement.EnumerateArray())
                    {
                        var suggestionText = suggestion.GetString();
                        if (!string.IsNullOrWhiteSpace(suggestionText))
                        {
                            analysisResult.Suggestions.Add(suggestionText);
                        }
                    }
                }

                if (root.TryGetProperty("suggestedRewrite", out var rewriteElement))
                {
                    analysisResult.SuggestedRewrite = rewriteElement.GetString();
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse JSON response for query analysis");
            }

            _logger.LogInformation("Query analysis completed. Score: {Score}, Ambiguous: {Ambiguous}", 
                analysisResult.QualityScore, analysisResult.IsAmbiguous);

            return analysisResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing query quality: {Query}", query);
            return new QueryAnalysisResult
            {
                QualityScore = 0.5,
                Suggestions = new List<string> { "Errore durante l'analisi della query" }
            };
        }
    }

    /// <summary>
    /// Costruisce il prompt per la riformulazione della query
    /// </summary>
    /// <param name="originalQuery">Query originale</param>
    /// <param name="conversationContext">Contesto conversazionale opzionale</param>
    /// <returns>Prompt formattato per l'AI</returns>
    private string BuildRewritePrompt(string originalQuery, string? conversationContext)
    {
        var promptBuilder = new StringBuilder();
        promptBuilder.AppendLine("Riformula la seguente query in una versione più chiara e specifica per un sistema di ricerca documentale.");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine($"Query originale: {originalQuery}");
        
        if (!string.IsNullOrWhiteSpace(conversationContext))
        {
            promptBuilder.AppendLine();
            promptBuilder.AppendLine("Contesto conversazionale:");
            promptBuilder.AppendLine(conversationContext);
        }

        promptBuilder.AppendLine();
        promptBuilder.AppendLine("Regole:");
        promptBuilder.AppendLine("- Risolvi riferimenti ambigui (es. 'quello', 'questo', 'il documento')");
        promptBuilder.AppendLine("- Rendi la query più specifica e ricercabile");
        promptBuilder.AppendLine("- Mantieni l'intento originale");
        promptBuilder.AppendLine("- Usa linguaggio naturale in italiano");
        promptBuilder.AppendLine("- Restituisci SOLO la query riformulata, senza spiegazioni");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("Query riformulata:");

        return promptBuilder.ToString();
    }
}
