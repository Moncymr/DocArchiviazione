using Microsoft.AspNetCore.Mvc;
using DocN.Data.Services;
using Microsoft.AspNetCore.RateLimiting;

namespace DocN.Server.Controllers;

/// <summary>
/// Controller for user activity tracking and retrieval
/// </summary>
[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("api")]
public class UserActivityController : ControllerBase
{
    private readonly IUserActivityService _activityService;
    private readonly ILogger<UserActivityController> _logger;

    public UserActivityController(
        IUserActivityService activityService,
        ILogger<UserActivityController> logger)
    {
        _activityService = activityService;
        _logger = logger;
    }

    /// <summary>
    /// Get user activities for a specific user
    /// </summary>
    [HttpGet("{userId}")]
    public async Task<IActionResult> GetUserActivities(
        string userId,
        [FromQuery] int count = 20)
    {
        try
        {
            var activities = await _activityService.GetUserActivitiesAsync(userId, count);
            return Ok(activities);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user activities for user {UserId}", userId);
            return StatusCode(500, new { error = "Failed to retrieve user activities" });
        }
    }

    /// <summary>
    /// Record a new user activity
    /// </summary>
    [HttpPost("record")]
    public async Task<IActionResult> RecordActivity(
        [FromBody] RecordActivityRequest request)
    {
        try
        {
            await _activityService.RecordActivityAsync(
                request.UserId,
                request.ActivityType,
                request.Description,
                request.DocumentId,
                request.Metadata);
            
            return Ok(new { success = true, message = "Activity recorded successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording activity for user {UserId}", request.UserId);
            return StatusCode(500, new { error = "Failed to record activity" });
        }
    }

    /// <summary>
    /// Get recent document-related activities for a user
    /// </summary>
    [HttpGet("{userId}/documents")]
    public async Task<IActionResult> GetRecentDocumentActivities(
        string userId,
        [FromQuery] int count = 10)
    {
        try
        {
            var activities = await _activityService.GetRecentDocumentActivitiesAsync(userId, count);
            return Ok(activities);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting document activities for user {UserId}", userId);
            return StatusCode(500, new { error = "Failed to retrieve document activities" });
        }
    }

    /// <summary>
    /// Get activity statistics for a user
    /// </summary>
    [HttpGet("{userId}/statistics")]
    public async Task<IActionResult> GetActivityStatistics(
        string userId,
        [FromQuery] int maxActivities = 500)
    {
        try
        {
            // Limit to prevent performance issues
            var limit = Math.Min(maxActivities, 1000);
            var activities = await _activityService.GetUserActivitiesAsync(userId, limit);
            
            var statistics = new
            {
                analyzedActivities = activities.Count,
                limitedTo = limit,
                note = activities.Count == limit ? "Results limited. Increase maxActivities parameter for more data." : "All recent activities analyzed.",
                activityTypeBreakdown = activities
                    .GroupBy(a => a.ActivityType)
                    .Select(g => new { activityType = g.Key, count = g.Count() })
                    .OrderByDescending(x => x.count)
                    .ToList(),
                documentActivities = activities.Count(a => a.DocumentId != null),
                recentActivity = activities.FirstOrDefault()?.CreatedAt,
                mostAccessedDocuments = activities
                    .Where(a => a.DocumentId != null)
                    .GroupBy(a => a.DocumentId)
                    .Select(g => new { documentId = g.Key, count = g.Count() })
                    .OrderByDescending(x => x.count)
                    .Take(5)
                    .ToList()
            };
            
            return Ok(statistics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting activity statistics for user {UserId}", userId);
            return StatusCode(500, new { error = "Failed to retrieve activity statistics" });
        }
    }
}

public record RecordActivityRequest(
    string UserId,
    string ActivityType,
    string Description,
    int? DocumentId = null,
    string? Metadata = null);
