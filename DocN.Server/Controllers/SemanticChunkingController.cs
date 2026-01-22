using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using DocN.Core.Interfaces;
using DocN.Data;
using Microsoft.EntityFrameworkCore;

namespace DocN.Server.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class SemanticChunkingController : ControllerBase
{
    private readonly ISemanticChunkingService _chunkingService;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SemanticChunkingController> _logger;

    public SemanticChunkingController(
        ISemanticChunkingService chunkingService,
        ApplicationDbContext context,
        ILogger<SemanticChunkingController> logger)
    {
        _chunkingService = chunkingService;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Chunk text using semantic strategy
    /// </summary>
    [HttpPost("chunk-text")]
    public async Task<ActionResult<List<EnhancedChunk>>> ChunkText(
        [FromBody] ChunkTextRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var options = new ChunkingOptions
            {
                MaxChunkSize = request.MaxChunkSize ?? 1000,
                MinChunkSize = request.MinChunkSize ?? 100,
                Overlap = request.Overlap ?? 200,
                ExtractKeywords = request.ExtractKeywords ?? true,
                MaxKeywords = request.MaxKeywords ?? 10,
                DetectSections = request.DetectSections ?? true,
                CalculateImportance = request.CalculateImportance ?? true,
                ContentType = request.ContentType
            };

            var chunks = await _chunkingService.ChunkDocumentSemanticAsync(
                request.Text,
                request.Strategy ?? ChunkingStrategy.Semantic,
                options,
                cancellationToken);

            return Ok(chunks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error chunking text");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Chunk text with automatic structure detection
    /// </summary>
    [HttpPost("chunk-with-detection")]
    public async Task<ActionResult<List<EnhancedChunk>>> ChunkWithDetection(
        [FromBody] ChunkWithDetectionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var options = new ChunkingOptions
            {
                MaxChunkSize = request.MaxChunkSize ?? 1000,
                MinChunkSize = request.MinChunkSize ?? 100,
                Overlap = request.Overlap ?? 200,
                ExtractKeywords = request.ExtractKeywords ?? true,
                MaxKeywords = request.MaxKeywords ?? 10
            };

            var chunks = await _chunkingService.ChunkWithStructureDetectionAsync(
                request.Text,
                request.ContentType,
                options,
                cancellationToken);

            return Ok(chunks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error chunking text with detection");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Extract metadata from a chunk
    /// </summary>
    [HttpPost("extract-metadata")]
    public async Task<ActionResult<ChunkMetadata>> ExtractMetadata(
        [FromBody] ExtractMetadataRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var metadata = await _chunkingService.ExtractChunkMetadataAsync(
                request.ChunkText,
                request.Context,
                cancellationToken);

            return Ok(metadata);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting metadata");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Calculate importance score for a chunk
    /// </summary>
    [HttpPost("calculate-importance")]
    public ActionResult<double> CalculateImportance([FromBody] CalculateImportanceRequest request)
    {
        try
        {
            var score = _chunkingService.CalculateImportanceScore(
                request.ChunkText,
                request.Position,
                request.TotalChunks,
                request.KeywordWeights);

            return Ok(new { importanceScore = score });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating importance");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get available chunking strategies
    /// </summary>
    [HttpGet("strategies")]
    public ActionResult<List<string>> GetStrategies()
    {
        var strategies = Enum.GetNames(typeof(ChunkingStrategy)).ToList();
        return Ok(strategies);
    }
}

/// <summary>
/// Request model for chunking text
/// </summary>
public class ChunkTextRequest
{
    public string Text { get; set; } = string.Empty;
    public ChunkingStrategy? Strategy { get; set; }
    public int? MaxChunkSize { get; set; }
    public int? MinChunkSize { get; set; }
    public int? Overlap { get; set; }
    public bool? ExtractKeywords { get; set; }
    public int? MaxKeywords { get; set; }
    public bool? DetectSections { get; set; }
    public bool? CalculateImportance { get; set; }
    public string? ContentType { get; set; }
}

/// <summary>
/// Request for chunking with detection
/// </summary>
public class ChunkWithDetectionRequest
{
    public string Text { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public int? MaxChunkSize { get; set; }
    public int? MinChunkSize { get; set; }
    public int? Overlap { get; set; }
    public bool? ExtractKeywords { get; set; }
    public int? MaxKeywords { get; set; }
}

/// <summary>
/// Request for extracting metadata
/// </summary>
public class ExtractMetadataRequest
{
    public string ChunkText { get; set; } = string.Empty;
    public string? Context { get; set; }
}

/// <summary>
/// Request for calculating importance
/// </summary>
public class CalculateImportanceRequest
{
    public string ChunkText { get; set; } = string.Empty;
    public int Position { get; set; }
    public int TotalChunks { get; set; }
    public Dictionary<string, double>? KeywordWeights { get; set; }
}
