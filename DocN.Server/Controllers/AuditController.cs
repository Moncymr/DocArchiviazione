using Microsoft.AspNetCore.Mvc;
using DocN.Data.Services;
using DocN.Data.Models;

namespace DocN.Server.Controllers;

/// <summary>
/// API endpoints for audit log access and compliance reporting
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuditController : ControllerBase
{
    private readonly IAuditService _auditService;
    private readonly ILogger<AuditController> _logger;

    public AuditController(
        IAuditService auditService,
        ILogger<AuditController> logger)
    {
        _auditService = auditService;
        _logger = logger;
    }

    /// <summary>
    /// Get audit logs with optional filters
    /// </summary>
    /// <param name="startDate">Start date for filtering (UTC)</param>
    /// <param name="endDate">End date for filtering (UTC)</param>
    /// <param name="userId">Filter by user ID</param>
    /// <param name="action">Filter by action type</param>
    /// <param name="resourceType">Filter by resource type</param>
    /// <param name="page">Page number (default: 1)</param>
    /// <param name="pageSize">Page size (default: 50, max: 100)</param>
    /// <returns>List of audit logs</returns>
    [HttpGet]
    public async Task<ActionResult<List<AuditLog>>> GetAuditLogs(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] string? userId = null,
        [FromQuery] string? action = null,
        [FromQuery] string? resourceType = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            if (pageSize > 100)
                pageSize = 100;

            var logs = await _auditService.GetAuditLogsAsync(
                startDate,
                endDate,
                userId,
                action,
                resourceType,
                page,
                pageSize);

            // Log this audit query itself
            await _auditService.LogAsync(
                "AuditLogsQueried",
                "AuditLog",
                null,
                new { StartDate = startDate, EndDate = endDate, UserId = userId, Action = action, ResourceType = resourceType },
                "Info");

            return Ok(logs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit logs");
            return StatusCode(500, new { error = "Failed to retrieve audit logs" });
        }
    }

    /// <summary>
    /// Get audit log count for a specific user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="startDate">Start date for filtering (UTC)</param>
    /// <param name="endDate">End date for filtering (UTC)</param>
    /// <returns>Count of audit logs</returns>
    [HttpGet("user/{userId}/count")]
    public async Task<ActionResult<int>> GetUserAuditCount(
        string userId,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var count = await _auditService.GetUserAuditCountAsync(userId, startDate, endDate);
            return Ok(new { userId, count, startDate, endDate });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user audit count for user {UserId}", userId);
            return StatusCode(500, new { error = "Failed to get user audit count" });
        }
    }

    /// <summary>
    /// Get audit summary statistics
    /// </summary>
    /// <param name="startDate">Start date for filtering (UTC)</param>
    /// <param name="endDate">End date for filtering (UTC)</param>
    /// <returns>Audit statistics</returns>
    [HttpGet("statistics")]
    public async Task<ActionResult> GetAuditStatistics(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var logs = await _auditService.GetAuditLogsAsync(
                startDate,
                endDate,
                page: 1,
                pageSize: 10000); // Get a large sample for statistics

            var statistics = new
            {
                TotalLogs = logs.Count,
                SuccessfulActions = logs.Count(l => l.Success),
                FailedActions = logs.Count(l => !l.Success),
                UniqueUsers = logs.Select(l => l.UserId).Distinct().Count(),
                ActionBreakdown = logs.GroupBy(l => l.Action)
                    .Select(g => new { Action = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .Take(10),
                ResourceTypeBreakdown = logs.GroupBy(l => l.ResourceType)
                    .Select(g => new { ResourceType = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count),
                SeverityBreakdown = logs.GroupBy(l => l.Severity)
                    .Select(g => new { Severity = g.Key, Count = g.Count() })
            };

            await _auditService.LogAsync(
                "AuditStatisticsQueried",
                "AuditLog",
                null,
                new { StartDate = startDate, EndDate = endDate },
                "Info");

            return Ok(statistics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting audit statistics");
            return StatusCode(500, new { error = "Failed to get audit statistics" });
        }
    }
}
