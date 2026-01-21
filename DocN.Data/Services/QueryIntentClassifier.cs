using DocN.Core.Interfaces;
using System.Text.RegularExpressions;

namespace DocN.Data.Services;

/// <summary>
/// Classifies user query intent using pattern matching and keyword analysis
/// Routes queries to appropriate processing pipelines (vector search vs database aggregation)
/// </summary>
public class QueryIntentClassifier : IQueryIntentClassifier
{
    // Statistical query patterns (English and Italian)
    private static readonly string[] StatisticalKeywords = new[]
    {
        // English
        "how many", "count", "total", "number of", "how much", "quantity",
        "statistics", "stat", "sum", "average", "mean", "median",
        // Italian
        "quanti", "quante", "quanto", "quanta", "numero di", "totale",
        "statistiche", "statistica", "somma", "media", "conteggio"
    };
    
    // Metadata query patterns
    private static readonly string[] MetadataKeywords = new[]
    {
        // English
        "what categories", "list categories", "what types", "list types",
        "what extensions", "which categories", "available categories",
        "show categories", "show types", "all categories", "all types",
        // Italian
        "quali categorie", "elenca categorie", "quali tipi", "elenca tipi",
        "quali estensioni", "categorie disponibili", "mostra categorie",
        "tutte le categorie", "tutti i tipi"
    };
    
    // Semantic search indicators
    private static readonly string[] SemanticKeywords = new[]
    {
        // English
        "about", "related to", "concerning", "regarding", "find documents",
        "search for", "look for", "show me documents", "topics",
        "contains", "mentions", "discusses", "describes",
        // Italian
        "riguardo", "relativo a", "concernente", "trova documenti",
        "cerca", "cerca documenti", "mostrami documenti", "argomenti",
        "contiene", "menziona", "discute", "descrive", "tratta"
    };

    public Task<QueryIntent> ClassifyAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult(QueryIntent.SemanticSearch);
        }

        var normalizedQuery = query.ToLowerInvariant().Trim();
        
        // Check for statistical patterns first
        var hasStatisticalKeyword = StatisticalKeywords.Any(kw => 
            normalizedQuery.Contains(kw, StringComparison.OrdinalIgnoreCase));
        
        var hasSemanticKeyword = SemanticKeywords.Any(kw => 
            normalizedQuery.Contains(kw, StringComparison.OrdinalIgnoreCase));
        
        var hasMetadataKeyword = MetadataKeywords.Any(kw => 
            normalizedQuery.Contains(kw, StringComparison.OrdinalIgnoreCase));

        // Hybrid: statistical question about semantic content
        // Examples: "How many documents discuss climate change?"
        if (hasStatisticalKeyword && hasSemanticKeyword)
        {
            return Task.FromResult(QueryIntent.Hybrid);
        }
        
        // Pure statistical query
        if (hasStatisticalKeyword)
        {
            return Task.FromResult(QueryIntent.Statistical);
        }
        
        // Metadata query
        if (hasMetadataKeyword)
        {
            return Task.FromResult(QueryIntent.MetadataQuery);
        }
        
        // Check for question marks with numbers/counts (heuristic)
        // Examples: "PDFs?" might be asking for count
        if (IsLikelyCountQuestion(normalizedQuery))
        {
            return Task.FromResult(QueryIntent.Statistical);
        }
        
        // Default to semantic search
        return Task.FromResult(QueryIntent.SemanticSearch);
    }

    public async Task<bool> IsStatisticalQueryAsync(string query)
    {
        var intent = await ClassifyAsync(query);
        return intent == QueryIntent.Statistical || intent == QueryIntent.MetadataQuery;
    }

    /// <summary>
    /// Heuristic to detect queries that are likely asking for counts
    /// Examples: "PDFs?", "documents in system", "total files"
    /// </summary>
    private bool IsLikelyCountQuestion(string query)
    {
        // Pattern: "word + ?" could be asking for count
        // Examples: "pdfs?", "documenti?"
        if (Regex.IsMatch(query, @"^\w+\s*\??\s*$", RegexOptions.IgnoreCase))
        {
            return true;
        }
        
        // Phrases that imply totals
        var totalPhrases = new[] { "in system", "in total", "nel sistema", "in totale", "in tutto" };
        if (totalPhrases.Any(p => query.Contains(p, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }
        
        return false;
    }
}
