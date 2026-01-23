using DocN.Data.Models;

namespace DocN.Data.Services;

/// <summary>
/// Service for calculating retrieval quality metrics
/// </summary>
public interface IRetrievalMetricsService
{
    /// <summary>
    /// Calculate Mean Reciprocal Rank (MRR)
    /// MRR measures how quickly relevant results appear
    /// </summary>
    double CalculateMRR(List<RetrievalResult> results);

    /// <summary>
    /// Calculate Normalized Discounted Cumulative Gain (NDCG)
    /// NDCG measures ranking quality considering relevance scores
    /// </summary>
    double CalculateNDCG(List<RetrievalResult> results, int k);

    /// <summary>
    /// Calculate Precision at K
    /// Precision@K = (relevant docs in top K) / K
    /// </summary>
    double CalculatePrecisionAtK(List<RetrievalResult> results, int k);

    /// <summary>
    /// Calculate Recall at K
    /// Recall@K = (relevant docs in top K) / (total relevant docs)
    /// </summary>
    double CalculateRecallAtK(List<RetrievalResult> results, int k, int totalRelevant);

    /// <summary>
    /// Calculate F1 score at K
    /// F1 = 2 * (Precision * Recall) / (Precision + Recall)
    /// </summary>
    double CalculateF1AtK(List<RetrievalResult> results, int k, int totalRelevant);

    /// <summary>
    /// Calculate all metrics for a query
    /// </summary>
    RetrievalMetrics CalculateAllMetrics(List<RetrievalResult> results, int totalRelevant);
}

/// <summary>
/// Represents a retrieval result with relevance information
/// </summary>
public class RetrievalResult
{
    public int DocumentId { get; set; }
    public int Rank { get; set; }
    public double Score { get; set; }
    public bool IsRelevant { get; set; }
    public int RelevanceGrade { get; set; } // 0-3 scale: 0=not relevant, 1=marginally, 2=relevant, 3=highly relevant
}

/// <summary>
/// Comprehensive retrieval metrics
/// </summary>
public class RetrievalMetrics
{
    public double MRR { get; set; }
    public double NDCG_5 { get; set; }
    public double NDCG_10 { get; set; }
    public double Precision_5 { get; set; }
    public double Precision_10 { get; set; }
    public double Recall_5 { get; set; }
    public double Recall_10 { get; set; }
    public double F1_5 { get; set; }
    public double F1_10 { get; set; }
    public int TotalResults { get; set; }
    public int TotalRelevant { get; set; }
    public DateTime MeasuredAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Implementation of retrieval metrics calculations
/// </summary>
public class RetrievalMetricsService : IRetrievalMetricsService
{
    /// <summary>
    /// Calculate Mean Reciprocal Rank (MRR)
    /// MRR = 1 / rank of first relevant result
    /// </summary>
    public double CalculateMRR(List<RetrievalResult> results)
    {
        if (results == null || !results.Any())
            return 0.0;

        // Find rank of first relevant result
        var firstRelevant = results
            .Where(r => r.IsRelevant)
            .OrderBy(r => r.Rank)
            .FirstOrDefault();

        if (firstRelevant == null)
            return 0.0;

        return 1.0 / firstRelevant.Rank;
    }

    /// <summary>
    /// Calculate Normalized Discounted Cumulative Gain (NDCG@k)
    /// NDCG considers both relevance grades and ranking positions
    /// </summary>
    public double CalculateNDCG(List<RetrievalResult> results, int k)
    {
        if (results == null || !results.Any() || k <= 0)
            return 0.0;

        var topK = results.OrderBy(r => r.Rank).Take(k).ToList();

        // Calculate DCG (Discounted Cumulative Gain)
        double dcg = 0.0;
        for (int i = 0; i < topK.Count; i++)
        {
            var rank = i + 1;
            var relevance = topK[i].RelevanceGrade;
            
            // DCG formula: sum((2^rel - 1) / log2(rank + 1))
            dcg += (Math.Pow(2, relevance) - 1) / Math.Log(rank + 1, 2);
        }

        // Calculate IDCG (Ideal DCG) - DCG if results were perfectly sorted by relevance
        var idealOrder = results.OrderByDescending(r => r.RelevanceGrade).Take(k).ToList();
        double idcg = 0.0;
        for (int i = 0; i < idealOrder.Count; i++)
        {
            var rank = i + 1;
            var relevance = idealOrder[i].RelevanceGrade;
            idcg += (Math.Pow(2, relevance) - 1) / Math.Log(rank + 1, 2);
        }

        // NDCG = DCG / IDCG
        return idcg > 0 ? dcg / idcg : 0.0;
    }

    /// <summary>
    /// Calculate Precision at K
    /// Precision@K = (number of relevant docs in top K) / K
    /// </summary>
    public double CalculatePrecisionAtK(List<RetrievalResult> results, int k)
    {
        if (results == null || !results.Any() || k <= 0)
            return 0.0;

        var topK = results.OrderBy(r => r.Rank).Take(k).ToList();
        var relevantCount = topK.Count(r => r.IsRelevant);

        return (double)relevantCount / k;
    }

    /// <summary>
    /// Calculate Recall at K
    /// Recall@K = (number of relevant docs in top K) / (total number of relevant docs)
    /// </summary>
    public double CalculateRecallAtK(List<RetrievalResult> results, int k, int totalRelevant)
    {
        if (results == null || !results.Any() || k <= 0 || totalRelevant <= 0)
            return 0.0;

        var topK = results.OrderBy(r => r.Rank).Take(k).ToList();
        var relevantCount = topK.Count(r => r.IsRelevant);

        return (double)relevantCount / totalRelevant;
    }

    /// <summary>
    /// Calculate F1 score at K
    /// F1 = 2 * (Precision * Recall) / (Precision + Recall)
    /// </summary>
    public double CalculateF1AtK(List<RetrievalResult> results, int k, int totalRelevant)
    {
        var precision = CalculatePrecisionAtK(results, k);
        var recall = CalculateRecallAtK(results, k, totalRelevant);

        if (precision + recall == 0)
            return 0.0;

        return 2 * (precision * recall) / (precision + recall);
    }

    /// <summary>
    /// Calculate all metrics for a query
    /// </summary>
    public RetrievalMetrics CalculateAllMetrics(List<RetrievalResult> results, int totalRelevant)
    {
        return new RetrievalMetrics
        {
            MRR = CalculateMRR(results),
            NDCG_5 = CalculateNDCG(results, 5),
            NDCG_10 = CalculateNDCG(results, 10),
            Precision_5 = CalculatePrecisionAtK(results, 5),
            Precision_10 = CalculatePrecisionAtK(results, 10),
            Recall_5 = CalculateRecallAtK(results, 5, totalRelevant),
            Recall_10 = CalculateRecallAtK(results, 10, totalRelevant),
            F1_5 = CalculateF1AtK(results, 5, totalRelevant),
            F1_10 = CalculateF1AtK(results, 10, totalRelevant),
            TotalResults = results.Count,
            TotalRelevant = totalRelevant
        };
    }
}
