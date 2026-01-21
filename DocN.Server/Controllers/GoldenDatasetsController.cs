using Microsoft.AspNetCore.Mvc;
using DocN.Core.Interfaces;
using Microsoft.AspNetCore.RateLimiting;

namespace DocN.Server.Controllers;

/// <summary>
/// Controller for managing golden datasets for RAG quality regression testing
/// </summary>
[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("api")]
public class GoldenDatasetsController : ControllerBase
{
    private readonly IGoldenDatasetService _datasetService;
    private readonly IRAGASMetricsService _ragasService;
    private readonly ILogger<GoldenDatasetsController> _logger;

    public GoldenDatasetsController(
        IGoldenDatasetService datasetService,
        IRAGASMetricsService ragasService,
        ILogger<GoldenDatasetsController> logger)
    {
        _datasetService = datasetService;
        _ragasService = ragasService;
        _logger = logger;
    }

    /// <summary>
    /// List all golden datasets
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ListDatasets(
        [FromQuery] int? tenantId,
        [FromQuery] bool activeOnly = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var datasets = await _datasetService.ListDatasetsAsync(tenantId, activeOnly, cancellationToken);
            return Ok(datasets);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing golden datasets");
            return StatusCode(500, new { error = "Failed to list golden datasets" });
        }
    }

    /// <summary>
    /// Get a specific golden dataset by ID
    /// </summary>
    [HttpGet("{datasetId}")]
    public async Task<IActionResult> GetDataset(
        string datasetId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dataset = await _datasetService.GetDatasetAsync(datasetId, cancellationToken);
            
            if (dataset == null)
                return NotFound(new { error = $"Dataset '{datasetId}' not found" });
            
            return Ok(dataset);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting golden dataset {DatasetId}", datasetId);
            return StatusCode(500, new { error = "Failed to retrieve golden dataset" });
        }
    }

    /// <summary>
    /// Create a new golden dataset
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateDataset(
        [FromBody] CreateGoldenDatasetRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dataset = await _datasetService.CreateDatasetAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetDataset), new { datasetId = dataset.DatasetId }, dataset);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating golden dataset");
            return StatusCode(500, new { error = "Failed to create golden dataset" });
        }
    }

    /// <summary>
    /// Get samples from a dataset
    /// </summary>
    [HttpGet("{datasetId}/samples")]
    public async Task<IActionResult> GetSamples(
        string datasetId,
        [FromQuery] string? category,
        [FromQuery] bool activeOnly = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var samples = await _datasetService.GetSamplesAsync(datasetId, category, activeOnly, cancellationToken);
            return Ok(samples);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting samples for dataset {DatasetId}", datasetId);
            return StatusCode(500, new { error = "Failed to retrieve samples" });
        }
    }

    /// <summary>
    /// Add a sample to a dataset
    /// </summary>
    [HttpPost("{datasetId}/samples")]
    public async Task<IActionResult> AddSample(
        string datasetId,
        [FromBody] CreateGoldenDatasetSampleRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var sample = await _datasetService.AddSampleAsync(datasetId, request, cancellationToken);
            return CreatedAtAction(nameof(GetSamples), new { datasetId }, sample);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding sample to dataset {DatasetId}", datasetId);
            return StatusCode(500, new { error = "Failed to add sample" });
        }
    }

    /// <summary>
    /// Update a sample
    /// </summary>
    [HttpPut("samples/{sampleId}")]
    public async Task<IActionResult> UpdateSample(
        int sampleId,
        [FromBody] UpdateGoldenDatasetSampleRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var sample = await _datasetService.UpdateSampleAsync(sampleId, request, cancellationToken);
            return Ok(sample);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating sample {SampleId}", sampleId);
            return StatusCode(500, new { error = "Failed to update sample" });
        }
    }

    /// <summary>
    /// Delete a sample
    /// </summary>
    [HttpDelete("samples/{sampleId}")]
    public async Task<IActionResult> DeleteSample(
        int sampleId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _datasetService.DeleteSampleAsync(sampleId, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting sample {SampleId}", sampleId);
            return StatusCode(500, new { error = "Failed to delete sample" });
        }
    }

    /// <summary>
    /// Import samples from JSON
    /// </summary>
    [HttpPost("{datasetId}/import")]
    public async Task<IActionResult> ImportSamples(
        string datasetId,
        [FromBody] ImportSamplesRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _datasetService.ImportSamplesFromJsonAsync(datasetId, request.JsonContent, cancellationToken);
            return Ok(new { message = "Samples imported successfully" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing samples to dataset {DatasetId}", datasetId);
            return StatusCode(500, new { error = "Failed to import samples" });
        }
    }

    /// <summary>
    /// Export samples to JSON
    /// </summary>
    [HttpGet("{datasetId}/export")]
    public async Task<IActionResult> ExportSamples(
        string datasetId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var json = await _datasetService.ExportSamplesToJsonAsync(datasetId, cancellationToken);
            return Content(json, "application/json");
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting samples from dataset {DatasetId}", datasetId);
            return StatusCode(500, new { error = "Failed to export samples" });
        }
    }

    /// <summary>
    /// Evaluate a golden dataset
    /// </summary>
    [HttpPost("{datasetId}/evaluate")]
    [EnableRateLimiting("ai")]
    public async Task<IActionResult> EvaluateDataset(
        string datasetId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting evaluation of golden dataset: {DatasetId}", datasetId);
            var result = await _ragasService.EvaluateGoldenDatasetAsync(datasetId, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating golden dataset {DatasetId}", datasetId);
            return StatusCode(500, new { error = "Failed to evaluate golden dataset" });
        }
    }

    /// <summary>
    /// Get evaluation history for a dataset
    /// </summary>
    [HttpGet("{datasetId}/evaluations")]
    public async Task<IActionResult> GetEvaluationHistory(
        string datasetId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var history = await _datasetService.GetEvaluationHistoryAsync(datasetId, from, to, cancellationToken);
            return Ok(history);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting evaluation history for dataset {DatasetId}", datasetId);
            return StatusCode(500, new { error = "Failed to retrieve evaluation history" });
        }
    }
}

/// <summary>
/// Request for importing samples
/// </summary>
public class ImportSamplesRequest
{
    public string JsonContent { get; set; } = string.Empty;
}
