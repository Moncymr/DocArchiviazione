using DocN.Core.Interfaces;
using DocN.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using System.Net;

namespace DocN.Data.Services;

/// <summary>
/// Service for verifying RAG response quality and accuracy
/// </summary>
public class RAGQualityService : IRAGQualityService
{
    private readonly ILogger<RAGQualityService> _logger;
    private readonly ApplicationDbContext _context;
    private readonly IMultiProviderAIService _aiService;
    private const double LOW_CONFIDENCE_THRESHOLD = 0.6;
    private const double HALLUCINATION_THRESHOLD = 0.7;

    public RAGQualityService(
        ILogger<RAGQualityService> logger,
        ApplicationDbContext context,
        IMultiProviderAIService aiService)
    {
        _logger = logger;
        _context = context;
        _aiService = aiService;
    }

    public async Task<RAGQualityResult> VerifyResponseQualityAsync(
        string query,
        string response,
        IEnumerable<string> sourceDocumentIds,
        CancellationToken cancellationToken = default)
    {
        var result = new RAGQualityResult();
        
        try
        {
            // Get source texts
            var sourceTexts = await GetSourceTextsAsync(sourceDocumentIds, cancellationToken);
            
            // Split response into statements
            var statements = SplitIntoStatements(response);
            
            // Calculate confidence score for each statement
            var confidenceScores = new List<double>();
            foreach (var statement in statements)
            {
                var score = await CalculateConfidenceScoreAsync(statement, sourceTexts, cancellationToken);
                confidenceScores.Add(score);
                result.StatementConfidenceScores[statement] = score;
                
                if (score < LOW_CONFIDENCE_THRESHOLD)
                {
                    result.LowConfidenceStatements.Add(statement);
                    result.HasLowConfidenceWarnings = true;
                }
            }
            
            result.OverallConfidenceScore = confidenceScores.Any() 
                ? confidenceScores.Average() 
                : 0.0;
            
            // Detect hallucinations
            result.HallucinationDetection = await DetectHallucinationsAsync(
                response, 
                sourceTexts, 
                cancellationToken);
            
            // Verify citations
            result.CitationVerification = await VerifyCitationsAsync(
                response, 
                sourceDocumentIds, 
                cancellationToken);
            
            // Generate quality warnings
            GenerateQualityWarnings(result);
            
            // Log if quality is concerning
            if (result.HasLowConfidenceWarnings || 
                result.HallucinationDetection.HasPotentialHallucinations)
            {
                await LogDiscrepancyAsync(
                    query,
                    response,
                    "QualityWarning",
                    $"Low confidence: {result.OverallConfidenceScore:F2}, Hallucinations: {result.HallucinationDetection.HasPotentialHallucinations}",
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying RAG response quality");
            result.QualityWarnings.Add("Error during quality verification");
        }
        
        return result;
    }

    public async Task<double> CalculateConfidenceScoreAsync(
        string statement,
        IEnumerable<string> sourceTexts,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Simple semantic similarity approach
            // In production, use embeddings comparison
            var sourceList = sourceTexts.ToList();
            if (!sourceList.Any())
                return 0.0;
            
            var maxSimilarity = 0.0;
            foreach (var sourceText in sourceList)
            {
                var similarity = CalculateTextSimilarity(statement, sourceText);
                maxSimilarity = Math.Max(maxSimilarity, similarity);
            }
            
            return maxSimilarity;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating confidence score");
            return 0.0;
        }
    }

    public async Task<HallucinationDetectionResult> DetectHallucinationsAsync(
        string response,
        IEnumerable<string> sourceTexts,
        CancellationToken cancellationToken = default)
    {
        var result = new HallucinationDetectionResult();
        
        try
        {
            var statements = SplitIntoStatements(response);
            var sourceList = sourceTexts.ToList();
            
            foreach (var statement in statements)
            {
                var confidence = await CalculateConfidenceScoreAsync(
                    statement, 
                    sourceList, 
                    cancellationToken);
                
                if (confidence < HALLUCINATION_THRESHOLD)
                {
                    result.Hallucinations.Add(new HallucinationInstance
                    {
                        Text = statement,
                        Confidence = confidence,
                        Reason = confidence < 0.3 
                            ? "No supporting evidence found in source documents" 
                            : "Weak supporting evidence in source documents"
                    });
                }
            }
            
            result.HasPotentialHallucinations = result.Hallucinations.Any();
            result.HallucinationScore = result.Hallucinations.Any()
                ? 1.0 - result.Hallucinations.Average(h => h.Confidence)
                : 0.0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting hallucinations");
        }
        
        return result;
    }

    public async Task<CitationVerificationResult> VerifyCitationsAsync(
        string response,
        IEnumerable<string> sourceDocumentIds,
        CancellationToken cancellationToken = default)
    {
        var result = new CitationVerificationResult();
        
        try
        {
            // Extract citation patterns [1], [2], etc.
            var citationPattern = @"\[(\d+)\]";
            var matches = Regex.Matches(response, citationPattern);
            
            result.TotalCitations = matches.Count;
            
            var sourceTexts = await GetSourceTextsAsync(sourceDocumentIds, cancellationToken);
            var sourceList = sourceTexts.ToList();
            
            foreach (Match match in matches)
            {
                var citationNumber = match.Groups[1].Value;
                
                // Get the text around the citation for verification
                var startIndex = Math.Max(0, match.Index - 100);
                var length = Math.Min(200, response.Length - startIndex);
                var contextText = response.Substring(startIndex, length);
                
                // Verify if citation is supported by sources
                var maxSimilarity = 0.0;
                foreach (var sourceText in sourceList)
                {
                    var similarity = CalculateTextSimilarity(contextText, sourceText);
                    maxSimilarity = Math.Max(maxSimilarity, similarity);
                }
                
                var citation = new CitationInfo
                {
                    CitedText = contextText,
                    IsVerified = maxSimilarity > 0.6,
                    ConfidenceScore = maxSimilarity
                };
                
                result.Citations.Add(citation);
                
                if (citation.IsVerified)
                    result.VerifiedCitations++;
                else
                    result.UnverifiedCitations++;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying citations");
        }
        
        return result;
    }

    public async Task<RAGQualityMetrics> GetQualityMetricsAsync(
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var fromDate = from ?? DateTime.UtcNow.AddDays(-7);
        var toDate = to ?? DateTime.UtcNow;
        
        try
        {
            // Query from GoldenDatasetEvaluationRecords table to get quality metrics
            var query = _context.GoldenDatasetEvaluationRecords.AsQueryable();
            
            // Apply date filters
            query = query.Where(r => r.EvaluatedAt >= fromDate && r.EvaluatedAt <= toDate);
            
            var records = await query.ToListAsync(cancellationToken);
            
            if (!records.Any())
            {
                // Return empty metrics if no data available
                return new RAGQualityMetrics
                {
                    TotalResponses = 0,
                    AverageConfidenceScore = 0.0,
                    LowConfidenceResponses = 0,
                    HallucinationsDetected = 0,
                    CitationVerificationRate = 0.0,
                    DiscrepanciesByType = new Dictionary<string, int>(),
                    TopWarnings = new List<string>()
                };
            }
            
            // Calculate aggregate metrics from evaluation records using weighted averages
            var totalEvaluations = records.Sum(r => r.EvaluatedSamples);
            
            // Weighted average: sum(value * weight) / sum(weight)
            var totalWeightedConfidence = records.Sum(r => r.AverageConfidenceScore * r.EvaluatedSamples);
            var avgConfidence = totalEvaluations > 0 ? totalWeightedConfidence / totalEvaluations : 0.0;
            
            var totalWeightedCitation = records.Sum(r => r.CitationVerificationRate * r.EvaluatedSamples);
            var avgCitationRate = totalEvaluations > 0 ? totalWeightedCitation / totalEvaluations : 0.0;
            
            var lowConfidenceCount = (int)Math.Round(records.Sum(r => r.EvaluatedSamples * r.LowConfidenceRate));
            var hallucinationsCount = (int)Math.Round(records.Sum(r => r.EvaluatedSamples * r.HallucinationRate));
            
            return new RAGQualityMetrics
            {
                TotalResponses = totalEvaluations,
                AverageConfidenceScore = avgConfidence,
                LowConfidenceResponses = lowConfidenceCount,
                HallucinationsDetected = hallucinationsCount,
                CitationVerificationRate = avgCitationRate,
                DiscrepanciesByType = new Dictionary<string, int>(),
                TopWarnings = new List<string>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting quality metrics from database");
            
            // Return empty metrics on error
            return new RAGQualityMetrics
            {
                TotalResponses = 0,
                AverageConfidenceScore = 0.0,
                LowConfidenceResponses = 0,
                HallucinationsDetected = 0,
                CitationVerificationRate = 0.0,
                DiscrepanciesByType = new Dictionary<string, int>(),
                TopWarnings = new List<string>()
            };
        }
    }

    public async Task LogDiscrepancyAsync(
        string query,
        string response,
        string discrepancyType,
        string details,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogWarning(
                "RAG Quality Discrepancy - Type: {Type}, Query: {Query}, Details: {Details}",
                discrepancyType,
                query,
                details);
            
            // In production, save to database for review
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging discrepancy");
        }
    }

    private async Task<List<string>> GetSourceTextsAsync(
        IEnumerable<string> documentIds,
        CancellationToken cancellationToken)
    {
        var sourceTexts = new List<string>();
        
        try
        {
            foreach (var docId in documentIds)
            {
                if (int.TryParse(docId, out var intId))
                {
                    var chunks = await _context.DocumentChunks
                        .Where(c => c.DocumentId == intId)
                        .Select(c => c.ChunkText)
                        .ToListAsync(cancellationToken);
                    
                    sourceTexts.AddRange(chunks);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting source texts");
        }
        
        return sourceTexts;
    }

    private List<string> SplitIntoStatements(string text)
    {
        // Split by sentence-ending punctuation
        var sentences = Regex.Split(text, @"(?<=[.!?])\s+")
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
        
        return sentences;
    }

    private double CalculateTextSimilarity(string text1, string text2)
    {
        // Simple word overlap similarity
        // TODO: In production, replace with embeddings cosine similarity for better accuracy
        // This is a placeholder implementation for initial deployment
        var words1 = text1.ToLower()
            .Split(new[] { ' ', '.', ',', '!', '?' }, StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet();
        
        var words2 = text2.ToLower()
            .Split(new[] { ' ', '.', ',', '!', '?' }, StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet();
        
        if (words1.Count == 0 || words2.Count == 0)
            return 0.0;
        
        var intersection = words1.Intersect(words2).Count();
        var union = words1.Union(words2).Count();
        
        return (double)intersection / union;
    }

    private void GenerateQualityWarnings(RAGQualityResult result)
    {
        if (result.OverallConfidenceScore < LOW_CONFIDENCE_THRESHOLD)
        {
            result.QualityWarnings.Add(
                $"Overall confidence score is low ({result.OverallConfidenceScore:F2})");
        }
        
        if (result.HasLowConfidenceWarnings)
        {
            result.QualityWarnings.Add(
                $"{result.LowConfidenceStatements.Count} statements have low confidence");
        }
        
        if (result.HallucinationDetection.HasPotentialHallucinations)
        {
            result.QualityWarnings.Add(
                $"{result.HallucinationDetection.Hallucinations.Count} potential hallucinations detected");
        }
        
        if (result.CitationVerification.UnverifiedCitations > 0)
        {
            result.QualityWarnings.Add(
                $"{result.CitationVerification.UnverifiedCitations} citations could not be verified");
        }
    }
}
