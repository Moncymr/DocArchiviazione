using Microsoft.AspNetCore.Mvc;
using DocN.Core.Interfaces;
using Microsoft.AspNetCore.RateLimiting;

namespace DocN.Server.Controllers;

/// <summary>
/// Controller for RAG quality verification and metrics
/// </summary>
[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("api")]
public class RAGQualityController : ControllerBase
{
    private readonly IRAGQualityService _qualityService;
    private readonly IRAGASMetricsService _ragasService;
    private readonly ILogger<RAGQualityController> _logger;

    public RAGQualityController(
        IRAGQualityService qualityService,
        IRAGASMetricsService ragasService,
        ILogger<RAGQualityController> logger)
    {
        _qualityService = qualityService;
        _ragasService = ragasService;
        _logger = logger;
    }

    /// <summary>
    /// Verify the quality of a RAG response
    /// </summary>
    [HttpPost("verify")]
    [EnableRateLimiting("ai")]
    public async Task<IActionResult> VerifyQuality(
        [FromBody] VerifyQualityRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _qualityService.VerifyResponseQualityAsync(
                request.Query,
                request.Response,
                request.SourceDocumentIds,
                cancellationToken);
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying RAG quality");
            return StatusCode(500, new { error = "Failed to verify quality" });
        }
    }

    /// <summary>
    /// Detect hallucinations in a response
    /// </summary>
    [HttpPost("hallucinations")]
    [EnableRateLimiting("ai")]
    public async Task<IActionResult> DetectHallucinations(
        [FromBody] HallucinationDetectionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _qualityService.DetectHallucinationsAsync(
                request.Response,
                request.SourceTexts,
                cancellationToken);
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting hallucinations");
            return StatusCode(500, new { error = "Failed to detect hallucinations" });
        }
    }

    /// <summary>
    /// Get quality metrics for a time period
    /// </summary>
    [HttpGet("metrics")]
    public async Task<IActionResult> GetQualityMetrics(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        try
        {
            var metrics = await _qualityService.GetQualityMetricsAsync(from, to, cancellationToken);
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting quality metrics");
            return StatusCode(500, new { error = "Failed to retrieve quality metrics" });
        }
    }

    /// <summary>
    /// Evaluate response using RAGAS metrics
    /// </summary>
    [HttpPost("ragas/evaluate")]
    [EnableRateLimiting("ai")]
    public async Task<IActionResult> EvaluateRAGAS(
        [FromBody] RAGASEvaluationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _ragasService.EvaluateResponseAsync(
                request.Query,
                request.Response,
                request.Contexts,
                request.GroundTruth,
                cancellationToken);
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating RAGAS metrics");
            return StatusCode(500, new { error = "Failed to evaluate RAGAS metrics" });
        }
    }

    /// <summary>
    /// Get continuous monitoring metrics
    /// </summary>
    [HttpGet("ragas/monitoring")]
    public async Task<IActionResult> GetMonitoringMetrics(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        try
        {
            var metrics = await _ragasService.GetMonitoringMetricsAsync(from, to, cancellationToken);
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting monitoring metrics");
            return StatusCode(500, new { error = "Failed to retrieve monitoring metrics" });
        }
    }

    /// <summary>
    /// Compare two RAG configurations (A/B testing)
    /// </summary>
    [HttpPost("ragas/ab-test")]
    [EnableRateLimiting("ai")]
    public async Task<IActionResult> CompareConfigurations(
        [FromBody] ABTestRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _ragasService.CompareConfigurationsAsync(
                request.ConfigurationA,
                request.ConfigurationB,
                request.TestDatasetId,
                cancellationToken);
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error comparing configurations");
            return StatusCode(500, new { error = "Failed to compare configurations" });
        }
    }

    /// <summary>
    /// Get quality dashboard data
    /// </summary>
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        try
        {
            var qualityMetrics = await _qualityService.GetQualityMetricsAsync(from, to, cancellationToken);
            var ragasMetrics = await _ragasService.GetMonitoringMetricsAsync(from, to, cancellationToken);
            
            return Ok(new
            {
                quality = qualityMetrics,
                ragas = ragasMetrics,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dashboard data");
            return StatusCode(500, new { error = "Failed to retrieve dashboard data" });
        }
    }

    /// <summary>
    /// Calculate faithfulness score (response based on given context)
    /// </summary>
    [HttpPost("ragas/faithfulness")]
    [EnableRateLimiting("ai")]
    public async Task<IActionResult> CalculateFaithfulness(
        [FromBody] FaithfulnessRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var score = await _ragasService.CalculateFaithfulnessAsync(
                request.Response,
                request.Contexts,
                cancellationToken);
            
            return Ok(new
            {
                faithfulnessScore = score,
                description = "Measures if the response is grounded in the provided contexts",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating faithfulness");
            return StatusCode(500, new { error = "Failed to calculate faithfulness score" });
        }
    }

    /// <summary>
    /// Calculate answer relevancy score (response relevant to query)
    /// </summary>
    [HttpPost("ragas/relevancy")]
    [EnableRateLimiting("ai")]
    public async Task<IActionResult> CalculateRelevancy(
        [FromBody] RelevancyRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var score = await _ragasService.CalculateAnswerRelevancyAsync(
                request.Query,
                request.Response,
                cancellationToken);
            
            return Ok(new
            {
                relevancyScore = score,
                description = "Measures if the response is relevant to the query",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating answer relevancy");
            return StatusCode(500, new { error = "Failed to calculate relevancy score" });
        }
    }

    /// <summary>
    /// Calculate context precision (relevant context retrieved)
    /// </summary>
    [HttpPost("ragas/context-precision")]
    [EnableRateLimiting("ai")]
    public async Task<IActionResult> CalculateContextPrecision(
        [FromBody] ContextPrecisionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var score = await _ragasService.CalculateContextPrecisionAsync(
                request.Query,
                request.Contexts,
                request.GroundTruth,
                cancellationToken);
            
            return Ok(new
            {
                contextPrecisionScore = score,
                description = "Measures if the retrieved contexts are relevant to the query",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating context precision");
            return StatusCode(500, new { error = "Failed to calculate context precision score" });
        }
    }

    /// <summary>
    /// Calculate context recall (all relevant context retrieved)
    /// </summary>
    [HttpPost("ragas/context-recall")]
    [EnableRateLimiting("ai")]
    public async Task<IActionResult> CalculateContextRecall(
        [FromBody] ContextRecallRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var score = await _ragasService.CalculateContextRecallAsync(
                request.Contexts,
                request.GroundTruth,
                cancellationToken);
            
            return Ok(new
            {
                contextRecallScore = score,
                description = "Measures if all relevant context was retrieved",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating context recall");
            return StatusCode(500, new { error = "Failed to calculate context recall score" });
        }
    }

    /// <summary>
    /// Evaluate golden dataset for comprehensive testing
    /// </summary>
    [HttpPost("ragas/evaluate-dataset")]
    [EnableRateLimiting("ai")]
    public async Task<IActionResult> EvaluateGoldenDataset(
        [FromBody] EvaluateDatasetRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _ragasService.EvaluateGoldenDatasetAsync(
                request.DatasetId,
                cancellationToken);
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating golden dataset");
            return StatusCode(500, new { error = "Failed to evaluate golden dataset" });
        }
    }
}

public record VerifyQualityRequest(
    string Query,
    string Response,
    List<string> SourceDocumentIds);

public record HallucinationDetectionRequest(
    string Response,
    List<string> SourceTexts);

public record RAGASEvaluationRequest(
    string Query,
    string Response,
    List<string> Contexts,
    string? GroundTruth = null);

public record ABTestRequest(
    string ConfigurationA,
    string ConfigurationB,
    string TestDatasetId);

public record FaithfulnessRequest(
    string Response,
    List<string> Contexts);

public record RelevancyRequest(
    string Query,
    string Response);

public record ContextPrecisionRequest(
    string Query,
    List<string> Contexts,
    string? GroundTruth = null);

public record ContextRecallRequest(
    List<string> Contexts,
    string? GroundTruth = null);

public record EvaluateDatasetRequest(
    string DatasetId);

