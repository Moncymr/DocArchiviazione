using DocN.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace DocN.Data.Services;

/// <summary>
/// Implementation of BM25 scoring algorithm for document ranking
/// BM25 is a probabilistic ranking function that considers term frequency, document length, and inverse document frequency
/// </summary>
public class BM25Service : IBM25Service
{
    private readonly ILogger<BM25Service> _logger;
    
    // BM25 hyperparameters
    private const double K1 = 1.5; // Term frequency saturation parameter
    private const double B = 0.75; // Length normalization parameter
    
    // Document statistics
    private double _averageDocumentLength = 0;
    private Dictionary<string, int> _documentFrequency = new(); // Number of documents containing each term
    private int _totalDocuments = 0;
    
    public BM25Service(ILogger<BM25Service> logger)
    {
        _logger = logger;
    }
    
    /// <inheritdoc/>
    public void UpdateDocumentStatistics(Dictionary<int, string> documents)
    {
        _totalDocuments = documents.Count;
        _documentFrequency.Clear();
        
        if (_totalDocuments == 0)
        {
            _averageDocumentLength = 0;
            return;
        }
        
        long totalLength = 0;
        var termDocumentSets = new Dictionary<string, HashSet<int>>();
        
        foreach (var (docId, text) in documents)
        {
            var terms = Tokenize(text);
            totalLength += terms.Count;
            
            foreach (var term in terms.Distinct())
            {
                if (!termDocumentSets.ContainsKey(term))
                {
                    termDocumentSets[term] = new HashSet<int>();
                }
                termDocumentSets[term].Add(docId);
            }
        }
        
        _averageDocumentLength = (double)totalLength / _totalDocuments;
        
        foreach (var (term, docSet) in termDocumentSets)
        {
            _documentFrequency[term] = docSet.Count;
        }
        
        _logger.LogInformation(
            "Updated BM25 statistics: {TotalDocs} documents, {AvgLength:F1} avg length, {UniqueTerms} unique terms",
            _totalDocuments, _averageDocumentLength, _documentFrequency.Count);
    }
    
    /// <inheritdoc/>
    public double CalculateScore(string query, string documentText, Dictionary<string, double>? documentFieldWeights = null)
    {
        var queryTerms = Tokenize(query);
        var documentTerms = Tokenize(documentText);
        
        if (!queryTerms.Any() || !documentTerms.Any())
        {
            return 0;
        }
        
        var documentLength = documentTerms.Count;
        var termFrequency = CountTermFrequencies(documentTerms);
        
        double score = 0;
        
        foreach (var queryTerm in queryTerms.Distinct())
        {
            if (!termFrequency.ContainsKey(queryTerm))
            {
                continue;
            }
            
            var tf = termFrequency[queryTerm];
            var idf = CalculateIDF(queryTerm);
            
            // BM25 formula
            var numerator = tf * (K1 + 1);
            var denominator = tf + K1 * (1 - B + B * (documentLength / _averageDocumentLength));
            
            score += idf * (numerator / denominator);
        }
        
        return score;
    }
    
    /// <inheritdoc/>
    public Dictionary<int, double> CalculateScores(string query, Dictionary<int, string> documents)
    {
        var scores = new Dictionary<int, double>();
        
        foreach (var (docId, text) in documents)
        {
            scores[docId] = CalculateScore(query, text);
        }
        
        return scores;
    }
    
    /// <summary>
    /// Calculate Inverse Document Frequency for a term
    /// </summary>
    private double CalculateIDF(string term)
    {
        if (_totalDocuments == 0)
        {
            return 0;
        }
        
        // Get number of documents containing the term
        var docsWithTerm = _documentFrequency.GetValueOrDefault(term, 0);
        
        if (docsWithTerm == 0)
        {
            return 0;
        }
        
        // IDF formula: log((N - df + 0.5) / (df + 0.5) + 1)
        // This is the Robertson/Zaragoza IDF variant used in modern BM25
        var idf = Math.Log((_totalDocuments - docsWithTerm + 0.5) / (docsWithTerm + 0.5) + 1);
        
        return Math.Max(0, idf); // Ensure non-negative
    }
    
    /// <summary>
    /// Tokenize text into terms
    /// </summary>
    private List<string> Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new List<string>();
        }
        
        // Convert to lowercase and split on non-alphanumeric characters
        var normalized = text.ToLowerInvariant();
        var terms = Regex.Split(normalized, @"[^a-z0-9àèéìòù]+")
            .Where(t => t.Length > 1) // Filter out single characters
            .ToList();
        
        return terms;
    }
    
    /// <summary>
    /// Count term frequencies in document
    /// </summary>
    private Dictionary<string, int> CountTermFrequencies(List<string> terms)
    {
        var frequencies = new Dictionary<string, int>();
        
        foreach (var term in terms)
        {
            frequencies[term] = frequencies.GetValueOrDefault(term, 0) + 1;
        }
        
        return frequencies;
    }
}
