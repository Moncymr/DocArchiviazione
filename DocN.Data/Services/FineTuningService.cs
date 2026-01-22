using DocN.Core.Interfaces;
using DocN.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace DocN.Data.Services;

/// <summary>
/// Service for fine-tuning embedding models on domain-specific documents
/// </summary>
public class FineTuningService : IFineTuningService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<FineTuningService> _logger;
    private readonly IMultiProviderAIService _aiService;

    public FineTuningService(
        ApplicationDbContext context,
        ILogger<FineTuningService> logger,
        IMultiProviderAIService aiService)
    {
        _context = context;
        _logger = logger;
        _aiService = aiService;
    }

    /// <summary>
    /// Generate training examples from existing documents
    /// Creates positive pairs from similar documents and negative pairs from dissimilar ones
    /// </summary>
    public async Task<List<TrainingExample>> GenerateTrainingExamplesAsync(
        IEnumerable<int> documentIds,
        int maxExamples = 1000,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating training examples from {Count} documents", documentIds.Count());

        var documents = await _context.Documents
            .Where(d => documentIds.Contains(d.Id))
            .ToListAsync(cancellationToken);
        
        // Load chunks separately to avoid navigation property issues
        var docIds = documents.Select(d => d.Id).ToList();
        var allChunks = await _context.DocumentChunks
            .Where(c => docIds.Contains(c.DocumentId))
            .ToListAsync(cancellationToken);
        
        var chunksByDocId = allChunks
            .GroupBy(c => c.DocumentId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var trainingExamples = new List<TrainingExample>();
        var random = new Random();

        // Group documents by category for better pair generation
        var documentsByCategory = documents
            .Where(d => !string.IsNullOrEmpty(d.ActualCategory))
            .GroupBy(d => d.ActualCategory)
            .ToDictionary(g => g.Key!, g => g.ToList());

        foreach (var category in documentsByCategory.Keys)
        {
            var categoryDocs = documentsByCategory[category];
            
            // Generate positive pairs (same category)
            for (int i = 0; i < categoryDocs.Count && trainingExamples.Count < maxExamples; i++)
            {
                var doc1 = categoryDocs[i];
                
                // Use chunks if available, otherwise use full text
                var text1 = GetRepresentativeText(doc1, chunksByDocId);
                if (string.IsNullOrWhiteSpace(text1))
                    continue;

                // Find a similar document (same category)
                for (int j = i + 1; j < Math.Min(i + 5, categoryDocs.Count); j++)
                {
                    var doc2 = categoryDocs[j];
                    var text2 = GetRepresentativeText(doc2, chunksByDocId);
                    
                    if (string.IsNullOrWhiteSpace(text2))
                        continue;

                    // Create positive example
                    var example = new TrainingExample
                    {
                        InputText = text1,
                        PositiveExample = text2,
                        Domain = category,
                        QualityScore = CalculateQualityScore(text1, text2)
                    };

                    // Find a negative example (different category)
                    var otherCategories = documentsByCategory.Keys
                        .Where(k => k != category)
                        .ToList();
                    
                    if (otherCategories.Any())
                    {
                        var negCategory = otherCategories[random.Next(otherCategories.Count)];
                        var negDocs = documentsByCategory[negCategory];
                        if (negDocs.Any())
                        {
                            var negDoc = negDocs[random.Next(negDocs.Count)];
                            example.NegativeExample = GetRepresentativeText(negDoc, chunksByDocId);
                        }
                    }

                    trainingExamples.Add(example);
                    
                    if (trainingExamples.Count >= maxExamples)
                        break;
                }
            }
        }

        // Save to database
        foreach (var example in trainingExamples)
        {
            var dbExample = new EmbeddingTrainingExample
            {
                InputText = example.InputText,
                PositiveExample = example.PositiveExample,
                NegativeExample = example.NegativeExample,
                Domain = example.Domain,
                QualityScore = example.QualityScore,
                CreatedAt = DateTime.UtcNow
            };
            _context.Set<EmbeddingTrainingExample>().Add(dbExample);
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Generated {Count} training examples", trainingExamples.Count);
        return trainingExamples;
    }

    /// <summary>
    /// Create a fine-tuning job
    /// </summary>
    public async Task<FineTuningJobResult> CreateFineTuningJobAsync(
        string baseModel,
        IEnumerable<TrainingExample> trainingExamples,
        FineTuningConfiguration? configuration = null,
        CancellationToken cancellationToken = default)
    {
        configuration ??= new FineTuningConfiguration();
        
        _logger.LogInformation("Creating fine-tuning job for model {BaseModel} with {Count} examples", 
            baseModel, trainingExamples.Count());

        var job = new FineTuningJob
        {
            Name = $"FineTune_{baseModel}_{DateTime.UtcNow:yyyyMMdd_HHmmss}",
            BaseModel = baseModel,
            Provider = configuration.Provider,
            Status = "Pending",
            TrainingExamplesCount = trainingExamples.Count(),
            ConfigurationJson = JsonSerializer.Serialize(configuration),
            CreatedAt = DateTime.UtcNow
        };

        _context.Set<FineTuningJob>().Add(job);
        await _context.SaveChangesAsync(cancellationToken);

        // Note: Actual fine-tuning would be triggered here via provider API
        // For OpenAI: https://platform.openai.com/docs/guides/fine-tuning
        // This is a placeholder implementation
        
        var result = new FineTuningJobResult
        {
            JobId = job.Id.ToString(),
            Status = job.Status,
            Message = "Fine-tuning job created. Implementation of provider-specific fine-tuning is required.",
            CreatedAt = job.CreatedAt
        };

        _logger.LogWarning("Fine-tuning job created but not started. Provider-specific implementation required.");
        
        return result;
    }

    /// <summary>
    /// Get status of a fine-tuning job
    /// </summary>
    public async Task<FineTuningJobStatus> GetJobStatusAsync(
        string jobId,
        CancellationToken cancellationToken = default)
    {
        if (!int.TryParse(jobId, out int id))
            throw new ArgumentException("Invalid job ID");

        var job = await _context.Set<FineTuningJob>()
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

        if (job == null)
            throw new ArgumentException($"Job {jobId} not found");

        var status = new FineTuningJobStatus
        {
            JobId = jobId,
            Status = job.Status,
            FineTunedModel = job.FineTunedModelId,
            ErrorMessage = job.ErrorMessage,
            CompletedAt = job.CompletedAt
        };

        if (!string.IsNullOrEmpty(job.MetricsJson))
        {
            status.Metrics = JsonSerializer.Deserialize<Dictionary<string, double>>(job.MetricsJson);
        }

        return status;
    }

    /// <summary>
    /// List all fine-tuned models
    /// </summary>
    public async Task<List<FineTunedModelInfo>> ListFineTunedModelsAsync(
        CancellationToken cancellationToken = default)
    {
        var models = await _context.Set<FineTunedModel>()
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync(cancellationToken);

        return models.Select(m => new FineTunedModelInfo
        {
            ModelId = m.ModelId,
            DisplayName = m.DisplayName,
            BaseModel = m.BaseModel,
            Provider = m.Provider,
            IsActive = m.IsActive,
            CreatedAt = m.CreatedAt,
            PerformanceMetrics = string.IsNullOrEmpty(m.PerformanceMetricsJson)
                ? null
                : JsonSerializer.Deserialize<Dictionary<string, double>>(m.PerformanceMetricsJson)
        }).ToList();
    }

    /// <summary>
    /// Activate a fine-tuned model
    /// </summary>
    public async Task<bool> ActivateModelAsync(
        string modelId,
        CancellationToken cancellationToken = default)
    {
        // Deactivate all other models
        var allModels = await _context.Set<FineTunedModel>().ToListAsync(cancellationToken);
        foreach (var model in allModels)
        {
            model.IsActive = false;
        }

        // Activate the specified model
        var targetModel = allModels.FirstOrDefault(m => m.ModelId == modelId);
        if (targetModel == null)
            return false;

        targetModel.IsActive = true;
        targetModel.LastUsedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation("Activated fine-tuned model: {ModelId}", modelId);
        return true;
    }

    /// <summary>
    /// Export training data in provider-specific format
    /// </summary>
    public async Task<string> ExportTrainingDataAsync(
        IEnumerable<TrainingExample> examples,
        string provider = "OpenAI",
        CancellationToken cancellationToken = default)
    {
        if (provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
        {
            return ExportOpenAIFormat(examples);
        }
        
        // Default JSONL format
        var builder = new StringBuilder();
        foreach (var example in examples)
        {
            var obj = new
            {
                input = example.InputText,
                positive = example.PositiveExample,
                negative = example.NegativeExample
            };
            builder.AppendLine(JsonSerializer.Serialize(obj));
        }

        return builder.ToString();
    }

    // Private helper methods

    private string GetRepresentativeText(Document document, Dictionary<int, List<DocumentChunk>>? chunksByDocId = null)
    {
        // Use first chunk if available and not too long
        if (chunksByDocId != null && chunksByDocId.TryGetValue(document.Id, out var chunks) && chunks.Any())
        {
            var firstChunk = chunks
                .OrderBy(c => c.ChunkIndex)
                .FirstOrDefault();
            
            if (firstChunk != null && !string.IsNullOrWhiteSpace(firstChunk.ChunkText))
            {
                return firstChunk.ChunkText.Length > 1000
                    ? firstChunk.ChunkText.Substring(0, 1000)
                    : firstChunk.ChunkText;
            }
        }

        // Fallback to extracted text
        if (!string.IsNullOrWhiteSpace(document.ExtractedText))
        {
            return document.ExtractedText.Length > 1000
                ? document.ExtractedText.Substring(0, 1000)
                : document.ExtractedText;
        }

        return string.Empty;
    }

    private double CalculateQualityScore(string text1, string text2)
    {
        // Simple quality heuristic based on text length and overlap
        double score = 0.5;

        // Length score (prefer moderate lengths)
        if (text1.Length >= 100 && text1.Length <= 1000 &&
            text2.Length >= 100 && text2.Length <= 1000)
        {
            score += 0.3;
        }

        // Simple word overlap score
        var words1 = text1.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var words2 = text2.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        
        if (words1.Any() && words2.Any())
        {
            int overlap = words1.Intersect(words2).Count();
            double overlapRatio = (double)overlap / Math.Max(words1.Count, words2.Count);
            
            // Good pairs have some overlap but aren't identical
            if (overlapRatio > 0.1 && overlapRatio < 0.7)
            {
                score += 0.2;
            }
        }

        return Math.Min(1.0, score);
    }

    private string ExportOpenAIFormat(IEnumerable<TrainingExample> examples)
    {
        // OpenAI fine-tuning format for embeddings
        // Format: JSONL with {"input": "text", "output": "embedding"} or similar
        var builder = new StringBuilder();
        
        foreach (var example in examples)
        {
            // For embeddings, we typically use contrastive learning format
            var obj = new
            {
                text = example.InputText,
                positive = example.PositiveExample,
                negative = example.NegativeExample ?? ""
            };
            builder.AppendLine(JsonSerializer.Serialize(obj));
        }

        return builder.ToString();
    }
}
