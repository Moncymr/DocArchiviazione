using DocN.Core.Interfaces;
using DocN.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;

namespace DocN.Data.Services;

/// <summary>
/// Implementation of retrieval metrics service for evaluating RAG retrieval quality
/// </summary>
public class RetrievalMetricsService : IRetrievalMetricsService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<RetrievalMetricsService> _logger;
    private readonly IEmbeddingService _embeddingService;

    public RetrievalMetricsService(
        ApplicationDbContext context,
        ILogger<RetrievalMetricsService> logger,
        IEmbeddingService embeddingService)
    {
        _context = context;
        _logger = logger;
        _embeddingService = embeddingService;
    }

    /// <summary>
    /// Calculate Mean Reciprocal Rank (MRR)
    /// MRR = (1/Q) * Σ(1/rank_i) where rank_i is the position of the first relevant result
    /// </summary>
    public double CalculateMRR(List<List<int>> retrievedIds, List<List<int>> relevantIds)
    {
        if (retrievedIds.Count != relevantIds.Count || retrievedIds.Count == 0)
        {
            _logger.LogWarning("Invalid input for MRR calculation");
            return 0.0;
        }

        double sumReciprocalRanks = 0.0;
        int validQueries = 0;

        for (int i = 0; i < retrievedIds.Count; i++)
        {
            var retrieved = retrievedIds[i];
            var relevant = new HashSet<int>(relevantIds[i]);

            if (relevant.Count == 0)
                continue;

            // Find the rank of the first relevant document (1-based)
            for (int rank = 0; rank < retrieved.Count; rank++)
            {
                if (relevant.Contains(retrieved[rank]))
                {
                    sumReciprocalRanks += 1.0 / (rank + 1);
                    validQueries++;
                    break;
                }
            }
        }

        return validQueries > 0 ? sumReciprocalRanks / validQueries : 0.0;
    }

    /// <summary>
    /// Calculate Normalized Discounted Cumulative Gain (NDCG)
    /// NDCG = DCG / IDCG where DCG = Σ(rel_i / log2(i+1))
    /// </summary>
    public double CalculateNDCG(List<int> retrievedIds, Dictionary<int, double> relevanceScores, int k = 10)
    {
        if (retrievedIds.Count == 0 || relevanceScores.Count == 0)
            return 0.0;

        // Calculate DCG
        double dcg = 0.0;
        int limit = Math.Min(k, retrievedIds.Count);
        
        for (int i = 0; i < limit; i++)
        {
            int docId = retrievedIds[i];
            double relevance = relevanceScores.ContainsKey(docId) ? relevanceScores[docId] : 0.0;
            dcg += relevance / Math.Log2(i + 2); // i+2 because log2(1) is 0
        }

        // Calculate IDCG (Ideal DCG with perfect ranking)
        var idealRelevances = relevanceScores.Values.OrderByDescending(x => x).Take(k).ToList();
        double idcg = 0.0;
        
        for (int i = 0; i < idealRelevances.Count; i++)
        {
            idcg += idealRelevances[i] / Math.Log2(i + 2);
        }

        return idcg > 0 ? dcg / idcg : 0.0;
    }

    /// <summary>
    /// Calculate Mean Average Precision (MAP)
    /// MAP = (1/Q) * Σ AP_i where AP_i is the average precision for query i
    /// </summary>
    public double CalculateMAP(List<List<int>> retrievedIds, List<List<int>> relevantIds)
    {
        if (retrievedIds.Count != relevantIds.Count || retrievedIds.Count == 0)
            return 0.0;

        double sumAP = 0.0;
        int validQueries = 0;

        for (int i = 0; i < retrievedIds.Count; i++)
        {
            var retrieved = retrievedIds[i];
            var relevant = new HashSet<int>(relevantIds[i]);

            if (relevant.Count == 0)
                continue;

            double ap = CalculateAveragePrecision(retrieved, relevant);
            sumAP += ap;
            validQueries++;
        }

        return validQueries > 0 ? sumAP / validQueries : 0.0;
    }

    /// <summary>
    /// Calculate Average Precision for a single query
    /// </summary>
    private double CalculateAveragePrecision(List<int> retrieved, HashSet<int> relevant)
    {
        if (relevant.Count == 0)
            return 0.0;

        double sumPrecisions = 0.0;
        int relevantFound = 0;

        for (int i = 0; i < retrieved.Count; i++)
        {
            if (relevant.Contains(retrieved[i]))
            {
                relevantFound++;
                double precision = (double)relevantFound / (i + 1);
                sumPrecisions += precision;
            }
        }

        return relevantFound > 0 ? sumPrecisions / relevant.Count : 0.0;
    }

    /// <summary>
    /// Calculate Precision at k
    /// Precision@k = (relevant retrieved in top-k) / k
    /// </summary>
    public double CalculatePrecisionAtK(List<int> retrievedIds, List<int> relevantIds, int k)
    {
        if (k <= 0 || retrievedIds.Count == 0)
            return 0.0;

        var relevant = new HashSet<int>(relevantIds);
        int topK = Math.Min(k, retrievedIds.Count);
        int relevantCount = 0;

        for (int i = 0; i < topK; i++)
        {
            if (relevant.Contains(retrievedIds[i]))
                relevantCount++;
        }

        return (double)relevantCount / topK;
    }

    /// <summary>
    /// Calculate Recall at k
    /// Recall@k = (relevant retrieved in top-k) / (total relevant)
    /// </summary>
    public double CalculateRecallAtK(List<int> retrievedIds, List<int> relevantIds, int k)
    {
        if (relevantIds.Count == 0 || retrievedIds.Count == 0)
            return 0.0;

        var relevant = new HashSet<int>(relevantIds);
        int topK = Math.Min(k, retrievedIds.Count);
        int relevantCount = 0;

        for (int i = 0; i < topK; i++)
        {
            if (relevant.Contains(retrievedIds[i]))
                relevantCount++;
        }

        return (double)relevantCount / relevant.Count;
    }

    /// <summary>
    /// Run a complete evaluation on an evaluation dataset
    /// </summary>
    public async Task<EvaluationMetricsResult> EvaluateRetrievalAsync(
        string evaluationName,
        List<EvaluationQuery> queries,
        string modelVersion,
        string? chunkingStrategy = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting evaluation: {EvaluationName} with {QueryCount} queries", 
            evaluationName, queries.Count);

        var stopwatch = Stopwatch.StartNew();
        var result = new EvaluationMetricsResult
        {
            EvaluationName = evaluationName,
            ModelVersion = modelVersion,
            ChunkingStrategy = chunkingStrategy,
            TotalQueries = queries.Count,
            CreatedAt = DateTime.UtcNow
        };

        var allRetrievedIds = new List<List<int>>();
        var allRelevantIds = new List<List<int>>();
        var queryDetails = new Dictionary<int, QueryMetrics>();

        foreach (var query in queries)
        {
            try
            {
                // Generate embedding for query
                var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query.QueryText);
                if (queryEmbedding == null)
                {
                    _logger.LogWarning("Failed to generate embedding for query {QueryId}", query.QueryId);
                    continue;
                }

                // Search for similar documents
                var retrievedDocs = await _embeddingService.SearchSimilarDocumentsAsync(queryEmbedding, 10);
                var retrievedIds = retrievedDocs.Select(d => d.Id).ToList();
                
                allRetrievedIds.Add(retrievedIds);
                allRelevantIds.Add(query.RelevantDocumentIds);

                // Calculate per-query metrics
                // TODO: Enhance SearchSimilarDocumentsAsync to return actual similarity scores
                // Currently using simplified score of 1.0 for all retrieved documents
                var queryMetric = new QueryMetrics
                {
                    QueryId = query.QueryId,
                    RetrievedIds = retrievedIds,
                    RetrievalScores = retrievedDocs.ToDictionary(d => d.Id, d => 1.0),
                };

                // Calculate reciprocal rank
                var relevant = new HashSet<int>(query.RelevantDocumentIds);
                for (int i = 0; i < retrievedIds.Count; i++)
                {
                    if (relevant.Contains(retrievedIds[i]))
                    {
                        queryMetric.ReciprocalRank = 1.0 / (i + 1);
                        queryMetric.FirstRelevantRank = i + 1;
                        queryMetric.FirstResultRelevant = (i == 0);
                        break;
                    }
                }

                // Calculate NDCG for this query
                if (query.RelevanceScores != null)
                {
                    queryMetric.NDCG = CalculateNDCG(retrievedIds, query.RelevanceScores, 10);
                }

                // Calculate Average Precision
                queryMetric.AveragePrecision = CalculateAveragePrecision(retrievedIds, relevant);

                queryDetails[query.QueryId] = queryMetric;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluating query {QueryId}", query.QueryId);
            }
        }

        // Calculate aggregate metrics
        result.MRRScore = CalculateMRR(allRetrievedIds, allRelevantIds);
        result.MAP = CalculateMAP(allRetrievedIds, allRelevantIds);

        // Calculate precision and recall at different k values
        double sumP1 = 0, sumP5 = 0, sumR5 = 0, sumR10 = 0, sumNDCG5 = 0, sumNDCG10 = 0;
        int validCount = 0;

        for (int i = 0; i < allRetrievedIds.Count; i++)
        {
            if (allRelevantIds[i].Count == 0)
                continue;

            sumP1 += CalculatePrecisionAtK(allRetrievedIds[i], allRelevantIds[i], 1);
            sumP5 += CalculatePrecisionAtK(allRetrievedIds[i], allRelevantIds[i], 5);
            sumR5 += CalculateRecallAtK(allRetrievedIds[i], allRelevantIds[i], 5);
            sumR10 += CalculateRecallAtK(allRetrievedIds[i], allRelevantIds[i], 10);

            // Calculate NDCG if relevance scores available
            if (queries[i].RelevanceScores != null)
            {
                sumNDCG5 += CalculateNDCG(allRetrievedIds[i], queries[i].RelevanceScores, 5);
                sumNDCG10 += CalculateNDCG(allRetrievedIds[i], queries[i].RelevanceScores, 10);
            }

            validCount++;
        }

        if (validCount > 0)
        {
            result.Precision_at_1 = sumP1 / validCount;
            result.Precision_at_5 = sumP5 / validCount;
            result.Recall_at_5 = sumR5 / validCount;
            result.Recall_at_10 = sumR10 / validCount;
            result.NDCG_at_5 = sumNDCG5 / validCount;
            result.NDCG_at_10 = sumNDCG10 / validCount;
        }

        result.QueryDetails = queryDetails;
        stopwatch.Stop();
        result.DurationMs = stopwatch.ElapsedMilliseconds;

        // Save to database
        var dbResult = new RetrievalEvaluationResult
        {
            EvaluationName = result.EvaluationName,
            ConfigurationJson = JsonSerializer.Serialize(new { modelVersion, chunkingStrategy }),
            ModelVersion = result.ModelVersion,
            ChunkingStrategy = result.ChunkingStrategy,
            TotalQueries = result.TotalQueries,
            MRRScore = result.MRRScore,
            NDCG_at_5 = result.NDCG_at_5,
            NDCG_at_10 = result.NDCG_at_10,
            Precision_at_1 = result.Precision_at_1,
            Precision_at_5 = result.Precision_at_5,
            Recall_at_5 = result.Recall_at_5,
            Recall_at_10 = result.Recall_at_10,
            MAP = result.MAP,
            DetailedResultsJson = JsonSerializer.Serialize(queryDetails),
            CreatedAt = result.CreatedAt,
            DurationMs = result.DurationMs
        };

        _context.Set<RetrievalEvaluationResult>().Add(dbResult);
        await _context.SaveChangesAsync(cancellationToken);
        result.EvaluationId = dbResult.Id;

        _logger.LogInformation("Evaluation completed: MRR={MRR:F3}, MAP={MAP:F3}, NDCG@10={NDCG:F3}", 
            result.MRRScore, result.MAP, result.NDCG_at_10);

        return result;
    }

    /// <summary>
    /// Get historical evaluation results
    /// </summary>
    public async Task<List<EvaluationMetricsResult>> GetEvaluationHistoryAsync(
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Set<RetrievalEvaluationResult>().AsQueryable();

        if (from.HasValue)
            query = query.Where(e => e.CreatedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(e => e.CreatedAt <= to.Value);

        var results = await query
            .OrderByDescending(e => e.CreatedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        return results.Select(r => new EvaluationMetricsResult
        {
            EvaluationId = r.Id,
            EvaluationName = r.EvaluationName,
            ModelVersion = r.ModelVersion,
            ChunkingStrategy = r.ChunkingStrategy,
            TotalQueries = r.TotalQueries,
            MRRScore = r.MRRScore,
            NDCG_at_5 = r.NDCG_at_5,
            NDCG_at_10 = r.NDCG_at_10,
            Precision_at_1 = r.Precision_at_1,
            Precision_at_5 = r.Precision_at_5,
            Recall_at_5 = r.Recall_at_5,
            Recall_at_10 = r.Recall_at_10,
            MAP = r.MAP,
            CreatedAt = r.CreatedAt,
            DurationMs = r.DurationMs ?? 0
        }).ToList();
    }

    /// <summary>
    /// Compare two evaluation results
    /// </summary>
    public async Task<EvaluationComparison> CompareEvaluationsAsync(
        int evaluationId1,
        int evaluationId2,
        CancellationToken cancellationToken = default)
    {
        var eval1 = await _context.Set<RetrievalEvaluationResult>()
            .FirstOrDefaultAsync(e => e.Id == evaluationId1, cancellationToken);
        var eval2 = await _context.Set<RetrievalEvaluationResult>()
            .FirstOrDefaultAsync(e => e.Id == evaluationId2, cancellationToken);

        if (eval1 == null || eval2 == null)
            throw new ArgumentException("One or both evaluations not found");

        var result1 = MapToMetricsResult(eval1);
        var result2 = MapToMetricsResult(eval2);

        var comparison = new EvaluationComparison
        {
            Evaluation1 = result1,
            Evaluation2 = result2
        };

        // Calculate improvements
        comparison.Improvements["MRR"] = result2.MRRScore - result1.MRRScore;
        comparison.Improvements["MAP"] = result2.MAP - result1.MAP;
        comparison.Improvements["NDCG@5"] = result2.NDCG_at_5 - result1.NDCG_at_5;
        comparison.Improvements["NDCG@10"] = result2.NDCG_at_10 - result1.NDCG_at_10;
        comparison.Improvements["P@1"] = result2.Precision_at_1 - result1.Precision_at_1;
        comparison.Improvements["P@5"] = result2.Precision_at_5 - result1.Precision_at_5;

        // Calculate percentage changes
        foreach (var key in comparison.Improvements.Keys)
        {
            var baseValue = GetMetricValue(result1, key);
            if (baseValue > 0)
            {
                comparison.PercentageChanges[key] = 
                    (comparison.Improvements[key] / baseValue) * 100;
            }
        }

        // Determine if it's an improvement
        comparison.IsImprovement = comparison.Improvements.Values.Average() > 0;

        // Generate summary
        comparison.Summary = GenerateComparisonSummary(comparison);

        return comparison;
    }

    private EvaluationMetricsResult MapToMetricsResult(RetrievalEvaluationResult dbResult)
    {
        return new EvaluationMetricsResult
        {
            EvaluationId = dbResult.Id,
            EvaluationName = dbResult.EvaluationName,
            ModelVersion = dbResult.ModelVersion,
            ChunkingStrategy = dbResult.ChunkingStrategy,
            TotalQueries = dbResult.TotalQueries,
            MRRScore = dbResult.MRRScore,
            NDCG_at_5 = dbResult.NDCG_at_5,
            NDCG_at_10 = dbResult.NDCG_at_10,
            Precision_at_1 = dbResult.Precision_at_1,
            Precision_at_5 = dbResult.Precision_at_5,
            Recall_at_5 = dbResult.Recall_at_5,
            Recall_at_10 = dbResult.Recall_at_10,
            MAP = dbResult.MAP,
            CreatedAt = dbResult.CreatedAt,
            DurationMs = dbResult.DurationMs ?? 0
        };
    }

    private double GetMetricValue(EvaluationMetricsResult result, string metricName)
    {
        return metricName switch
        {
            "MRR" => result.MRRScore,
            "MAP" => result.MAP,
            "NDCG@5" => result.NDCG_at_5,
            "NDCG@10" => result.NDCG_at_10,
            "P@1" => result.Precision_at_1,
            "P@5" => result.Precision_at_5,
            _ => 0.0
        };
    }

    private string GenerateComparisonSummary(EvaluationComparison comparison)
    {
        var improvements = comparison.Improvements
            .Where(kvp => kvp.Value > 0)
            .OrderByDescending(kvp => Math.Abs(kvp.Value))
            .ToList();

        var degradations = comparison.Improvements
            .Where(kvp => kvp.Value < 0)
            .OrderBy(kvp => kvp.Value)
            .ToList();

        var summary = $"Comparison: {comparison.Evaluation1.EvaluationName} vs {comparison.Evaluation2.EvaluationName}\n";
        
        if (improvements.Any())
        {
            summary += "Improvements:\n";
            foreach (var imp in improvements)
            {
                summary += $"  {imp.Key}: +{imp.Value:F4} ({comparison.PercentageChanges[imp.Key]:F1}%)\n";
            }
        }

        if (degradations.Any())
        {
            summary += "Degradations:\n";
            foreach (var deg in degradations)
            {
                summary += $"  {deg.Key}: {deg.Value:F4} ({comparison.PercentageChanges[deg.Key]:F1}%)\n";
            }
        }

        return summary;
    }
}
