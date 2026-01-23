using DocN.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace DocN.Data.Services;

/// <summary>
/// BM25 search service for keyword-based document retrieval
/// BM25 is a probabilistic ranking function used for text search
/// </summary>
public interface IBM25SearchService
{
    /// <summary>
    /// Search documents using BM25 algorithm
    /// </summary>
    /// <param name="query">Search query</param>
    /// <param name="topK">Number of results to return</param>
    /// <param name="categoryFilter">Optional category filter</param>
    /// <param name="ownerId">Optional owner filter</param>
    /// <returns>List of documents with BM25 scores</returns>
    Task<List<BM25Result>> SearchAsync(string query, int topK = 10, string? categoryFilter = null, string? ownerId = null);

    /// <summary>
    /// Calculate BM25 score for a document given a query
    /// </summary>
    /// <param name="query">Search query</param>
    /// <param name="document">Document to score</param>
    /// <returns>BM25 score</returns>
    double CalculateBM25Score(string query, Document document);
}

/// <summary>
/// BM25 search result
/// </summary>
public class BM25Result
{
    public Document Document { get; set; } = null!;
    public double Score { get; set; }
    public Dictionary<string, int> TermFrequencies { get; set; } = new();
}

/// <summary>
/// Implementation of BM25 search algorithm
/// </summary>
public class BM25SearchService : IBM25SearchService
{
    private readonly ApplicationDbContext _context;
    
    // BM25 parameters
    private const double K1 = 1.5; // Term frequency saturation parameter
    private const double B = 0.75; // Length normalization parameter

    public BM25SearchService(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Search documents using BM25 algorithm
    /// </summary>
    public async Task<List<BM25Result>> SearchAsync(string query, int topK = 10, string? categoryFilter = null, string? ownerId = null)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new List<BM25Result>();

        // Tokenize query
        var queryTerms = Tokenize(query);
        if (!queryTerms.Any())
            return new List<BM25Result>();

        // Build document query with filters
        var documentsQuery = _context.Documents.AsQueryable();
        
        if (!string.IsNullOrEmpty(categoryFilter))
        {
            documentsQuery = documentsQuery.Where(d => d.ActualCategory == categoryFilter);
        }
        
        if (!string.IsNullOrEmpty(ownerId))
        {
            documentsQuery = documentsQuery.Where(d => d.OwnerId == ownerId);
        }

        // Load documents
        var documents = await documentsQuery.ToListAsync();
        
        if (!documents.Any())
            return new List<BM25Result>();

        // Calculate average document length
        var avgDocLength = documents.Average(d => GetDocumentLength(d));
        var totalDocs = documents.Count;

        // Calculate IDF for each query term
        var idfScores = CalculateIDF(queryTerms, documents);

        // Calculate BM25 score for each document
        var results = new List<BM25Result>();
        
        foreach (var doc in documents)
        {
            var score = CalculateBM25ScoreInternal(queryTerms, doc, idfScores, avgDocLength);
            
            if (score > 0)
            {
                var termFreqs = GetTermFrequencies(queryTerms, doc);
                results.Add(new BM25Result
                {
                    Document = doc,
                    Score = score,
                    TermFrequencies = termFreqs
                });
            }
        }

        // Sort by score and return top K
        return results
            .OrderByDescending(r => r.Score)
            .Take(topK)
            .ToList();
    }

    /// <summary>
    /// Calculate BM25 score for a document
    /// </summary>
    public double CalculateBM25Score(string query, Document document)
    {
        var queryTerms = Tokenize(query);
        if (!queryTerms.Any())
            return 0.0;

        // For single document, use default values
        var avgDocLength = GetDocumentLength(document);
        var idfScores = new Dictionary<string, double>();
        
        // Simplified IDF (assuming term appears in this document only)
        foreach (var term in queryTerms)
        {
            idfScores[term] = Math.Log(2.0); // Neutral IDF for single doc
        }

        return CalculateBM25ScoreInternal(queryTerms, document, idfScores, avgDocLength);
    }

