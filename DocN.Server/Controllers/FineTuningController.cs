using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using DocN.Core.Interfaces;

namespace DocN.Server.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class FineTuningController : ControllerBase
{
    private readonly IFineTuningService _fineTuningService;
    private readonly ILogger<FineTuningController> _logger;

    public FineTuningController(
        IFineTuningService fineTuningService,
        ILogger<FineTuningController> logger)
    {
        _fineTuningService = fineTuningService;
        _logger = logger;
    }

    /// <summary>
    /// Generate training examples from documents
    /// </summary>
    [HttpPost("generate-training-examples")]
    public async Task<ActionResult<List<TrainingExample>>> GenerateTrainingExamples(
        [FromBody] GenerateTrainingExamplesRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Generating training examples from {Count} documents", 
                request.DocumentIds.Count);

            var examples = await _fineTuningService.GenerateTrainingExamplesAsync(
                request.DocumentIds,
                request.MaxExamples ?? 1000,
                cancellationToken);

            return Ok(examples);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating training examples");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Create a fine-tuning job
    /// Note: This endpoint creates a fine-tuning job record but provider-specific
    /// fine-tuning implementation (OpenAI, Cohere, etc.) is not yet complete.
    /// The job will be created with "Pending" status and requires manual completion.
    /// </summary>
    [HttpPost("create-job")]
    public async Task<ActionResult<FineTuningJobResult>> CreateFineTuningJob(
        [FromBody] CreateFineTuningJobRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Creating fine-tuning job for model {BaseModel}", 
                request.BaseModel);

            var configuration = request.Configuration != null 
                ? new FineTuningConfiguration
                {
                    Provider = request.Configuration.Provider ?? "OpenAI",
                    Epochs = request.Configuration.Epochs ?? 3,
                    LearningRate = request.Configuration.LearningRate ?? 0.0001,
                    BatchSize = request.Configuration.BatchSize ?? 32,
                    ValidationSplit = request.Configuration.ValidationSplit,
                    AdditionalParameters = request.Configuration.AdditionalParameters ?? new()
                }
                : null;

            var result = await _fineTuningService.CreateFineTuningJobAsync(
                request.BaseModel,
                request.TrainingExamples,
                configuration,
                cancellationToken);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating fine-tuning job");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get status of a fine-tuning job
    /// </summary>
    [HttpGet("job-status/{jobId}")]
    public async Task<ActionResult<FineTuningJobStatus>> GetJobStatus(
        string jobId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var status = await _fineTuningService.GetJobStatusAsync(jobId, cancellationToken);
            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting job status for {JobId}", jobId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// List all fine-tuned models
    /// </summary>
    [HttpGet("models")]
    public async Task<ActionResult<List<FineTunedModelInfo>>> ListModels(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var models = await _fineTuningService.ListFineTunedModelsAsync(cancellationToken);
            return Ok(models);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing fine-tuned models");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Activate a fine-tuned model
    /// </summary>
    [HttpPost("activate-model/{modelId}")]
    public async Task<ActionResult> ActivateModel(
        string modelId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var success = await _fineTuningService.ActivateModelAsync(modelId, cancellationToken);
            
            if (success)
            {
                return Ok(new { message = $"Model {modelId} activated successfully" });
            }
            else
            {
                return NotFound(new { error = $"Model {modelId} not found" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error activating model {ModelId}", modelId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Export training data in provider-specific format
    /// </summary>
    [HttpPost("export-training-data")]
    public async Task<ActionResult<string>> ExportTrainingData(
        [FromBody] ExportTrainingDataRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var exportedData = await _fineTuningService.ExportTrainingDataAsync(
                request.TrainingExamples,
                request.Provider ?? "OpenAI",
                cancellationToken);

            return Ok(new { data = exportedData, provider = request.Provider });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting training data");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

/// <summary>
/// Request for generating training examples
/// </summary>
public class GenerateTrainingExamplesRequest
{
    public List<int> DocumentIds { get; set; } = new();
    public int? MaxExamples { get; set; }
}

/// <summary>
/// Request for creating a fine-tuning job
/// </summary>
public class CreateFineTuningJobRequest
{
    public string BaseModel { get; set; } = string.Empty;
    public List<TrainingExample> TrainingExamples { get; set; } = new();
    public FineTuningConfigurationDto? Configuration { get; set; }
}

/// <summary>
/// DTO for fine-tuning configuration
/// </summary>
public class FineTuningConfigurationDto
{
    public string? Provider { get; set; }
    public int? Epochs { get; set; }
    public double? LearningRate { get; set; }
    public int? BatchSize { get; set; }
    public string? ValidationSplit { get; set; }
    public Dictionary<string, object>? AdditionalParameters { get; set; }
}

/// <summary>
/// Request for exporting training data
/// </summary>
public class ExportTrainingDataRequest
{
    public List<TrainingExample> TrainingExamples { get; set; } = new();
    public string? Provider { get; set; }
}
