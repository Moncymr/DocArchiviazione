namespace DocN.Core.Interfaces;

/// <summary>
/// Service for managing golden datasets for RAG quality regression testing
/// </summary>
public interface IGoldenDatasetService
{
    /// <summary>
    /// Create a new golden dataset
    /// </summary>
    Task<GoldenDatasetDto> CreateDatasetAsync(
        CreateGoldenDatasetRequest request,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get a golden dataset by ID
    /// </summary>
    Task<GoldenDatasetDto?> GetDatasetAsync(
        string datasetId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// List all golden datasets
    /// </summary>
    Task<List<GoldenDatasetDto>> ListDatasetsAsync(
        int? tenantId = null,
        bool activeOnly = true,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Add a sample to a golden dataset
    /// </summary>
    Task<GoldenDatasetSampleDto> AddSampleAsync(
        string datasetId,
        CreateGoldenDatasetSampleRequest request,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Update a sample in a golden dataset
    /// </summary>
    Task<GoldenDatasetSampleDto> UpdateSampleAsync(
        int sampleId,
        UpdateGoldenDatasetSampleRequest request,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete a sample from a golden dataset
    /// </summary>
    Task DeleteSampleAsync(
        int sampleId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all samples for a dataset
    /// </summary>
    Task<List<GoldenDatasetSampleDto>> GetSamplesAsync(
        string datasetId,
        string? category = null,
        bool activeOnly = true,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Load samples from a JSON file
    /// </summary>
    Task ImportSamplesFromJsonAsync(
        string datasetId,
        string jsonContent,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Export samples to JSON format
    /// </summary>
    Task<string> ExportSamplesToJsonAsync(
        string datasetId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get evaluation history for a dataset
    /// </summary>
    Task<List<GoldenDatasetEvaluationRecordDto>> GetEvaluationHistoryAsync(
        string datasetId,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// DTO for golden dataset
/// </summary>
public class GoldenDatasetDto
{
    public int Id { get; set; }
    public string DatasetId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Version { get; set; } = "1.0";
    public int? TenantId { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsActive { get; set; }
    public int SampleCount { get; set; }
    public int EvaluationCount { get; set; }
    public double? LastEvaluationScore { get; set; }
    public DateTime? LastEvaluationDate { get; set; }
}

/// <summary>
/// DTO for golden dataset sample
/// </summary>
public class GoldenDatasetSampleDto
{
    public int Id { get; set; }
    public int GoldenDatasetId { get; set; }
    public string Query { get; set; } = string.Empty;
    public string GroundTruth { get; set; } = string.Empty;
    public List<int>? RelevantDocumentIds { get; set; }
    public string? ExpectedResponse { get; set; }
    public string? Category { get; set; }
    public string DifficultyLevel { get; set; } = "medium";
    public int ImportanceWeight { get; set; } = 5;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// DTO for golden dataset evaluation record
/// </summary>
public class GoldenDatasetEvaluationRecordDto
{
    public int Id { get; set; }
    public int GoldenDatasetId { get; set; }
    public DateTime EvaluatedAt { get; set; }
    public string? ConfigurationId { get; set; }
    public int TotalSamples { get; set; }
    public int EvaluatedSamples { get; set; }
    public int FailedSamples { get; set; }
    public double AverageFaithfulnessScore { get; set; }
    public double AverageAnswerRelevancyScore { get; set; }
    public double AverageContextPrecisionScore { get; set; }
    public double AverageContextRecallScore { get; set; }
    public double OverallRAGASScore { get; set; }
    public double AverageConfidenceScore { get; set; }
    public double LowConfidenceRate { get; set; }
    public double HallucinationRate { get; set; }
    public double CitationVerificationRate { get; set; }
    public string Status { get; set; } = "success";
    public string? Notes { get; set; }
    public double DurationSeconds { get; set; }
}

/// <summary>
/// Request to create a golden dataset
/// </summary>
public class CreateGoldenDatasetRequest
{
    public string DatasetId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Version { get; set; } = "1.0";
    public int? TenantId { get; set; }
}

/// <summary>
/// Request to create a golden dataset sample
/// </summary>
public class CreateGoldenDatasetSampleRequest
{
    public string Query { get; set; } = string.Empty;
    public string GroundTruth { get; set; } = string.Empty;
    public List<int>? RelevantDocumentIds { get; set; }
    public string? ExpectedResponse { get; set; }
    public string? Category { get; set; }
    public string DifficultyLevel { get; set; } = "medium";
    public int ImportanceWeight { get; set; } = 5;
    public string? Notes { get; set; }
}

/// <summary>
/// Request to update a golden dataset sample
/// </summary>
public class UpdateGoldenDatasetSampleRequest
{
    public string? Query { get; set; }
    public string? GroundTruth { get; set; }
    public List<int>? RelevantDocumentIds { get; set; }
    public string? ExpectedResponse { get; set; }
    public string? Category { get; set; }
    public string? DifficultyLevel { get; set; }
    public int? ImportanceWeight { get; set; }
    public string? Notes { get; set; }
    public bool? IsActive { get; set; }
}