    /// <summary>
    /// Internal BM25 score calculation
    /// </summary>
    private double CalculateBM25ScoreInternal(
        List<string> queryTerms, 
        Document document, 
        Dictionary<string, double> idfScores, 
        double avgDocLength)
    {
        var docLength = GetDocumentLength(document);
        var score = 0.0;

        foreach (var term in queryTerms)
        {
            var termFreq = GetTermFrequency(term, document);
            
            if (termFreq > 0 && idfScores.ContainsKey(term))
            {
                var idf = idfScores[term];
                var numerator = termFreq * (K1 + 1);
                var denominator = termFreq + K1 * (1 - B + B * (docLength / avgDocLength));
                
                score += idf * (numerator / denominator);
            }
        }

        return score;
    }

    /// <summary>
    /// Calculate Inverse Document Frequency (IDF) for query terms
    /// IDF = log((N - n + 0.5) / (n + 0.5))
    /// where N is total documents and n is documents containing the term
    /// </summary>
    private Dictionary<string, double> CalculateIDF(List<string> queryTerms, List<Document> documents)
    {
        var idfScores = new Dictionary<string, double>();
        var totalDocs = documents.Count;

        foreach (var term in queryTerms)
        {
            var docsContainingTerm = documents.Count(d => ContainsTerm(d, term));
            
            if (docsContainingTerm > 0)
            {
                // BM25 IDF formula
                var idf = Math.Log((totalDocs - docsContainingTerm + 0.5) / (docsContainingTerm + 0.5) + 1.0);
                idfScores[term] = Math.Max(idf, 0.01); // Avoid negative IDF
            }
            else
            {
                idfScores[term] = 0.0;
            }
        }

        return idfScores;
    }

    /// <summary>
    /// Get term frequency in document
    /// </summary>
    private int GetTermFrequency(string term, Document document)
    {
        var content = GetDocumentContent(document).ToLowerInvariant();
        var termLower = term.ToLowerInvariant();
        
        var count = 0;
        var index = 0;
        
        while ((index = content.IndexOf(termLower, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += termLower.Length;
        }
        
        return count;
    }

    /// <summary>
    /// Get term frequencies for all query terms
    /// </summary>
    private Dictionary<string, int> GetTermFrequencies(List<string> queryTerms, Document document)
    {
        var frequencies = new Dictionary<string, int>();
        
        foreach (var term in queryTerms)
        {
            frequencies[term] = GetTermFrequency(term, document);
        }
        
        return frequencies;
    }

    /// <summary>
    /// Check if document contains term
    /// </summary>
    private bool ContainsTerm(Document document, string term)
    {
        var content = GetDocumentContent(document).ToLowerInvariant();
        return content.Contains(term.ToLowerInvariant(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Get document length (word count)
    /// </summary>
    private int GetDocumentLength(Document document)
    {
        var content = GetDocumentContent(document);
        
        if (string.IsNullOrWhiteSpace(content))
            return 0;

        return content.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
    }

    /// <summary>
    /// Get searchable content from document
    /// </summary>
    private string GetDocumentContent(Document document)
    {
        var content = new System.Text.StringBuilder();
        
        // Weight filename higher (add it multiple times)
        if (!string.IsNullOrEmpty(document.FileName))
        {
            content.Append(document.FileName);
            content.Append(" ");
            content.Append(document.FileName);
            content.Append(" ");
        }
        
        // Add extracted text
        if (!string.IsNullOrEmpty(document.ExtractedText))
        {
            content.Append(document.ExtractedText);
            content.Append(" ");
        }
        
        // Add category
        if (!string.IsNullOrEmpty(document.ActualCategory))
        {
            content.Append(document.ActualCategory);
            content.Append(" ");
        }
        
        return content.ToString();
    }

    /// <summary>
    /// Tokenize text into terms
    /// </summary>
    private List<string> Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new List<string>();

        // Split into words and normalize
        var tokens = text
            .Split(new[] { ' ', '\n', '\r', '\t', '.', ',', ';', ':', '!', '?', '"', '\'', '(', ')', '[', ']', '{', '}' },
                StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim().ToLowerInvariant())
            .Where(t => t.Length > 1) // Filter very short tokens
            .Distinct()
            .ToList();

        return tokens;
    }
}
