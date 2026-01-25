using Microsoft.AspNetCore.Mvc;
using DocN.Data.Services;
using Microsoft.AspNetCore.RateLimiting;

namespace DocN.Server.Controllers;

/// <summary>
/// Controller for retrieval quality metrics calculation
/// </summary>
[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("api")]
public class RetrievalMetricsController : ControllerBase
{
    private readonly IRetrievalMetricsService _metricsService;
    private readonly ILogger<RetrievalMetricsController> _logger;

    public RetrievalMetricsController(
        IRetrievalMetricsService metricsService,
        ILogger<RetrievalMetricsController> logger)
    {
        _metricsService = metricsService;
        _logger = logger;
    }

    /// <summary>
    /// Calculate all retrieval metrics for a set of results
    /// </summary>
    [HttpPost("calculate")]
    public IActionResult CalculateMetrics([FromBody] CalculateMetricsRequest request)
    {
        try
        {
            var metrics = _metricsService.CalculateAllMetrics(request.Results, request.TotalRelevant);
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating retrieval metrics");
            return StatusCode(500, new { error = "Failed to calculate retrieval metrics" });
        }
    }

    /// <summary>
    /// Calculate Mean Reciprocal Rank (MRR)
    /// </summary>
    [HttpPost("mrr")]
    public IActionResult CalculateMRR([FromBody] List<RetrievalResult> results)
    {
        try
        {
            var mrr = _metricsService.CalculateMRR(results);
            return Ok(new { mrr, description = "Mean Reciprocal Rank - measures how quickly relevant results appear" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating MRR");
            return StatusCode(500, new { error = "Failed to calculate MRR" });
        }
    }

    /// <summary>
    /// Calculate Normalized Discounted Cumulative Gain (NDCG)
    /// </summary>
    [HttpPost("ndcg")]
    public IActionResult CalculateNDCG(
        [FromBody] List<RetrievalResult> results,
        [FromQuery] int k = 10)
    {
        try
        {
            var ndcg = _metricsService.CalculateNDCG(results, k);
            return Ok(new { ndcg, k, description = $"NDCG@{k} - measures ranking quality considering relevance scores" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating NDCG");
            return StatusCode(500, new { error = "Failed to calculate NDCG" });
        }
    }

    /// <summary>
    /// Calculate Precision at K
    /// </summary>
    [HttpPost("precision")]
    public IActionResult CalculatePrecision(
        [FromBody] List<RetrievalResult> results,
        [FromQuery] int k = 10)
    {
        try
        {
            var precision = _metricsService.CalculatePrecisionAtK(results, k);
            return Ok(new { precision, k, description = $"Precision@{k} - relevant docs in top {k} results" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating Precision");
            return StatusCode(500, new { error = "Failed to calculate Precision" });
        }
    }

    /// <summary>
    /// Calculate Recall at K
    /// </summary>
    [HttpPost("recall")]
    public IActionResult CalculateRecall(
        [FromBody] RecallRequest request)
    {
        try
        {
            var recall = _metricsService.CalculateRecallAtK(request.Results, request.K, request.TotalRelevant);
            return Ok(new { recall, k = request.K, description = $"Recall@{request.K} - coverage of relevant docs" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating Recall");
            return StatusCode(500, new { error = "Failed to calculate Recall" });
        }
    }

    /// <summary>
    /// Calculate F1 score at K
    /// </summary>
    [HttpPost("f1")]
    public IActionResult CalculateF1(
        [FromBody] RecallRequest request)
    {
        try
        {
            var f1 = _metricsService.CalculateF1AtK(request.Results, request.K, request.TotalRelevant);
            return Ok(new { f1, k = request.K, description = $"F1@{request.K} - harmonic mean of precision and recall" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating F1");
            return StatusCode(500, new { error = "Failed to calculate F1" });
        }
    }

    /// <summary>
    /// Get metrics summary with common K values
    /// </summary>
    [HttpPost("summary")]
    public IActionResult GetMetricsSummary([FromBody] CalculateMetricsRequest request)
    {
        try
        {
            var metrics = _metricsService.CalculateAllMetrics(request.Results, request.TotalRelevant);
            
            var summary = new
            {
                meanReciprocalRank = metrics.MRR,
                ndcg = new
                {
                    at5 = metrics.NDCG_5,
                    at10 = metrics.NDCG_10
                },
                precision = new
                {
                    at5 = metrics.Precision_5,
                    at10 = metrics.Precision_10
                },
                recall = new
                {
                    at5 = metrics.Recall_5,
                    at10 = metrics.Recall_10
                },
                f1 = new
                {
                    at5 = metrics.F1_5,
                    at10 = metrics.F1_10
                },
                totalResults = metrics.TotalResults,
                totalRelevant = metrics.TotalRelevant,
                measuredAt = metrics.MeasuredAt
            };
            
            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting metrics summary");
            return StatusCode(500, new { error = "Failed to get metrics summary" });
        }
    }
}

public record CalculateMetricsRequest(
    List<RetrievalResult> Results,
    int TotalRelevant);

public record RecallRequest(
    List<RetrievalResult> Results,
    int K,
    int TotalRelevant);
