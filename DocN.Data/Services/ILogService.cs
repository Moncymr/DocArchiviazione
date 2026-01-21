using DocN.Data.Models;

namespace DocN.Data.Services;

public interface ILogService
{
    Task LogInfoAsync(string category, string message, string? details = null, string? userId = null, string? fileName = null);
    Task LogWarningAsync(string category, string message, string? details = null, string? userId = null, string? fileName = null);
    Task LogErrorAsync(string category, string message, string? details = null, string? userId = null, string? fileName = null, string? stackTrace = null);
    Task LogDebugAsync(string category, string message, string? details = null, string? userId = null, string? fileName = null);
    Task<List<LogEntry>> GetLogsAsync(string? category = null, string? userId = null, DateTime? fromDate = null, int maxRecords = 100);
    Task<List<LogEntry>> GetUploadLogsAsync(string? userId = null, DateTime? fromDate = null, int maxRecords = 100);
}
