using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.Logging;
using DocN.Data.Services;

namespace DocN.Data.Services;

/// <summary>
/// Service for query expansion - enhances queries with synonyms and related terms
/// </summary>
public interface IQueryExpansionService
{
    /// <summary>
    /// Expand query with synonyms and related terms
    /// </summary>
    /// <param name="query">Original query</param>
    /// <param name="maxExpansions">Maximum number of expansion terms</param>
    /// <returns>Expanded query with additional terms</returns>
    Task<QueryExpansionResult> ExpandQueryAsync(string query, int maxExpansions = 10);

    /// <summary>
    /// Generate related keywords for a query
    /// </summary>
    /// <param name="query">Original query</param>
    /// <param name="count">Number of related terms to generate</param>
    /// <returns>List of related terms</returns>
    Task<List<string>> GenerateRelatedTermsAsync(string query, int count = 5);

    /// <summary>
    /// Expand query using manual synonym dictionary
    /// </summary>
    /// <param name="query">Original query</param>
    /// <returns>Expanded query with synonyms</returns>
    string ExpandWithSynonyms(string query);
}

/// <summary>
/// Query expansion result
/// </summary>
public class QueryExpansionResult
{
    public string OriginalQuery { get; set; } = string.Empty;
    public string ExpandedQuery { get; set; } = string.Empty;
    public List<string> ExpansionTerms { get; set; } = new();
    public Dictionary<string, List<string>> Synonyms { get; set; } = new();
}

/// <summary>
/// Implementation of query expansion service
/// </summary>
public class QueryExpansionService : IQueryExpansionService
{
    private readonly IKernelProvider? _kernelProvider;
    private readonly ILogger<QueryExpansionService> _logger;

    // Synonym dictionary for common terms (Italian and English)
    private static readonly Dictionary<string, List<string>> _synonymDictionary = new()
    {
        // Italian terms
        { "contratto", new List<string> { "accordo", "convenzione", "patto" } },
        { "fattura", new List<string> { "ricevuta", "documento fiscale", "nota" } },
        { "documento", new List<string> { "file", "testo", "atto", "carta" } },
        { "cliente", new List<string> { "utente", "consumatore", "compratore" } },
        { "fornitore", new List<string> { "venditore", "distributore", "supplier" } },
        { "pagamento", new List<string> { "saldo", "versamento", "transazione" } },
        { "scadenza", new List<string> { "termine", "deadline", "data limite" } },
        { "prezzo", new List<string> { "costo", "importo", "tariffa", "valore" } },
        { "ordine", new List<string> { "commissione", "richiesta", "ordinazione" } },
        { "servizio", new List<string> { "prestazione", "assistenza", "supporto" } },
        
        // English terms
        { "contract", new List<string> { "agreement", "deal", "arrangement" } },
        { "invoice", new List<string> { "bill", "receipt", "statement" } },
        { "document", new List<string> { "file", "record", "paper" } },
        { "customer", new List<string> { "client", "buyer", "consumer" } },
        { "supplier", new List<string> { "vendor", "provider", "seller" } },
        { "payment", new List<string> { "transaction", "settlement", "remittance" } },
        { "deadline", new List<string> { "due date", "expiry", "time limit" } },
        { "price", new List<string> { "cost", "amount", "rate", "value" } },
        { "order", new List<string> { "purchase", "request", "requisition" } },
        { "service", new List<string> { "support", "assistance", "offering" } }
    };

    public QueryExpansionService(
        ILogger<QueryExpansionService> logger,
        IKernelProvider? kernelProvider = null)
    {
        _logger = logger;
        _kernelProvider = kernelProvider;
    }

