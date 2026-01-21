using DocN.Core.Interfaces;
using DocN.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace DocN.Data.Services;

/// <summary>
/// Service for managing golden datasets for RAG quality regression testing
/// </summary>
public class GoldenDatasetService : IGoldenDatasetService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<GoldenDatasetService> _logger;

    public GoldenDatasetService(
        ApplicationDbContext context,
        ILogger<GoldenDatasetService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<GoldenDatasetDto> CreateDatasetAsync(
        CreateGoldenDatasetRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating golden dataset: {DatasetId}", request.DatasetId);

        // Check if dataset already exists
        var existing = await _context.GoldenDatasets
            .FirstOrDefaultAsync(d => d.DatasetId == request.DatasetId, cancellationToken);

        if (existing != null)
        {
            throw new InvalidOperationException($"Dataset with ID '{request.DatasetId}' already exists");
        }

        var dataset = new GoldenDataset
        {
            DatasetId = request.DatasetId,
            Name = request.Name,
            Description = request.Description,
            Version = request.Version,
            TenantId = request.TenantId,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _context.GoldenDatasets.Add(dataset);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Golden dataset created: {DatasetId} (ID: {Id})", dataset.DatasetId, dataset.Id);

        return await MapToDto(dataset, cancellationToken);
    }

    public async Task<GoldenDatasetDto?> GetDatasetAsync(
        string datasetId,
        CancellationToken cancellationToken = default)
    {
        var dataset = await _context.GoldenDatasets
            .Include(d => d.Samples)
            .Include(d => d.EvaluationRecords)
            .FirstOrDefaultAsync(d => d.DatasetId == datasetId, cancellationToken);

        if (dataset == null)
            return null;

        return await MapToDto(dataset, cancellationToken);
    }

    public async Task<List<GoldenDatasetDto>> ListDatasetsAsync(
        int? tenantId = null,
        bool activeOnly = true,
        CancellationToken cancellationToken = default)
    {
        var query = _context.GoldenDatasets
            .Include(d => d.Samples)
            .Include(d => d.EvaluationRecords)
            .AsQueryable();

        if (tenantId.HasValue)
            query = query.Where(d => d.TenantId == tenantId.Value);

        if (activeOnly)
            query = query.Where(d => d.IsActive);

        var datasets = await query.ToListAsync(cancellationToken);

        var dtos = new List<GoldenDatasetDto>();
        foreach (var dataset in datasets)
        {
            dtos.Add(await MapToDto(dataset, cancellationToken));
        }

        return dtos;
    }

    public async Task<GoldenDatasetSampleDto> AddSampleAsync(
        string datasetId,
        CreateGoldenDatasetSampleRequest request,
        CancellationToken cancellationToken = default)
    {
        var dataset = await _context.GoldenDatasets
            .FirstOrDefaultAsync(d => d.DatasetId == datasetId, cancellationToken);

        if (dataset == null)
        {
            throw new InvalidOperationException($"Dataset '{datasetId}' not found");
        }

        var sample = new GoldenDatasetSample
        {
            GoldenDatasetId = dataset.Id,
            Query = request.Query,
            GroundTruth = request.GroundTruth,
            RelevantDocumentIdsJson = request.RelevantDocumentIds != null
                ? JsonSerializer.Serialize(request.RelevantDocumentIds)
                : null,
            ExpectedResponse = request.ExpectedResponse,
            Category = request.Category,
            DifficultyLevel = request.DifficultyLevel,
            ImportanceWeight = request.ImportanceWeight,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _context.GoldenDatasetSamples.Add(sample);
        
        // Update dataset timestamp
        dataset.UpdatedAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Added sample to dataset {DatasetId}: {Query}", datasetId, request.Query);

        return MapSampleToDto(sample);
    }

    public async Task<GoldenDatasetSampleDto> UpdateSampleAsync(
        int sampleId,
        UpdateGoldenDatasetSampleRequest request,
        CancellationToken cancellationToken = default)
    {
        var sample = await _context.GoldenDatasetSamples
            .Include(s => s.GoldenDataset)
            .FirstOrDefaultAsync(s => s.Id == sampleId, cancellationToken);

        if (sample == null)
        {
            throw new InvalidOperationException($"Sample {sampleId} not found");
        }

        if (!string.IsNullOrEmpty(request.Query))
            sample.Query = request.Query;

        if (!string.IsNullOrEmpty(request.GroundTruth))
            sample.GroundTruth = request.GroundTruth;

        if (request.RelevantDocumentIds != null)
            sample.RelevantDocumentIdsJson = JsonSerializer.Serialize(request.RelevantDocumentIds);

        if (request.ExpectedResponse != null)
            sample.ExpectedResponse = request.ExpectedResponse;

        if (request.Category != null)
            sample.Category = request.Category;

        if (!string.IsNullOrEmpty(request.DifficultyLevel))
            sample.DifficultyLevel = request.DifficultyLevel;

        if (request.ImportanceWeight.HasValue)
            sample.ImportanceWeight = request.ImportanceWeight.Value;

        if (request.Notes != null)
            sample.Notes = request.Notes;

        if (request.IsActive.HasValue)
            sample.IsActive = request.IsActive.Value;

        // Update dataset timestamp
        sample.GoldenDataset.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated sample {SampleId}", sampleId);

        return MapSampleToDto(sample);
    }

    public async Task DeleteSampleAsync(
        int sampleId,
        CancellationToken cancellationToken = default)
    {
        var sample = await _context.GoldenDatasetSamples
            .Include(s => s.GoldenDataset)
            .FirstOrDefaultAsync(s => s.Id == sampleId, cancellationToken);

        if (sample == null)
        {
            throw new InvalidOperationException($"Sample {sampleId} not found");
        }

        _context.GoldenDatasetSamples.Remove(sample);
        
        // Update dataset timestamp
        sample.GoldenDataset.UpdatedAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted sample {SampleId}", sampleId);
    }

    public async Task<List<GoldenDatasetSampleDto>> GetSamplesAsync(
        string datasetId,
        string? category = null,
        bool activeOnly = true,
        CancellationToken cancellationToken = default)
    {
        var dataset = await _context.GoldenDatasets
            .FirstOrDefaultAsync(d => d.DatasetId == datasetId, cancellationToken);

        if (dataset == null)
        {
            throw new InvalidOperationException($"Dataset '{datasetId}' not found");
        }

        var query = _context.GoldenDatasetSamples
            .Where(s => s.GoldenDatasetId == dataset.Id);

        if (!string.IsNullOrEmpty(category))
            query = query.Where(s => s.Category == category);

        if (activeOnly)
            query = query.Where(s => s.IsActive);

        var samples = await query
            .OrderBy(s => s.Category)
            .ThenBy(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

        return samples.Select(MapSampleToDto).ToList();
    }

    public async Task ImportSamplesFromJsonAsync(
        string datasetId,
        string jsonContent,
        CancellationToken cancellationToken = default)
    {
        var dataset = await _context.GoldenDatasets
            .FirstOrDefaultAsync(d => d.DatasetId == datasetId, cancellationToken);

        if (dataset == null)
        {
            throw new InvalidOperationException($"Dataset '{datasetId}' not found");
        }

        try
        {
            var importData = JsonSerializer.Deserialize<GoldenDatasetImport>(jsonContent);
            if (importData == null || importData.Samples == null)
            {
                throw new InvalidOperationException("Invalid JSON format");
            }

            var samples = new List<GoldenDatasetSample>();
            foreach (var sampleData in importData.Samples)
            {
                var sample = new GoldenDatasetSample
                {
                    GoldenDatasetId = dataset.Id,
                    Query = sampleData.Query,
                    GroundTruth = sampleData.GroundTruth,
                    RelevantDocumentIdsJson = sampleData.RelevantDocIds != null
                        ? JsonSerializer.Serialize(sampleData.RelevantDocIds)
                        : null,
                    ExpectedResponse = sampleData.ExpectedResponse,
                    Category = sampleData.Category,
                    DifficultyLevel = sampleData.DifficultyLevel ?? "medium",
                    ImportanceWeight = sampleData.ImportanceWeight ?? 5,
                    Notes = sampleData.Notes,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                samples.Add(sample);
            }

            _context.GoldenDatasetSamples.AddRange(samples);
            dataset.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Imported {Count} samples to dataset {DatasetId}", samples.Count, datasetId);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse JSON for dataset import");
            throw new InvalidOperationException("Invalid JSON format", ex);
        }
    }

    public async Task<string> ExportSamplesToJsonAsync(
        string datasetId,
        CancellationToken cancellationToken = default)
    {
        var dataset = await _context.GoldenDatasets
            .Include(d => d.Samples.Where(s => s.IsActive))
            .FirstOrDefaultAsync(d => d.DatasetId == datasetId, cancellationToken);

        if (dataset == null)
        {
            throw new InvalidOperationException($"Dataset '{datasetId}' not found");
        }

        var exportData = new GoldenDatasetExport
        {
            DatasetId = dataset.DatasetId,
            Name = dataset.Name,
            Description = dataset.Description,
            Version = dataset.Version,
            ExportedAt = DateTime.UtcNow,
            Samples = dataset.Samples.Select(s => new GoldenDatasetSampleExport
            {
                Query = s.Query,
                GroundTruth = s.GroundTruth,
                RelevantDocIds = !string.IsNullOrEmpty(s.RelevantDocumentIdsJson)
                    ? JsonSerializer.Deserialize<List<int>>(s.RelevantDocumentIdsJson)
                    : null,
                ExpectedResponse = s.ExpectedResponse,
                Category = s.Category,
                DifficultyLevel = s.DifficultyLevel,
                ImportanceWeight = s.ImportanceWeight,
                Notes = s.Notes
            }).ToList()
        };

        return JsonSerializer.Serialize(exportData, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    public async Task<List<GoldenDatasetEvaluationRecordDto>> GetEvaluationHistoryAsync(
        string datasetId,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var dataset = await _context.GoldenDatasets
            .FirstOrDefaultAsync(d => d.DatasetId == datasetId, cancellationToken);

        if (dataset == null)
        {
            throw new InvalidOperationException($"Dataset '{datasetId}' not found");
        }

        var query = _context.GoldenDatasetEvaluationRecords
            .Where(r => r.GoldenDatasetId == dataset.Id);

        if (from.HasValue)
            query = query.Where(r => r.EvaluatedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(r => r.EvaluatedAt <= to.Value);

        var records = await query
            .OrderByDescending(r => r.EvaluatedAt)
            .ToListAsync(cancellationToken);

        return records.Select(MapEvaluationRecordToDto).ToList();
    }

    private async Task<GoldenDatasetDto> MapToDto(GoldenDataset dataset, CancellationToken cancellationToken)
    {
        var activeSamplesCount = await _context.GoldenDatasetSamples
            .Where(s => s.GoldenDatasetId == dataset.Id && s.IsActive)
            .CountAsync(cancellationToken);

        var lastEvaluation = await _context.GoldenDatasetEvaluationRecords
            .Where(r => r.GoldenDatasetId == dataset.Id)
            .OrderByDescending(r => r.EvaluatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return new GoldenDatasetDto
        {
            Id = dataset.Id,
            DatasetId = dataset.DatasetId,
            Name = dataset.Name,
            Description = dataset.Description,
            Version = dataset.Version,
            TenantId = dataset.TenantId,
            CreatedBy = dataset.CreatedBy,
            CreatedAt = dataset.CreatedAt,
            UpdatedAt = dataset.UpdatedAt,
            IsActive = dataset.IsActive,
            SampleCount = activeSamplesCount,
            EvaluationCount = dataset.EvaluationRecords.Count,
            LastEvaluationScore = lastEvaluation?.OverallRAGASScore,
            LastEvaluationDate = lastEvaluation?.EvaluatedAt
        };
    }

    private GoldenDatasetSampleDto MapSampleToDto(GoldenDatasetSample sample)
    {
        return new GoldenDatasetSampleDto
        {
            Id = sample.Id,
            GoldenDatasetId = sample.GoldenDatasetId,
            Query = sample.Query,
            GroundTruth = sample.GroundTruth,
            RelevantDocumentIds = !string.IsNullOrEmpty(sample.RelevantDocumentIdsJson)
                ? JsonSerializer.Deserialize<List<int>>(sample.RelevantDocumentIdsJson)
                : null,
            ExpectedResponse = sample.ExpectedResponse,
            Category = sample.Category,
            DifficultyLevel = sample.DifficultyLevel,
            ImportanceWeight = sample.ImportanceWeight,
            Notes = sample.Notes,
            CreatedAt = sample.CreatedAt,
            CreatedBy = sample.CreatedBy,
            IsActive = sample.IsActive
        };
    }

    private GoldenDatasetEvaluationRecordDto MapEvaluationRecordToDto(GoldenDatasetEvaluationRecord record)
    {
        return new GoldenDatasetEvaluationRecordDto
        {
            Id = record.Id,
            GoldenDatasetId = record.GoldenDatasetId,
            EvaluatedAt = record.EvaluatedAt,
            ConfigurationId = record.ConfigurationId,
            TotalSamples = record.TotalSamples,
            EvaluatedSamples = record.EvaluatedSamples,
            FailedSamples = record.FailedSamples,
            AverageFaithfulnessScore = record.AverageFaithfulnessScore,
            AverageAnswerRelevancyScore = record.AverageAnswerRelevancyScore,
            AverageContextPrecisionScore = record.AverageContextPrecisionScore,
            AverageContextRecallScore = record.AverageContextRecallScore,
            OverallRAGASScore = record.OverallRAGASScore,
            AverageConfidenceScore = record.AverageConfidenceScore,
            LowConfidenceRate = record.LowConfidenceRate,
            HallucinationRate = record.HallucinationRate,
            CitationVerificationRate = record.CitationVerificationRate,
            Status = record.Status,
            Notes = record.Notes,
            DurationSeconds = record.DurationSeconds
        };
    }

    // Helper classes for JSON import/export
    private class GoldenDatasetImport
    {
        public List<GoldenDatasetSampleImport>? Samples { get; set; }
    }

    private class GoldenDatasetSampleImport
    {
        public string Query { get; set; } = string.Empty;
        public string GroundTruth { get; set; } = string.Empty;
        public List<int>? RelevantDocIds { get; set; }
        public string? ExpectedResponse { get; set; }
        public string? Category { get; set; }
        public string? DifficultyLevel { get; set; }
        public int? ImportanceWeight { get; set; }
        public string? Notes { get; set; }
    }

    private class GoldenDatasetExport
    {
        public string DatasetId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Version { get; set; } = "1.0";
        public DateTime ExportedAt { get; set; }
        public List<GoldenDatasetSampleExport> Samples { get; set; } = new();
    }

    private class GoldenDatasetSampleExport
    {
        public string Query { get; set; } = string.Empty;
        public string GroundTruth { get; set; } = string.Empty;
        public List<int>? RelevantDocIds { get; set; }
        public string? ExpectedResponse { get; set; }
        public string? Category { get; set; }
        public string DifficultyLevel { get; set; } = "medium";
        public int ImportanceWeight { get; set; } = 5;
        public string? Notes { get; set; }
    }
}
