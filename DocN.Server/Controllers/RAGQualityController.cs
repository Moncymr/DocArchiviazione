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
