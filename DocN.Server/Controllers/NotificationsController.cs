using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using DocN.Data.Services;
using DocN.Data.Models;
using System.Security.Claims;

namespace DocN.Server.Controllers;

/// <summary>
/// API endpoints for managing notifications
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(
        INotificationService notificationService,
        ILogger<NotificationsController> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <summary>
    /// Get notifications for the current user
    /// </summary>
    /// <param name="unreadOnly">Filter to show only unread notifications</param>
    /// <param name="skip">Number of notifications to skip for pagination</param>
    /// <param name="take">Number of notifications to retrieve</param>
    /// <returns>List of notifications</returns>
    [HttpGet]
    [ProducesResponseType(typeof(List<Notification>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<Notification>>> GetNotifications(
        [FromQuery] bool? unreadOnly = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var notifications = await _notificationService.GetNotificationsAsync(userId, unreadOnly, skip, take);
            return Ok(notifications);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving notifications");
            return StatusCode(500, new { error = "An error occurred while retrieving notifications" });
        }
    }

    /// <summary>
    /// Get the count of unread notifications for the current user
    /// </summary>
    /// <returns>Count of unread notifications</returns>
    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> GetUnreadCount()
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var count = await _notificationService.GetUnreadCountAsync(userId);
            return Ok(new { count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting unread notification count");
            return StatusCode(500, new { error = "An error occurred while getting unread count" });
        }
    }

    /// <summary>
    /// Mark a specific notification as read
    /// </summary>
    /// <param name="id">Notification ID</param>
    /// <returns>Success status</returns>
    [HttpPost("mark-read/{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var success = await _notificationService.MarkAsReadAsync(id, userId);
            if (!success)
            {
                return NotFound(new { error = "Notification not found" });
            }

            return Ok(new { message = "Notification marked as read" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking notification as read");
            return StatusCode(500, new { error = "An error occurred while marking notification as read" });
        }
    }

    /// <summary>
    /// Mark all notifications as read for the current user
    /// </summary>
    /// <returns>Success status</returns>
    [HttpPost("mark-all-read")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkAllAsRead()
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            await _notificationService.MarkAllAsReadAsync(userId);
            return Ok(new { message = "All notifications marked as read" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking all notifications as read");
            return StatusCode(500, new { error = "An error occurred while marking all notifications as read" });
        }
    }

    /// <summary>
    /// Delete a specific notification
    /// </summary>
    /// <param name="id">Notification ID</param>
    /// <returns>Success status</returns>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteNotification(int id)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var success = await _notificationService.DeleteNotificationAsync(id, userId);
            if (!success)
            {
                return NotFound(new { error = "Notification not found" });
            }

            return Ok(new { message = "Notification deleted" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting notification");
            return StatusCode(500, new { error = "An error occurred while deleting notification" });
        }
    }

    /// <summary>
    /// Get notification preferences for the current user
    /// </summary>
    /// <returns>Notification preferences</returns>
    [HttpGet("preferences")]
    [ProducesResponseType(typeof(NotificationPreference), StatusCodes.Status200OK)]
    public async Task<ActionResult<NotificationPreference>> GetPreferences()
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var preferences = await _notificationService.GetOrCreatePreferenceAsync(userId);
            return Ok(preferences);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving notification preferences");
            return StatusCode(500, new { error = "An error occurred while retrieving preferences" });
        }
    }

    /// <summary>
    /// Update notification preferences for the current user
    /// </summary>
    /// <param name="preferences">Updated preferences</param>
    /// <returns>Updated preferences</returns>
    [HttpPut("preferences")]
    [ProducesResponseType(typeof(NotificationPreference), StatusCodes.Status200OK)]
    public async Task<ActionResult<NotificationPreference>> UpdatePreferences(
        [FromBody] NotificationPreference preferences)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            // Ensure the preference belongs to the current user
            preferences.UserId = userId;

            var updated = await _notificationService.UpdatePreferenceAsync(preferences);
            return Ok(updated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating notification preferences");
            return StatusCode(500, new { error = "An error occurred while updating preferences" });
        }
    }
}
