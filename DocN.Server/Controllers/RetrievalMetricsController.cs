using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using DocN.Core.Interfaces;

namespace DocN.Server.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
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
    /// Run evaluation on a set of queries
    /// </summary>
    [HttpPost("evaluate")]
    public async Task<ActionResult<EvaluationMetricsResult>> EvaluateRetrieval(
        [FromBody] EvaluationRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Running evaluation: {EvaluationName} with {QueryCount} queries", 
                request.EvaluationName, request.Queries.Count);

            var result = await _metricsService.EvaluateRetrievalAsync(
                request.EvaluationName,
                request.Queries,
                request.ModelVersion,
                request.ChunkingStrategy,
                cancellationToken);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running evaluation: {EvaluationName}", request.EvaluationName);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get evaluation history
    /// </summary>
    [HttpGet("history")]
    public async Task<ActionResult<List<EvaluationMetricsResult>>> GetEvaluationHistory(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var results = await _metricsService.GetEvaluationHistoryAsync(from, to, cancellationToken);
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting evaluation history");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Compare two evaluations
    /// </summary>
    [HttpGet("compare")]
    public async Task<ActionResult<EvaluationComparison>> CompareEvaluations(
        [FromQuery] int evaluationId1,
        [FromQuery] int evaluationId2,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var comparison = await _metricsService.CompareEvaluationsAsync(
                evaluationId1, evaluationId2, cancellationToken);
            return Ok(comparison);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error comparing evaluations {Id1} and {Id2}", 
                evaluationId1, evaluationId2);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Calculate MRR for provided results
    /// </summary>
    [HttpPost("calculate-mrr")]
    public ActionResult<double> CalculateMRR([FromBody] MetricsCalculationRequest request)
    {
        try
        {
            var mrr = _metricsService.CalculateMRR(request.RetrievedIds, request.RelevantIds);
            return Ok(new { mrr });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating MRR");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Calculate NDCG for provided results
    /// </summary>
    [HttpPost("calculate-ndcg")]
    public ActionResult<double> CalculateNDCG([FromBody] NDCGCalculationRequest request)
    {
        try
        {
            var ndcg = _metricsService.CalculateNDCG(
                request.RetrievedIds, 
                request.RelevanceScores, 
                request.K);
            return Ok(new { ndcg, k = request.K });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating NDCG");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

/// <summary>
/// Request model for evaluation
/// </summary>
public class EvaluationRequest
{
    public string EvaluationName { get; set; } = string.Empty;
    public List<EvaluationQuery> Queries { get; set; } = new();
    public string ModelVersion { get; set; } = string.Empty;
    public string? ChunkingStrategy { get; set; }
}

/// <summary>
/// Request for metrics calculation
/// </summary>
public class MetricsCalculationRequest
{
    public List<List<int>> RetrievedIds { get; set; } = new();
    public List<List<int>> RelevantIds { get; set; } = new();
}

/// <summary>
/// Request for NDCG calculation
/// </summary>
public class NDCGCalculationRequest
{
    public List<int> RetrievedIds { get; set; } = new();
    public Dictionary<int, double> RelevanceScores { get; set; } = new();
    public int K { get; set; } = 10;
}
