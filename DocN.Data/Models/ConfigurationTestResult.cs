namespace DocN.Data.Models;

/// <summary>
/// Result of testing AI configuration
/// </summary>
public class ConfigurationTestResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<ProviderTestResult> ProviderResults { get; set; } = new();
}

/// <summary>
/// Result of testing a specific AI provider
/// </summary>
public class ProviderTestResult
{
    public string ProviderName { get; set; } = string.Empty;
    public AIProviderType ProviderType { get; set; }
    public bool IsConfigured { get; set; }
    public bool IsValid { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<ServiceTestResult> Services { get; set; } = new();
}

/// <summary>
/// Result of testing a specific service assignment
/// </summary>
public class ServiceTestResult
{
    public string ServiceName { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsHealthy { get; set; }
}

/// <summary>
/// Diagnostic information about AI configurations
/// </summary>
public class DiagnosticResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public int TotalConfigurations { get; set; }
    public AIConfiguration? ActiveConfiguration { get; set; }
    public List<ConfigurationInfo> Configurations { get; set; } = new();
}

/// <summary>
/// Information about a specific configuration
/// </summary>
public class ConfigurationInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<string> ProvidersConfigured { get; set; } = new();
    public string ChatProvider { get; set; } = string.Empty;
    public string EmbeddingsProvider { get; set; } = string.Empty;
    public string TagExtractionProvider { get; set; } = string.Empty;
    public string RAGProvider { get; set; } = string.Empty;
}

/// <summary>
/// Result of reset configuration operation
/// </summary>
public class ResetResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? ConfigurationId { get; set; }
}

/// <summary>
/// Result of set default configuration operation
/// </summary>
public class SetDefaultResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? ConfigurationId { get; set; }
    public string? ConfigurationName { get; set; }
}
