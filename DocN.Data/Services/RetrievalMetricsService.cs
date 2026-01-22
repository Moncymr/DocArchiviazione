using DocN.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace DocN.Data.Services;

/// <summary>
/// Service for evaluating retrieval quality using standard IR metrics
/// </summary>
/// <remarks>
/// Implements standard Information Retrieval metrics:
/// - MRR (Mean Reciprocal Rank): Position of first relevant document
/// - NDCG (Normalized Discounted Cumulative Gain): Ranking quality with position discount
/// - Hit Rate: Presence of at least one relevant document in top K
/// - Precision@K: Fraction of relevant documents in top K
/// - Recall@K: Fraction of relevant documents retrieved in top K
/// </remarks>
public class RetrievalMetricsService : IRetrievalMetricsService
{
    private readonly ILogger<RetrievalMetricsService> _logger;
    private readonly ApplicationDbContext _context;
    private readonly IGoldenDatasetService _datasetService;
    private readonly ISemanticRAGService _ragService;

    public RetrievalMetricsService(
        ILogger<RetrievalMetricsService> logger,
        ApplicationDbContext context,
        IGoldenDatasetService datasetService,
        ISemanticRAGService ragService)
    {
        _logger = logger;
        _context = context;
        _datasetService = datasetService;
        _ragService = ragService;
    }

    /// <summary>
    /// Calculate Mean Reciprocal Rank
    /// MRR = 1 / rank_of_first_relevant_document
    /// </summary>
    public double CalculateMRR(List<int> retrievedDocIds, HashSet<int> relevantDocIds)
    {
        if (retrievedDocIds.Count == 0 || relevantDocIds.Count == 0)
            return 0.0;

        // Find position of first relevant document (1-indexed)
        for (int i = 0; i < retrievedDocIds.Count; i++)
        {
            if (relevantDocIds.Contains(retrievedDocIds[i]))
            {
                return 1.0 / (i + 1); // rank is 1-indexed
            }
        }

        return 0.0; // No relevant document found
    }

    /// <summary>
    /// Calculate Normalized Discounted Cumulative Gain
    /// NDCG = DCG / IDCG
    /// DCG = sum(rel_i / log2(i + 1)) for i in 1..k
    /// </summary>
    public double CalculateNDCG(List<int> retrievedDocIds, Dictionary<int, double> relevanceScores, int k = 10)
    {
        if (retrievedDocIds.Count == 0 || relevanceScores.Count == 0)
            return 0.0;

        var topK = retrievedDocIds.Take(k).ToList();

        // Calculate DCG
        var dcg = 0.0;
        for (int i = 0; i < topK.Count; i++)
        {
            var docId = topK[i];
            var relevance = relevanceScores.GetValueOrDefault(docId, 0.0);
            var position = i + 1; // 1-indexed
            dcg += relevance / Math.Log2(position + 1);
        }

        // Calculate IDCG (ideal DCG with perfect ranking)
        var idealRelevances = relevanceScores.Values
            .OrderByDescending(r => r)
            .Take(k)
            .ToList();

        var idcg = 0.0;
        for (int i = 0; i < idealRelevances.Count; i++)
        {
            var position = i + 1; // 1-indexed
            idcg += idealRelevances[i] / Math.Log2(position + 1);
        }

        if (idcg == 0.0)
            return 0.0;

        return dcg / idcg;
    }

    /// <summary>
    /// Calculate Hit Rate (binary: 1 if any relevant doc in top K, else 0)
    /// </summary>
    public double CalculateHitRate(List<int> retrievedDocIds, HashSet<int> relevantDocIds, int k = 10)
    {
        if (retrievedDocIds.Count == 0 || relevantDocIds.Count == 0)
            return 0.0;

        var topK = retrievedDocIds.Take(k);
        return topK.Any(docId => relevantDocIds.Contains(docId)) ? 1.0 : 0.0;
    }

