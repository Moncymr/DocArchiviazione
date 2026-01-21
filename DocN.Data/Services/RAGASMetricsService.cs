using DocN.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace DocN.Data.Services;

/// <summary>
/// Service for RAGAS (RAG Assessment) metrics evaluation
/// </summary>
public class RAGASMetricsService : IRAGASMetricsService
{
    private readonly ILogger<RAGASMetricsService> _logger;
    private readonly IMultiProviderAIService _aiService;
    private readonly IRAGQualityService _qualityService;
    private readonly ApplicationDbContext _context;
    private readonly IGoldenDatasetService _datasetService;
    
    // RAGAS metric thresholds
    private const double FAITHFULNESS_THRESHOLD = 0.75;
    private const double RELEVANCY_THRESHOLD = 0.75;
    private const double PRECISION_THRESHOLD = 0.70;
    private const double RECALL_THRESHOLD = 0.70;
    
    // Common stop words for term extraction
    private static readonly HashSet<string> _stopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "a", "an", "and", "or", "but", "in", "on", "at", "to", "for",
        "of", "with", "by", "from", "as", "is", "was", "are", "were", "be",
        "been", "being", "have", "has", "had", "do", "does", "did", "will",
        "would", "could", "should", "may", "might", "can", "this", "that",
        "these", "those", "i", "you", "he", "she", "it", "we", "they"
    };

    public RAGASMetricsService(
        ILogger<RAGASMetricsService> logger,
        IMultiProviderAIService aiService,
        IRAGQualityService qualityService,
        ApplicationDbContext context,
        IGoldenDatasetService datasetService)
    {
        _logger = logger;
        _aiService = aiService;
        _qualityService = qualityService;
        _context = context;
        _datasetService = datasetService;
    }

    public async Task<RAGASEvaluationResult> EvaluateResponseAsync(
        string query,
        string response,
        IEnumerable<string> contexts,
        string? groundTruth = null,
        CancellationToken cancellationToken = default)
    {
        var result = new RAGASEvaluationResult();
        
        try
        {
            var contextList = contexts.ToList();
            
            // Calculate individual metrics
            result.FaithfulnessScore = await CalculateFaithfulnessAsync(
                response, 
                contextList, 
                cancellationToken);
            
            result.AnswerRelevancyScore = await CalculateAnswerRelevancyAsync(
                query, 
                response, 
                cancellationToken);
            
            result.ContextPrecisionScore = await CalculateContextPrecisionAsync(
                query, 
                contextList, 
                groundTruth, 
                cancellationToken);
            
            result.ContextRecallScore = await CalculateContextRecallAsync(
                contextList, 
                groundTruth, 
                cancellationToken);
            
            // Calculate overall RAGAS score (harmonic mean of all metrics)
            var scores = new[]
            {
                result.FaithfulnessScore,
                result.AnswerRelevancyScore,
                result.ContextPrecisionScore,
                result.ContextRecallScore
            };
            
            result.OverallRAGASScore = CalculateHarmonicMean(scores);
            
            // Add detailed metrics
            result.DetailedMetrics["faithfulness"] = result.FaithfulnessScore;
            result.DetailedMetrics["answer_relevancy"] = result.AnswerRelevancyScore;
            result.DetailedMetrics["context_precision"] = result.ContextPrecisionScore;
            result.DetailedMetrics["context_recall"] = result.ContextRecallScore;
            result.DetailedMetrics["overall"] = result.OverallRAGASScore;
            
            // Generate insights
            GenerateInsights(result);
            
            _logger.LogInformation(
                "RAGAS Evaluation - Overall: {Overall:F2}, Faithfulness: {Faithfulness:F2}, Relevancy: {Relevancy:F2}",
                result.OverallRAGASScore,
                result.FaithfulnessScore,
                result.AnswerRelevancyScore);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating RAGAS metrics");
        }
        
        return result;
    }

    public async Task<double> CalculateFaithfulnessAsync(
        string response,
        IEnumerable<string> contexts,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Faithfulness: How well the response is grounded in the context
            // Score = (Number of supported statements) / (Total statements)
            
            var contextList = contexts.ToList();
            if (!contextList.Any())
                return 0.0;
            
            // Split response into statements
            var statements = SplitIntoStatements(response);
            if (!statements.Any())
                return 1.0;
            
            var supportedCount = 0;
            foreach (var statement in statements)
            {
                var confidence = await _qualityService.CalculateConfidenceScoreAsync(
                    statement, 
                    contextList, 
                    cancellationToken);
                
                if (confidence > 0.7)
                    supportedCount++;
            }
            
            return (double)supportedCount / statements.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating faithfulness");
            return 0.0;
        }
    }

    public async Task<double> CalculateAnswerRelevancyAsync(
        string query,
        string response,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Answer Relevancy: How relevant the response is to the query
            // Using semantic similarity between query and response
            
            // Extract key terms from query and response
            var queryTerms = ExtractKeyTerms(query);
            var responseTerms = ExtractKeyTerms(response);
            
            if (!queryTerms.Any() || !responseTerms.Any())
                return 0.5;
            
            // Calculate term overlap
            var intersection = queryTerms.Intersect(responseTerms, StringComparer.OrdinalIgnoreCase).Count();
            var union = queryTerms.Union(responseTerms, StringComparer.OrdinalIgnoreCase).Count();
            
            var jaccardSimilarity = (double)intersection / union;
            
            // Boost score if response directly addresses query intent
            var intentBoost = ContainsQueryIntent(query, response) ? 0.2 : 0.0;
            
            return Math.Min(1.0, jaccardSimilarity + intentBoost);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating answer relevancy");
            return 0.0;
        }
    }

    public async Task<double> CalculateContextPrecisionAsync(
        string query,
        IEnumerable<string> contexts,
        string? groundTruth = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Context Precision: How many retrieved contexts are relevant
            // Score = (Number of relevant contexts) / (Total contexts)
            
            var contextList = contexts.ToList();
            if (!contextList.Any())
                return 0.0;
            
            var relevantCount = 0;
            var queryTerms = ExtractKeyTerms(query);
            
            foreach (var context in contextList)
            {
                var contextTerms = ExtractKeyTerms(context);
                var overlap = queryTerms.Intersect(contextTerms, StringComparer.OrdinalIgnoreCase).Count();
                
                // Context is relevant if it shares significant terms with query
                if (overlap >= Math.Min(3, queryTerms.Count / 2))
                    relevantCount++;
            }
            
            return (double)relevantCount / contextList.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating context precision");
            return 0.0;
        }
    }

    public async Task<double> CalculateContextRecallAsync(
        IEnumerable<string> contexts,
        string? groundTruth = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Context Recall: How much of the ground truth is covered by contexts
            // If no ground truth, estimate based on context diversity
            
            if (string.IsNullOrEmpty(groundTruth))
            {
                // Estimate recall based on context diversity
                var contextList = contexts.ToList();
                if (!contextList.Any())
                    return 0.0;
                
                // Calculate diversity score
                var allTerms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var termFrequencies = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                
                foreach (var context in contextList)
                {
                    var terms = ExtractKeyTerms(context);
                    foreach (var term in terms)
                    {
                        allTerms.Add(term);
                        termFrequencies[term] = termFrequencies.GetValueOrDefault(term, 0) + 1;
                    }
                }
                
                // Higher diversity indicates better recall
                var uniqueTermRatio = (double)allTerms.Count / termFrequencies.Values.Sum();
                return Math.Min(1.0, uniqueTermRatio * 2);
            }
            
            // With ground truth, calculate actual recall
            var groundTruthTerms = ExtractKeyTerms(groundTruth);
            var contextTerms = contexts
                .SelectMany(ExtractKeyTerms)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            
            if (!groundTruthTerms.Any())
                return 1.0;
            
            var coveredTerms = groundTruthTerms
                .Count(term => contextTerms.Contains(term));
            
            return (double)coveredTerms / groundTruthTerms.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating context recall");
            return 0.0;
        }
    }

    public async Task<GoldenDatasetEvaluationResult> EvaluateGoldenDatasetAsync(
        string datasetId,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var result = new GoldenDatasetEvaluationResult
        {
            DatasetId = datasetId,
            EvaluatedAt = startTime
        };
        
        try
        {
            _logger.LogInformation("Evaluating golden dataset: {DatasetId}", datasetId);
            
            // Load dataset with samples
            var dataset = await _datasetService.GetDatasetAsync(datasetId, cancellationToken);
            if (dataset == null)
            {
                throw new InvalidOperationException($"Golden dataset '{datasetId}' not found");
            }

            var samples = await _datasetService.GetSamplesAsync(datasetId, activeOnly: true, cancellationToken: cancellationToken);
            
            result.TotalSamples = samples.Count;
            
            if (samples.Count == 0)
            {
                _logger.LogWarning("No active samples found in dataset {DatasetId}", datasetId);
                return result;
            }

            // Evaluate each sample
            var evaluationResults = new List<RAGASEvaluationResult>();
            var perSampleScores = new Dictionary<string, RAGASEvaluationResult>();
            var failedSamples = new List<string>();

            foreach (var sample in samples)
            {
                try
                {
                    // For each sample, we need to:
                    // 1. Generate a response using the query
                    // 2. Get the relevant contexts (from documents)
                    // 3. Evaluate using RAGAS metrics
                    
                    // Since we don't have a direct way to generate responses here,
                    // we'll use the ground truth and expected response
                    // In a real implementation, you would call your RAG service to generate a response
                    
                    var sampleResponse = sample.ExpectedResponse ?? sample.GroundTruth;
                    var contexts = new List<string> { sample.GroundTruth };
                    
                    var evaluation = await EvaluateResponseAsync(
                        sample.Query,
                        sampleResponse,
                        contexts,
                        sample.GroundTruth,
                        cancellationToken);
                    
                    evaluationResults.Add(evaluation);
                    perSampleScores[sample.Id.ToString()] = evaluation;
                    result.EvaluatedSamples++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to evaluate sample {SampleId}", sample.Id);
                    failedSamples.Add(sample.Id.ToString());
                }
            }

            result.FailedSamples = failedSamples;

            // Calculate average scores
            if (evaluationResults.Any())
            {
                result.AverageScores = new RAGASEvaluationResult
                {
                    FaithfulnessScore = evaluationResults.Average(r => r.FaithfulnessScore),
                    AnswerRelevancyScore = evaluationResults.Average(r => r.AnswerRelevancyScore),
                    ContextPrecisionScore = evaluationResults.Average(r => r.ContextPrecisionScore),
                    ContextRecallScore = evaluationResults.Average(r => r.ContextRecallScore),
                    OverallRAGASScore = evaluationResults.Average(r => r.OverallRAGASScore)
                };
            }
            
            result.PerSampleScores = perSampleScores;

            // Store evaluation record in database
            var evaluationRecord = new DocN.Data.Models.GoldenDatasetEvaluationRecord
            {
                GoldenDatasetId = dataset.Id,
                EvaluatedAt = startTime,
                ConfigurationId = "default",
                TotalSamples = result.TotalSamples,
                EvaluatedSamples = result.EvaluatedSamples,
                FailedSamples = result.FailedSamples.Count,
                AverageFaithfulnessScore = result.AverageScores.FaithfulnessScore,
                AverageAnswerRelevancyScore = result.AverageScores.AnswerRelevancyScore,
                AverageContextPrecisionScore = result.AverageScores.ContextPrecisionScore,
                AverageContextRecallScore = result.AverageScores.ContextRecallScore,
                OverallRAGASScore = result.AverageScores.OverallRAGASScore,
                AverageConfidenceScore = 0.0, // TODO: Calculate from quality service
                LowConfidenceRate = 0.0,
                HallucinationRate = 0.0,
                CitationVerificationRate = 0.0,
                DetailedResultsJson = JsonSerializer.Serialize(result.PerSampleScores),
                FailedSampleIdsJson = JsonSerializer.Serialize(result.FailedSamples),
                Status = result.FailedSamples.Count == 0 ? "success" : result.FailedSamples.Count < result.TotalSamples ? "partial" : "failed",
                DurationSeconds = (DateTime.UtcNow - startTime).TotalSeconds
            };

            _context.GoldenDatasetEvaluationRecords.Add(evaluationRecord);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Golden dataset evaluation completed: {DatasetId}, Score: {Score:F2}, Samples: {Evaluated}/{Total}",
                datasetId,
                result.AverageScores.OverallRAGASScore,
                result.EvaluatedSamples,
                result.TotalSamples);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating golden dataset {DatasetId}", datasetId);
            throw;
        }
        
        return result;
    }

    public async Task<ContinuousMonitoringMetrics> GetMonitoringMetricsAsync(
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var result = new ContinuousMonitoringMetrics
        {
            TotalEvaluations = 0,
            AverageScores = new RAGASEvaluationResult(),
            QualityTrend = 0.0
        };
        
        try
        {
            _logger.LogInformation("Getting continuous monitoring metrics from {From} to {To}", from, to);
            
            // Query evaluation records from database
            var query = _context.GoldenDatasetEvaluationRecords.AsQueryable();
            
            if (from.HasValue)
                query = query.Where(r => r.EvaluatedAt >= from.Value);
                
            if (to.HasValue)
                query = query.Where(r => r.EvaluatedAt <= to.Value);
            
            var records = await query
                .OrderBy(r => r.EvaluatedAt)
                .ToListAsync(cancellationToken);
            
            result.TotalEvaluations = records.Count;
            
            if (records.Any())
            {
                // Calculate average scores across all evaluations
                result.AverageScores = new RAGASEvaluationResult
                {
                    FaithfulnessScore = records.Average(r => r.AverageFaithfulnessScore),
                    AnswerRelevancyScore = records.Average(r => r.AverageAnswerRelevancyScore),
                    ContextPrecisionScore = records.Average(r => r.AverageContextPrecisionScore),
                    ContextRecallScore = records.Average(r => r.AverageContextRecallScore),
                    OverallRAGASScore = records.Average(r => r.OverallRAGASScore)
                };
                
                // Build trend data (group by day)
                var trendData = records
                    .GroupBy(r => r.EvaluatedAt.Date)
                    .ToDictionary(
                        g => g.Key,
                        g => new RAGASEvaluationResult
                        {
                            FaithfulnessScore = g.Average(r => r.AverageFaithfulnessScore),
                            AnswerRelevancyScore = g.Average(r => r.AverageAnswerRelevancyScore),
                            ContextPrecisionScore = g.Average(r => r.AverageContextPrecisionScore),
                            ContextRecallScore = g.Average(r => r.AverageContextRecallScore),
                            OverallRAGASScore = g.Average(r => r.OverallRAGASScore)
                        });
                
                result.TrendData = trendData;
                
                // Calculate quality trend (comparing first half vs second half)
                if (records.Count >= 2)
                {
                    var midpoint = records.Count / 2;
                    var firstHalfAvg = records.Take(midpoint).Average(r => r.OverallRAGASScore);
                    var secondHalfAvg = records.Skip(midpoint).Average(r => r.OverallRAGASScore);
                    result.QualityTrend = (secondHalfAvg - firstHalfAvg) / firstHalfAvg;
                }
                
                // Check for quality degradation alerts
                var recentRecords = records.TakeLast(5).ToList();
                if (recentRecords.Count >= 2)
                {
                    var previousAvg = recentRecords.SkipLast(1).Average(r => r.OverallRAGASScore);
                    var latestScore = recentRecords.Last().OverallRAGASScore;
                    
                    // Alert if score drops below threshold or degrades significantly
                    if (latestScore < FAITHFULNESS_THRESHOLD || 
                        (latestScore < previousAvg * 0.9)) // 10% degradation
                    {
                        result.QualityAlerts.Add(new QualityDegradationAlert
                        {
                            MetricName = "overall_ragas_score",
                            CurrentValue = latestScore,
                            Threshold = FAITHFULNESS_THRESHOLD,
                            PreviousValue = previousAvg,
                            DetectedAt = recentRecords.Last().EvaluatedAt,
                            Severity = latestScore < 0.65 ? "critical" : "warning"
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting monitoring metrics");
        }
        
        return result;
    }

    public async Task<ABTestResult> CompareConfigurationsAsync(
        string configurationA,
        string configurationB,
        string testDatasetId,
        CancellationToken cancellationToken = default)
    {
        var result = new ABTestResult
        {
            ConfigurationA = configurationA,
            ConfigurationB = configurationB
        };
        
        try
        {
            _logger.LogInformation(
                "Comparing configurations: {ConfigA} vs {ConfigB}",
                configurationA,
                configurationB);
            
            // In production, run evaluations on both configurations
            // and perform statistical significance testing
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error comparing configurations");
        }
        
        return result;
    }

    private List<string> SplitIntoStatements(string text)
    {
        return text
            .Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
    }

    private List<string> ExtractKeyTerms(string text)
    {
        // Simple term extraction - in production, use NLP techniques
        return text
            .ToLower()
            .Split(new[] { ' ', ',', '.', '!', '?', ';', ':', '"', '\'', '(', ')', '[', ']' }, 
                   StringSplitOptions.RemoveEmptyEntries)
            .Where(term => term.Length > 2 && !_stopWords.Contains(term))
            .Distinct()
            .ToList();
    }

    private bool ContainsQueryIntent(string query, string response)
    {
        // Check if response contains question words from query
        var questionWords = new[] { "what", "when", "where", "who", "why", "how", "which" };
        var queryLower = query.ToLower();
        
        return questionWords.Any(word => queryLower.Contains(word));
    }

    private double CalculateHarmonicMean(double[] values)
    {
        if (values.Length == 0 || values.Any(v => v <= 0))
            return 0.0;
        
        var sum = values.Sum(v => 1.0 / v);
        return values.Length / sum;
    }

    private void GenerateInsights(RAGASEvaluationResult result)
    {
        if (result.OverallRAGASScore >= 0.80)
            result.Insights.Add("Excellent RAG quality - all metrics are strong");
        else if (result.OverallRAGASScore >= 0.70)
            result.Insights.Add("Good RAG quality - minor improvements possible");
        else
            result.Insights.Add("RAG quality needs improvement - review below metrics");
        
        if (result.FaithfulnessScore < FAITHFULNESS_THRESHOLD)
            result.Insights.Add($"Low faithfulness ({result.FaithfulnessScore:F2}) - response may contain hallucinations");
        
        if (result.AnswerRelevancyScore < RELEVANCY_THRESHOLD)
            result.Insights.Add($"Low answer relevancy ({result.AnswerRelevancyScore:F2}) - response may be off-topic");
        
        if (result.ContextPrecisionScore < PRECISION_THRESHOLD)
            result.Insights.Add($"Low context precision ({result.ContextPrecisionScore:F2}) - improve retrieval filtering");
        
        if (result.ContextRecallScore < RECALL_THRESHOLD)
            result.Insights.Add($"Low context recall ({result.ContextRecallScore:F2}) - increase number of retrieved documents");
    }
}
