namespace DocN.Core.Interfaces;

/// <summary>
/// Service for verifying RAG response quality and accuracy
/// </summary>
public interface IRAGQualityService
{
    /// <summary>
    /// Verify the quality of a RAG response
    /// </summary>
    Task<RAGQualityResult> VerifyResponseQualityAsync(
        string query,
        string response,
        IEnumerable<string> sourceDocumentIds,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Calculate confidence score for a response statement
    /// </summary>
    Task<double> CalculateConfidenceScoreAsync(
        string statement,
        IEnumerable<string> sourceTexts,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Detect potential hallucinations in response
    /// </summary>
    Task<HallucinationDetectionResult> DetectHallucinationsAsync(
        string response,
        IEnumerable<string> sourceTexts,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Verify citations in response
    /// </summary>
    Task<CitationVerificationResult> VerifyCitationsAsync(
        string response,
        IEnumerable<string> sourceDocumentIds,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get quality metrics for a time period
    /// </summary>
    Task<RAGQualityMetrics> GetQualityMetricsAsync(
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Log a quality discrepancy for review
    /// </summary>
    Task LogDiscrepancyAsync(
        string query,
        string response,
        string discrepancyType,
        string details,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of RAG quality verification
/// </summary>
public class RAGQualityResult
{
    public double OverallConfidenceScore { get; set; }
    public bool HasLowConfidenceWarnings { get; set; }
    public List<string> LowConfidenceStatements { get; set; } = new();
    public HallucinationDetectionResult HallucinationDetection { get; set; } = new();
    public CitationVerificationResult CitationVerification { get; set; } = new();
    public List<string> QualityWarnings { get; set; } = new();
    public Dictionary<string, double> StatementConfidenceScores { get; set; } = new();
}

/// <summary>
/// Result of hallucination detection
/// </summary>
public class HallucinationDetectionResult
{
    public bool HasPotentialHallucinations { get; set; }
    public List<HallucinationInstance> Hallucinations { get; set; } = new();
    public double HallucinationScore { get; set; }
}

/// <summary>
/// Instance of a potential hallucination
/// </summary>
public class HallucinationInstance
{
    public string Text { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Result of citation verification
/// </summary>
public class CitationVerificationResult
{
    public int TotalCitations { get; set; }
    public int VerifiedCitations { get; set; }
    public int UnverifiedCitations { get; set; }
    public List<CitationInfo> Citations { get; set; } = new();
}

/// <summary>
/// Information about a citation
/// </summary>
public class CitationInfo
{
    public string CitedText { get; set; } = string.Empty;
    public string? SourceDocumentId { get; set; }
    public bool IsVerified { get; set; }
    public double ConfidenceScore { get; set; }
}

/// <summary>
/// RAG quality metrics
/// </summary>
public class RAGQualityMetrics
{
    public int TotalResponses { get; set; }
    public double AverageConfidenceScore { get; set; }
    public int LowConfidenceResponses { get; set; }
    public int HallucinationsDetected { get; set; }
    public double CitationVerificationRate { get; set; }
    public Dictionary<string, int> DiscrepanciesByType { get; set; } = new();
    public List<string> TopWarnings { get; set; } = new();
}
