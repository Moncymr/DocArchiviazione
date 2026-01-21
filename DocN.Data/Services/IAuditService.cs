using DocN.Data.Models;

namespace DocN.Data.Services;

/// <summary>
/// Service for audit logging to meet GDPR/SOC2 compliance requirements
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// Log an audit entry
    /// </summary>
    Task LogAsync(string action, string resourceType, string? resourceId = null, object? details = null, string severity = "Info", bool success = true, string? errorMessage = null);
    
    /// <summary>
    /// Log user authentication events
    /// </summary>
    Task LogAuthenticationAsync(string action, string? userId, string? username, bool success, string? errorMessage = null);
    
    /// <summary>
    /// Log document operations
    /// </summary>
    Task LogDocumentOperationAsync(string action, int documentId, string fileName, object? details = null);
    
    /// <summary>
    /// Log configuration changes
    /// </summary>
    Task LogConfigurationChangeAsync(string action, string configName, object? oldValue, object? newValue);
    
    /// <summary>
    /// Query audit logs with filters
    /// </summary>
    Task<List<AuditLog>> GetAuditLogsAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? userId = null,
        string? action = null,
        string? resourceType = null,
        int page = 1,
        int pageSize = 50);
    
    /// <summary>
    /// Get audit logs count for a user
    /// </summary>
    Task<int> GetUserAuditCountAsync(string userId, DateTime? startDate = null, DateTime? endDate = null);
}