    /// <summary>
    /// Expand query with synonyms and related terms using AI
    /// </summary>
    public async Task<QueryExpansionResult> ExpandQueryAsync(string query, int maxExpansions = 10)
    {
        try
        {
            // Start with manual synonym expansion
            var manualExpanded = ExpandWithSynonyms(query);
            var synonymsFound = new Dictionary<string, List<string>>();

            // Extract words from query
            var words = query.Split(new[] { ' ', ',', '.', ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var word in words)
            {
                var lowerWord = word.ToLowerInvariant().Trim();
                if (_synonymDictionary.ContainsKey(lowerWord))
                {
                    synonymsFound[word] = _synonymDictionary[lowerWord];
                }
            }

            // Try AI-based expansion if available
            List<string> aiExpansionTerms = new();
            if (_kernelProvider != null)
            {
                try
                {
                    aiExpansionTerms = await GenerateRelatedTermsAsync(query, maxExpansions);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "AI-based query expansion failed, using manual expansion only");
                }
            }

            // Combine expansions
            var allExpansionTerms = new List<string>();
            allExpansionTerms.AddRange(synonymsFound.Values.SelectMany(v => v).Distinct());
            allExpansionTerms.AddRange(aiExpansionTerms);
            allExpansionTerms = allExpansionTerms.Distinct().Take(maxExpansions).ToList();

            var expandedQuery = manualExpanded;
            if (aiExpansionTerms.Any())
            {
                expandedQuery = $"{manualExpanded} {string.Join(" ", aiExpansionTerms)}";
            }

            return new QueryExpansionResult
            {
                OriginalQuery = query,
                ExpandedQuery = expandedQuery.Trim(),
                ExpansionTerms = allExpansionTerms,
                Synonyms = synonymsFound
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Query expansion failed for query: {Query}", query);
            return new QueryExpansionResult
            {
                OriginalQuery = query,
                ExpandedQuery = query,
                ExpansionTerms = new List<string>(),
                Synonyms = new Dictionary<string, List<string>>()
            };
        }
    }

    /// <summary>
    /// Generate related keywords using AI
    /// </summary>
    public async Task<List<string>> GenerateRelatedTermsAsync(string query, int count = 5)
    {
        if (_kernelProvider == null)
        {
            return new List<string>();
        }

        try
        {
            var kernel = await _kernelProvider.GetKernelAsync();
            if (kernel == null)
            {
                return new List<string>();
            }

            var chatService = kernel.GetRequiredService<IChatCompletionService>();
            
            var prompt = $@"Given the search query: ""{query}""

Generate {count} related keywords or phrases that someone might use when searching for similar information.
The keywords should be:
- Semantically related to the query
- Useful for expanding search results
- In the same language as the query
- Separated by commas

Only return the comma-separated keywords, nothing else.";

            var chatHistory = new Microsoft.SemanticKernel.ChatCompletion.ChatHistory();
            chatHistory.AddUserMessage(prompt);

            var response = await chatService.GetChatMessageContentAsync(chatHistory);
            var content = response.Content ?? string.Empty;

            // Parse comma-separated keywords
            var keywords = content
                .Split(new[] { ',', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(k => k.Trim().Trim('"').Trim('\''))
                .Where(k => !string.IsNullOrWhiteSpace(k) && k.Length > 2)
                .Take(count)
                .ToList();

            _logger.LogDebug("Generated {Count} related terms for query: {Query}", keywords.Count, query);
            return keywords;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate related terms for query: {Query}", query);
            return new List<string>();
        }
    }

    /// <summary>
    /// Expand query using manual synonym dictionary
    /// </summary>
    public string ExpandWithSynonyms(string query)
    {
        var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var expandedTerms = new List<string> { query };

        foreach (var word in words)
        {
            var lowerWord = word.ToLowerInvariant().Trim(new[] { '.', ',', ';', ':', '!', '?' });
            
            if (_synonymDictionary.ContainsKey(lowerWord))
            {
                // Add first synonym only to keep query manageable
                var synonym = _synonymDictionary[lowerWord].FirstOrDefault();
                if (synonym != null)
                {
                    expandedTerms.Add(synonym);
                }
            }
        }

        return string.Join(" ", expandedTerms.Distinct());
    }
}
