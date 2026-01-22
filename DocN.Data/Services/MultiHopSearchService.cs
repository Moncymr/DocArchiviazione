using DocN.Core.Interfaces;
using DocN.Data.Services;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Diagnostics;
using System.Text.Json;

namespace DocN.Data.Services;

/// <summary>
/// Implementation of multi-hop search for complex queries
/// Decomposes complex queries into multiple sub-queries and aggregates results
/// </summary>
public class MultiHopSearchService : IMultiHopSearchService
{
    private readonly IHybridSearchService _searchService;
    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chatService;
    private readonly ILogger<MultiHopSearchService> _logger;

    public MultiHopSearchService(
        IHybridSearchService searchService,
        Kernel kernel,
        ILogger<MultiHopSearchService> logger)
    {
        _searchService = searchService;
        _kernel = kernel;
        _chatService = kernel.GetRequiredService<IChatCompletionService>();
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<MultiHopSearchResult> SearchAsync(string query, int maxHops = 3, int topKPerHop = 5)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new MultiHopSearchResult
        {
            OriginalQuery = query,
            Hops = new List<HopStep>()
        };

        try
        {
            // Step 1: Decompose query into sub-queries
            var subQueries = await DecomposeQueryAsync(query, maxHops);
            
            _logger.LogInformation(
                "Decomposed query '{Query}' into {Count} sub-queries",
                TruncateQuery(query), subQueries.Count);

            // Step 2: Execute each hop
            var allResults = new List<SearchResult>();
            var seenDocIds = new HashSet<int>();

            for (int i = 0; i < Math.Min(subQueries.Count, maxHops); i++)
            {
                var hopWatch = Stopwatch.StartNew();
                var subQuery = subQueries[i];

                _logger.LogDebug("Executing hop {Hop}: {SubQuery}", i + 1, subQuery.Query);

                // Execute search for this sub-query
                var hopResults = await _searchService.SearchAsync(subQuery.Query, new SearchOptions
                {
                    TopK = topKPerHop,
                    MinSimilarity = 0.3
                });

                // Filter out duplicates
                var newResults = hopResults.Where(r => !seenDocIds.Contains(r.Document.Id)).ToList();
                foreach (var r in newResults)
                {
                    seenDocIds.Add(r.Document.Id);
                    allResults.Add(r);
                }

                hopWatch.Stop();

                result.Hops.Add(new HopStep
                {
                    HopNumber = i + 1,
                    SubQuery = subQuery.Query,
                    Reasoning = subQuery.Reasoning,
                    Results = newResults.Cast<object>().ToList(),
                    TimeMs = hopWatch.ElapsedMilliseconds
                });

                _logger.LogDebug(
                    "Hop {Hop} completed in {Time}ms with {Count} new results",
                    i + 1, hopWatch.ElapsedMilliseconds, newResults.Count);
            }

            // Step 3: Rank and aggregate final results
            result.FinalResults = allResults
                .OrderByDescending(r => r.CombinedScore)
                .Take(10)
                .Cast<object>()
                .ToList();

            result.TotalHops = result.Hops.Count;
            stopwatch.Stop();
            result.TotalTimeMs = stopwatch.ElapsedMilliseconds;

            _logger.LogInformation(
                "Multi-hop search completed: {Hops} hops, {Results} results in {Time}ms",
                result.TotalHops, result.FinalResults.Count, result.TotalTimeMs);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in multi-hop search for query: {Query}", query);
            
            // Fallback to simple search
            var fallbackResults = await _searchService.SearchAsync(query, new SearchOptions { TopK = 10 });
            result.FinalResults = fallbackResults.Cast<object>().ToList();
            stopwatch.Stop();
            result.TotalTimeMs = stopwatch.ElapsedMilliseconds;
            
            return result;
        }
    }

    /// <summary>
    /// Decompose a complex query into multiple sub-queries using AI
    /// </summary>
    private async Task<List<SubQuery>> DecomposeQueryAsync(string query, int maxSubQueries)
    {
        try
        {
            var prompt = $@"Analizza questa query complessa e decomponila in {maxSubQueries} sotto-query più semplici per una ricerca multi-step.
Ogni sotto-query dovrebbe focalizzarsi su un aspetto specifico della query originale.

Query originale: {query}

Restituisci un array JSON con questo formato:
[
  {{
    ""query"": ""prima sotto-query"",
    ""reasoning"": ""perché questa sotto-query è importante""
  }},
  ...
]

Rispondi SOLO con il JSON, senza testo aggiuntivo.";

            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage("Sei un esperto di query decomposition per sistemi di ricerca. Analizza query complesse e decomponile in sotto-query più semplici.");
            chatHistory.AddUserMessage(prompt);

            var settings = new OpenAIPromptExecutionSettings
            {
                MaxTokens = 500,
                Temperature = 0.3
            };

            var result = await _chatService.GetChatMessageContentAsync(chatHistory, settings, _kernel);
            var jsonResponse = result.Content?.Trim() ?? "[]";

            // Try to parse JSON
            var subQueries = JsonSerializer.Deserialize<List<SubQuery>>(jsonResponse);
            
            if (subQueries == null || !subQueries.Any())
            {
                // Fallback: use original query
                return new List<SubQuery>
                {
                    new SubQuery { Query = query, Reasoning = "Original query" }
                };
            }

            return subQueries;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error decomposing query, using original query");
            return new List<SubQuery>
            {
                new SubQuery { Query = query, Reasoning = "Original query (decomposition failed)" }
            };
        }
    }

    private string TruncateQuery(string query)
    {
        return query.Length > 50 ? query.Substring(0, 47) + "..." : query;
    }

    private class SubQuery
    {
        public string Query { get; set; } = string.Empty;
        public string Reasoning { get; set; } = string.Empty;
    }
}
