using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DocN.Data.Models;

namespace DocN.Data.Services;

/// <summary>
/// Implementation of audit logging service for GDPR/SOC2 compliance
/// </summary>
public class AuditService : IAuditService
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuditService> _logger;

    public AuditService(
        ApplicationDbContext context,
        IHttpContextAccessor httpContextAccessor,
        ILogger<AuditService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task LogAsync(
        string action,
        string resourceType,
        string? resourceId = null,
        object? details = null,
        string severity = "Info",
        bool success = true,
        string? errorMessage = null)
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            // Note: Using Identity.Name for both userId and username as ASP.NET Identity 
            // stores the username in the Name claim. For more detailed user info,
            // consider querying the database or using additional claims.
            var userId = httpContext?.User?.Identity?.Name;
            var username = httpContext?.User?.Identity?.Name;
            var ipAddress = httpContext?.Connection?.RemoteIpAddress?.ToString();
            var userAgent = httpContext?.Request?.Headers["User-Agent"].ToString();
            
            // Try to get tenant ID from user claims
            int? tenantId = null;
            var tenantIdClaim = httpContext?.User?.FindFirst("TenantId");
            if (tenantIdClaim != null && int.TryParse(tenantIdClaim.Value, out var tid))
            {
                tenantId = tid;
            }

            var auditLog = new AuditLog
            {
                UserId = userId,
                Username = username,
                Action = action,
                ResourceType = resourceType,
                ResourceId = resourceId,
                Details = details != null ? JsonSerializer.Serialize(details) : null,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                TenantId = tenantId,
                Timestamp = DateTime.UtcNow,
                Severity = severity,
                Success = success,
                ErrorMessage = errorMessage
            };

            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation(
                "Audit log created: {Action} on {ResourceType} {ResourceId} by user {UserId}",
                action, resourceType, resourceId, userId);
        }
        catch (Exception ex)
        {
            // Never throw exceptions from audit logging - log and continue
            _logger.LogError(ex, "Failed to create audit log for action {Action}", action);
        }
    }

    public async Task LogAuthenticationAsync(
        string action,
        string? userId,
        string? username,
        bool success,
        string? errorMessage = null)
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var ipAddress = httpContext?.Connection?.RemoteIpAddress?.ToString();
            var userAgent = httpContext?.Request?.Headers["User-Agent"].ToString();

            var auditLog = new AuditLog
            {
                UserId = userId,
                Username = username,
                Action = action,
                ResourceType = "Authentication",
                IpAddress = ipAddress,
                UserAgent = userAgent,
                Timestamp = DateTime.UtcNow,
                Severity = success ? "Info" : "Warning",
                Success = success,
                ErrorMessage = errorMessage
            };

            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation(
                "Authentication audit log: {Action} for user {Username}, success: {Success}",
                action, username, success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create authentication audit log");
        }
    }

    public async Task LogDocumentOperationAsync(
        string action,
        int documentId,
        string fileName,
        object? details = null)
    {
        var detailsObj = new
        {
            FileName = fileName,
            AdditionalDetails = details
        };
        
        await LogAsync(
            action,
            "Document",
            documentId.ToString(),
            detailsObj,
            "Info");
    }

    public async Task LogConfigurationChangeAsync(
        string action,
        string configName,
        object? oldValue,
        object? newValue)
    {
        var details = new
        {
            ConfigurationName = configName,
            OldValue = oldValue,
            NewValue = newValue
        };
        
        await LogAsync(
            action,
            "Configuration",
            configName,
            details,
            "Warning"); // Configuration changes are important
    }

    public async Task<List<AuditLog>> GetAuditLogsAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? userId = null,
        string? action = null,
        string? resourceType = null,
        int page = 1,
        int pageSize = 50)
    {
        var query = _context.AuditLogs.AsQueryable();

        if (startDate.HasValue)
            query = query.Where(a => a.Timestamp >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(a => a.Timestamp <= endDate.Value);

        if (!string.IsNullOrEmpty(userId))
            query = query.Where(a => a.UserId == userId);

        if (!string.IsNullOrEmpty(action))
            query = query.Where(a => a.Action == action);

        if (!string.IsNullOrEmpty(resourceType))
            query = query.Where(a => a.ResourceType == resourceType);

        return await query
            .OrderByDescending(a => a.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<int> GetUserAuditCountAsync(
        string userId,
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        var query = _context.AuditLogs.Where(a => a.UserId == userId);

        if (startDate.HasValue)
            query = query.Where(a => a.Timestamp >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(a => a.Timestamp <= endDate.Value);

        return await query.CountAsync();
    }
}