    /// <summary>
    /// Calculate Precision@K = (relevant docs in top K) / K
    /// </summary>
    public double CalculatePrecisionAtK(List<int> retrievedDocIds, HashSet<int> relevantDocIds, int k = 10)
    {
        if (retrievedDocIds.Count == 0 || relevantDocIds.Count == 0)
            return 0.0;

        var topK = retrievedDocIds.Take(k).ToList();
        var relevantInTopK = topK.Count(docId => relevantDocIds.Contains(docId));

        return (double)relevantInTopK / Math.Min(k, topK.Count);
    }

    /// <summary>
    /// Calculate Recall@K = (relevant docs in top K) / (total relevant docs)
    /// </summary>
    public double CalculateRecallAtK(List<int> retrievedDocIds, HashSet<int> relevantDocIds, int k = 10)
    {
        if (retrievedDocIds.Count == 0 || relevantDocIds.Count == 0)
            return 0.0;

        var topK = retrievedDocIds.Take(k);
        var relevantInTopK = topK.Count(docId => relevantDocIds.Contains(docId));

        return (double)relevantInTopK / relevantDocIds.Count;
    }

    /// <summary>
    /// Evaluate retrieval quality on golden dataset
    /// </summary>
    public async Task<RetrievalEvaluationResult> EvaluateRetrievalQualityAsync(
        string datasetId,
        CancellationToken cancellationToken = default)
    {
        var result = new RetrievalEvaluationResult
        {
            EvaluatedAt = DateTime.UtcNow,
            ConfigurationName = "default",
            K = 10
        };

        try
        {
            _logger.LogInformation("Evaluating retrieval quality on dataset: {DatasetId}", datasetId);

            // Load dataset samples
            var dataset = await _datasetService.GetDatasetAsync(datasetId, cancellationToken);
            if (dataset == null)
            {
                throw new InvalidOperationException($"Dataset '{datasetId}' not found");
            }

            var samples = await _datasetService.GetSamplesAsync(datasetId, activeOnly: true, cancellationToken: cancellationToken);

            if (samples.Count == 0)
            {
                _logger.LogWarning("No active samples in dataset {DatasetId}", datasetId);
                return result;
            }

            var perQueryMetrics = new Dictionary<string, QueryMetrics>();
            var mrrScores = new List<double>();
            var ndcgScores = new List<double>();
            var hitRates = new List<double>();
            var precisionScores = new List<double>();
            var recallScores = new List<double>();

            foreach (var sample in samples)
            {
                try
                {
                    // For each query, perform retrieval and evaluate
                    // Note: In production, you would call your actual retrieval service
                    // For now, we'll use a placeholder implementation

                    // Parse expected document IDs from sample metadata
                    var relevantDocIds = ParseRelevantDocIds(sample);
                    if (relevantDocIds.Count == 0)
                    {
                        _logger.LogWarning("No relevant doc IDs for sample {SampleId}", sample.Id);
                        continue;
                    }

                    // Simulate retrieval (in production, call actual RAG service)
                    var retrievedDocIds = await SimulateRetrievalAsync(sample.Query, cancellationToken);

                    // Calculate metrics for this query
                    var queryMetric = new QueryMetrics
                    {
                        Query = sample.Query,
                        MRR = CalculateMRR(retrievedDocIds, relevantDocIds),
                        HitRate = CalculateHitRate(retrievedDocIds, relevantDocIds, result.K),
                        PrecisionAtK = CalculatePrecisionAtK(retrievedDocIds, relevantDocIds, result.K),
                        RecallAtK = CalculateRecallAtK(retrievedDocIds, relevantDocIds, result.K),
                        TotalRelevantDocs = relevantDocIds.Count,
                        RelevantDocsRetrieved = retrievedDocIds.Take(result.K).Count(id => relevantDocIds.Contains(id))
                    };

                    // Calculate NDCG (assume binary relevance: 1 if relevant, 0 otherwise)
                    var relevanceScores = relevantDocIds.ToDictionary(id => id, id => 1.0);
                    queryMetric.NDCG = CalculateNDCG(retrievedDocIds, relevanceScores, result.K);

                    perQueryMetrics[sample.Id.ToString()] = queryMetric;

                    mrrScores.Add(queryMetric.MRR);
                    ndcgScores.Add(queryMetric.NDCG);
                    hitRates.Add(queryMetric.HitRate);
                    precisionScores.Add(queryMetric.PrecisionAtK);
                    recallScores.Add(queryMetric.RecallAtK);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to evaluate sample {SampleId}", sample.Id);
                }
            }

            // Calculate average metrics
            result.TotalQueries = mrrScores.Count;
            if (result.TotalQueries > 0)
            {
                result.MRR = mrrScores.Average();
                result.NDCG = ndcgScores.Average();
                result.HitRate = hitRates.Average();
                result.PrecisionAtK = precisionScores.Average();
                result.RecallAtK = recallScores.Average();
            }

            result.PerQueryMetrics = perQueryMetrics;

            _logger.LogInformation(
                "Retrieval evaluation completed - MRR: {MRR:F3}, NDCG: {NDCG:F3}, Hit Rate: {HitRate:F3}",
                result.MRR, result.NDCG, result.HitRate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating retrieval quality");
            throw;
        }

        return result;
    }

    /// <summary>
    /// Compare retrieval configurations using A/B testing
    /// </summary>
    public async Task<RetrievalABTestResult> CompareRetrievalConfigurationsAsync(
        string configurationA,
        string configurationB,
        string datasetId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Comparing retrieval configurations: {ConfigA} vs {ConfigB}",
            configurationA, configurationB);

        var result = new RetrievalABTestResult
        {
            ConfigurationA = configurationA,
            ConfigurationB = configurationB
        };

        // In production, evaluate both configurations and compare
        // For now, return placeholder result
        result.ResultsA = await EvaluateRetrievalQualityAsync(datasetId, cancellationToken);
        result.ResultsA.ConfigurationName = configurationA;

        result.ResultsB = await EvaluateRetrievalQualityAsync(datasetId, cancellationToken);
        result.ResultsB.ConfigurationName = configurationB;

        // Calculate improvement percentages
        result.ImprovementPercentages["MRR"] = CalculateImprovement(result.ResultsA.MRR, result.ResultsB.MRR);
        result.ImprovementPercentages["NDCG"] = CalculateImprovement(result.ResultsA.NDCG, result.ResultsB.NDCG);
        result.ImprovementPercentages["HitRate"] = CalculateImprovement(result.ResultsA.HitRate, result.ResultsB.HitRate);

        // Determine winner (simple comparison based on MRR)
        if (result.ResultsB.MRR > result.ResultsA.MRR * 1.05) // 5% improvement threshold
        {
            result.Winner = configurationB;
            result.IsStatisticallySignificant = true;
        }
        else if (result.ResultsA.MRR > result.ResultsB.MRR * 1.05)
        {
            result.Winner = configurationA;
            result.IsStatisticallySignificant = true;
        }
        else
        {
            result.Winner = "tie";
            result.IsStatisticallySignificant = false;
        }

        return result;
    }

    private double CalculateImprovement(double baseline, double current)
    {
        if (baseline == 0)
            return current > 0 ? 100.0 : 0.0;

        return ((current - baseline) / baseline) * 100.0;
    }

    private HashSet<int> ParseRelevantDocIds(GoldenDatasetSampleDto sample)
    {
        // Try to extract relevant doc IDs from metadata or ground truth
        // This is a placeholder - implement based on your dataset structure
        var relevantIds = new HashSet<int>();

        // Example: parse from metadata JSON if available
        // For now, return empty set (to be implemented based on your dataset format)

        return relevantIds;
    }

    private async Task<List<int>> SimulateRetrievalAsync(string query, CancellationToken cancellationToken)
    {
        // Placeholder: In production, call your actual retrieval service
        // For now, return some document IDs
        var documents = await _context.Documents
            .Where(d => d.ExtractedText.Contains(query))
            .Take(10)
            .Select(d => d.Id)
            .ToListAsync(cancellationToken);

        return documents;
    }
}
