using Microsoft.AspNetCore.Mvc;
using DocN.Core.Interfaces;
using DocN.Data.Models;
using DocN.Data;
using Microsoft.EntityFrameworkCore;

namespace DocN.Server.Controllers;

/// <summary>
/// Controller for RAG enhancement features demonstration and testing
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class RAGEnhancementsController : ControllerBase
{
    private readonly ILogger<RAGEnhancementsController> _logger;
    private readonly ISemanticChunkingService _semanticChunker;
    private readonly IRetrievalMetricsService _retrievalMetrics;
    private readonly IEmbeddingFineTuningService _fineTuning;
    private readonly ApplicationDbContext _context;

    public RAGEnhancementsController(
        ILogger<RAGEnhancementsController> logger,
        ISemanticChunkingService semanticChunker,
        IRetrievalMetricsService retrievalMetrics,
        IEmbeddingFineTuningService fineTuning,
        ApplicationDbContext context)
    {
        _logger = logger;
        _semanticChunker = semanticChunker;
        _retrievalMetrics = retrievalMetrics;
        _fineTuning = fineTuning;
        _context = context;
    }

    /// <summary>
    /// Demonstrate semantic chunking on provided text
    /// </summary>
    [HttpPost("demo/semantic-chunking")]
    public IActionResult DemoSemanticChunking([FromBody] ChunkingDemoRequest request)
    {
        try
        {
            var chunks = request.UseStructure
                ? _semanticChunker.ChunkByStructure(request.Text, request.MaxChunkSize)
                : _semanticChunker.ChunkBySemantic(request.Text, request.MaxChunkSize, request.MinChunkSize);

            return Ok(new
            {
                totalChunks = chunks.Count,
                strategy = request.UseStructure ? "structure-based" : "semantic",
                chunks = chunks.Select(c => new
                {
                    text = c.Text.Length > 100 ? c.Text.Substring(0, 100) + "..." : c.Text,
                    fullText = c.Text,
                    length = c.Text.Length,
                    type = c.ChunkType,
                    metadata = new
                    {
                        title = c.Metadata.Title,
                        sectionPath = c.Metadata.SectionPath,
                        keywords = c.Metadata.Keywords,
                        documentType = c.Metadata.DocumentType,
                        headerLevel = c.Metadata.HeaderLevel,
                        isListItem = c.Metadata.IsListItem
                    }
                }).ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in semantic chunking demo");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Extract document structure from text
    /// </summary>
    [HttpPost("demo/extract-structure")]
    public IActionResult ExtractStructure([FromBody] StructureExtractionRequest request)
    {
        try
        {
            var structure = _semanticChunker.ExtractStructure(request.Text);

            return Ok(new
            {
                documentType = structure.DocumentType,
                hasHierarchy = structure.HasHierarchy,
                sectionCount = structure.Sections.Count,
                sections = structure.Sections.Select(s => new
                {
                    title = s.Title,
                    level = s.Level,
                    startPosition = s.StartPosition,
                    endPosition = s.EndPosition,
                    length = s.EndPosition - s.StartPosition
                }).ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting structure");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get chunk metadata statistics for a document
    /// </summary>
    [HttpGet("documents/{documentId}/chunk-metadata")]
    public async Task<IActionResult> GetChunkMetadata(int documentId)
    {
        try
        {
            var chunks = await _context.DocumentChunks
                .Where(c => c.DocumentId == documentId)
                .ToListAsync();

            if (!chunks.Any())
            {
                return NotFound(new { message = "No chunks found for this document" });
            }

            var metadata = new
            {
                documentId,
                totalChunks = chunks.Count,
                chunkTypes = chunks.GroupBy(c => c.ChunkType)
                    .ToDictionary(g => g.Key, g => g.Count()),
                sectionsWithTitles = chunks.Count(c => !string.IsNullOrEmpty(c.SectionTitle)),
                chunksWithKeywords = chunks.Count(c => !string.IsNullOrEmpty(c.KeywordsJson)),
                headerLevels = chunks.GroupBy(c => c.HeaderLevel)
                    .Where(g => g.Key > 0)
                    .ToDictionary(g => $"H{g.Key}", g => g.Count()),
                documentTypes = chunks.Where(c => !string.IsNullOrEmpty(c.DocumentType))
                    .GroupBy(c => c.DocumentType)
                    .ToDictionary(g => g.Key!, g => g.Count()),
                listItems = chunks.Count(c => c.IsListItem),
                avgChunkLength = chunks.Average(c => c.ChunkText.Length),
                sections = chunks.Where(c => !string.IsNullOrEmpty(c.SectionTitle))
                    .Select(c => c.SectionTitle)
                    .Distinct()
                    .ToList()
            };

            return Ok(metadata);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting chunk metadata");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Evaluate retrieval quality on golden dataset
    /// </summary>
    [HttpPost("retrieval/evaluate/{datasetId}")]
    public async Task<IActionResult> EvaluateRetrieval(string datasetId)
    {
        try
        {
            _logger.LogInformation("Starting retrieval evaluation for dataset: {DatasetId}", datasetId);

            var result = await _retrievalMetrics.EvaluateRetrievalQualityAsync(datasetId);

            return Ok(new
            {
                datasetId,
                metrics = new
                {
                    mrr = result.MRR,
                    ndcg = result.NDCG,
                    hitRate = result.HitRate,
                    precisionAtK = result.PrecisionAtK,
                    recallAtK = result.RecallAtK
                },
                totalQueries = result.TotalQueries,
                k = result.K,
                evaluatedAt = result.EvaluatedAt,
                configurationName = result.ConfigurationName,
                topQueries = result.PerQueryMetrics
                    .OrderByDescending(q => q.Value.MRR)
                    .Take(5)
                    .Select(q => new
                    {
                        query = q.Value.Query,
                        mrr = q.Value.MRR,
                        ndcg = q.Value.NDCG,
                        hitRate = q.Value.HitRate
                    })
                    .ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating retrieval");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Compare two retrieval configurations (A/B test)
    /// </summary>
    [HttpPost("retrieval/ab-test")]
    public async Task<IActionResult> ABTestRetrieval([FromBody] RetrievalABTestRequest request)
    {
        try
        {
            var result = await _retrievalMetrics.CompareRetrievalConfigurationsAsync(
                request.ConfigurationA,
                request.ConfigurationB,
                request.DatasetId);

            return Ok(new
            {
                winner = result.Winner,
                isSignificant = result.IsStatisticallySignificant,
                improvements = result.ImprovementPercentages,
                configurationA = new
                {
                    name = result.ConfigurationA,
                    mrr = result.ResultsA.MRR,
                    ndcg = result.ResultsA.NDCG,
                    hitRate = result.ResultsA.HitRate
                },
                configurationB = new
                {
                    name = result.ConfigurationB,
                    mrr = result.ResultsB.MRR,
                    ndcg = result.ResultsB.NDCG,
                    hitRate = result.ResultsB.HitRate
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in A/B test");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Prepare training data for embedding fine-tuning
    /// </summary>
    [HttpPost("fine-tuning/prepare-training-data")]
    public async Task<IActionResult> PrepareTrainingData([FromBody] TrainingDataRequest request)
    {
        try
        {
            var result = await _fineTuning.PrepareTrainingDataAsync(
                request.DocumentIds,
                request.OutputPath);

            return Ok(new
            {
                success = true,
                totalExamples = result.TotalExamples,
                outputPath = result.OutputPath,
                format = result.Format,
                statistics = result.Statistics,
                warnings = result.Warnings
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error preparing training data");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Generate contrastive pairs for similarity learning
    /// </summary>
    [HttpPost("fine-tuning/contrastive-pairs")]
    public async Task<IActionResult> GenerateContrastivePairs([FromBody] ContrastivePairsRequest request)
    {
        try
        {
            var pairs = await _fineTuning.GenerateContrastivePairsAsync(
                request.DocumentIds,
                request.NumPairs);

            return Ok(new
            {
                totalPairs = pairs.Count,
                samples = pairs.Take(5).Select(p => new
                {
                    anchor = p.Anchor.Length > 100 ? p.Anchor.Substring(0, 100) + "..." : p.Anchor,
                    positive = p.Positive.Length > 100 ? p.Positive.Substring(0, 100) + "..." : p.Positive,
                    negative = p.Negative.Length > 100 ? p.Negative.Substring(0, 100) + "..." : p.Negative,
                    metadata = p.Metadata
                }).ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating contrastive pairs");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Evaluate embedding model quality
    /// </summary>
    [HttpPost("fine-tuning/evaluate-model")]
    public async Task<IActionResult> EvaluateEmbeddingModel([FromBody] ModelEvaluationRequest request)
    {
        try
        {
            var result = await _fineTuning.EvaluateEmbeddingModelAsync(
                request.TestDatasetId,
                request.ModelName);

            return Ok(new
            {
                modelName = result.ModelName,
                metrics = new
                {
                    averageSimilarity = result.AverageSimilarityScore,
                    retrievalAccuracy = result.RetrievalAccuracy,
                    map = result.MAP
                },
                embeddingDimension = result.EmbeddingDimension,
                averageInferenceTimeMs = result.AverageInferenceTimeMs,
                categoryPerformance = result.CategoryPerformance,
                evaluatedAt = result.EvaluatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating embedding model");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

// Request DTOs
public class ChunkingDemoRequest
{
    public string Text { get; set; } = string.Empty;
    public int MaxChunkSize { get; set; } = 1000;
    public int MinChunkSize { get; set; } = 200;
    public bool UseStructure { get; set; } = false;
}

public class StructureExtractionRequest
{
    public string Text { get; set; } = string.Empty;
}

public class RetrievalABTestRequest
{
    public string ConfigurationA { get; set; } = string.Empty;
    public string ConfigurationB { get; set; } = string.Empty;
    public string DatasetId { get; set; } = string.Empty;
}

public class TrainingDataRequest
{
    public List<int> DocumentIds { get; set; } = new();
    public string OutputPath { get; set; } = "/tmp/training_data.jsonl";
}

public class ContrastivePairsRequest
{
    public List<int> DocumentIds { get; set; } = new();
    public int NumPairs { get; set; } = 1000;
}

public class ModelEvaluationRequest
{
    public string TestDatasetId { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
}
