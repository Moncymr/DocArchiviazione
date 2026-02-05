using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DocN.Data.Services;
using DocN.Data.Models;
using System.Security.Claims;

namespace DocN.Server.Controllers;

/// <summary>
/// API Controller for Dashboard management
/// Provides endpoints for widget management and dashboard customization
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IDashboardWidgetService _widgetService;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(
        IDashboardWidgetService widgetService,
        ILogger<DashboardController> logger)
    {
        _widgetService = widgetService;
        _logger = logger;
    }

    /// <summary>
    /// Get all widgets for the current user
    /// </summary>
    /// <returns>List of dashboard widgets</returns>
    [HttpGet("widgets")]
    public async Task<IActionResult> GetWidgets()
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var widgets = await _widgetService.GetUserWidgetsAsync(userId);
            return Ok(widgets);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dashboard widgets");
            return StatusCode(500, new { message = "An error occurred while getting widgets" });
        }
    }

    /// <summary>
    /// Get a specific widget by ID
    /// </summary>
    /// <param name="widgetId">Widget ID</param>
    /// <returns>Dashboard widget</returns>
    [HttpGet("widgets/{widgetId}")]
    public async Task<IActionResult> GetWidget(int widgetId)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var widget = await _widgetService.GetWidgetAsync(widgetId, userId);
            if (widget == null)
            {
                return NotFound(new { message = "Widget not found" });
            }

            return Ok(widget);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting widget {WidgetId}", widgetId);
            return StatusCode(500, new { message = "An error occurred while getting the widget" });
        }
    }

    /// <summary>
    /// Create a new widget for the current user
    /// </summary>
    /// <param name="request">Widget creation request</param>
    /// <returns>Created widget</returns>
    [HttpPost("widgets")]
    public async Task<IActionResult> CreateWidget([FromBody] CreateWidgetRequest request)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var widget = new DashboardWidget
            {
                UserId = userId,
                WidgetType = request.WidgetType,
                Title = request.Title,
                Position = request.Position,
                Configuration = request.Configuration,
                IsVisible = request.IsVisible
            };

            var createdWidget = await _widgetService.CreateWidgetAsync(widget);
            _logger.LogInformation("Widget created: {WidgetId} for user {UserId}", createdWidget.Id, userId);

            return CreatedAtAction(nameof(GetWidget), new { widgetId = createdWidget.Id }, createdWidget);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating widget");
            return StatusCode(500, new { message = "An error occurred while creating the widget" });
        }
    }

    /// <summary>
    /// Update an existing widget
    /// </summary>
    /// <param name="widgetId">Widget ID</param>
    /// <param name="request">Widget update request</param>
    /// <returns>Updated widget</returns>
    [HttpPut("widgets/{widgetId}")]
    public async Task<IActionResult> UpdateWidget(int widgetId, [FromBody] UpdateWidgetRequest request)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var widget = await _widgetService.GetWidgetAsync(widgetId, userId);
            if (widget == null)
            {
                return NotFound(new { message = "Widget not found" });
            }

            widget.Title = request.Title ?? widget.Title;
            widget.Position = request.Position ?? widget.Position;
            widget.Configuration = request.Configuration ?? widget.Configuration;
            widget.IsVisible = request.IsVisible ?? widget.IsVisible;

            var updatedWidget = await _widgetService.UpdateWidgetAsync(widget);
            _logger.LogInformation("Widget updated: {WidgetId}", widgetId);

            return Ok(updatedWidget);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating widget {WidgetId}", widgetId);
            return StatusCode(500, new { message = "An error occurred while updating the widget" });
        }
    }

    /// <summary>
    /// Delete a widget
    /// </summary>
    /// <param name="widgetId">Widget ID</param>
    /// <returns>Success response</returns>
    [HttpDelete("widgets/{widgetId}")]
    public async Task<IActionResult> DeleteWidget(int widgetId)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var widget = await _widgetService.GetWidgetAsync(widgetId, userId);
            if (widget == null)
            {
                return NotFound(new { message = "Widget not found" });
            }

            await _widgetService.DeleteWidgetAsync(widgetId, userId);
            _logger.LogInformation("Widget deleted: {WidgetId}", widgetId);

            return Ok(new { message = "Widget deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting widget {WidgetId}", widgetId);
            return StatusCode(500, new { message = "An error occurred while deleting the widget" });
        }
    }

    /// <summary>
    /// Reorder widgets for the current user
    /// </summary>
    /// <param name="request">Reorder request with widget IDs in desired order</param>
    /// <returns>Success response</returns>
    [HttpPost("widgets/reorder")]
    public async Task<IActionResult> ReorderWidgets([FromBody] ReorderWidgetsRequest request)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            await _widgetService.ReorderWidgetsAsync(userId, request.WidgetIds);
            _logger.LogInformation("Widgets reordered for user {UserId}", userId);

            return Ok(new { message = "Widgets reordered successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reordering widgets");
            return StatusCode(500, new { message = "An error occurred while reordering widgets" });
        }
    }

    /// <summary>
    /// Initialize default widgets for the current user based on their role
    /// </summary>
    /// <returns>List of created default widgets</returns>
    [HttpPost("widgets/initialize-defaults")]
    public async Task<IActionResult> InitializeDefaultWidgets()
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            // Get user's role (assuming first role)
            var userRole = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;
            if (string.IsNullOrEmpty(userRole))
            {
                userRole = "User"; // Default role
            }

            // Get default widgets for the role
            var defaultWidgets = await _widgetService.GetDefaultWidgetsForRole(userRole);

            // Create widgets for the user
            var createdWidgets = new List<DashboardWidget>();
            foreach (var widget in defaultWidgets)
            {
                widget.UserId = userId;
                var created = await _widgetService.CreateWidgetAsync(widget);
                createdWidgets.Add(created);
            }

            _logger.LogInformation("Default widgets initialized for user {UserId}", userId);
            return Ok(createdWidgets);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing default widgets");
            return StatusCode(500, new { message = "An error occurred while initializing default widgets" });
        }
    }

    /// <summary>
    /// Get available widget types
    /// </summary>
    /// <returns>List of available widget types</returns>
    [HttpGet("widget-types")]
    public IActionResult GetWidgetTypes()
    {
        var widgetTypes = new[]
        {
            new { Type = "RecentDocuments", Name = "Recent Documents", Description = "Shows recently uploaded or modified documents" },
            new { Type = "ActivityFeed", Name = "Activity Feed", Description = "Shows recent system activity and user actions" },
            new { Type = "Statistics", Name = "Document Statistics", Description = "Displays document counts and storage information" },
            new { Type = "SavedSearches", Name = "Saved Searches", Description = "Quick access to saved search queries" },
            new { Type = "SystemHealth", Name = "System Health", Description = "Shows system status and health metrics (Admin only)" }
        };

        return Ok(widgetTypes);
    }

    #region Request Models

    public class CreateWidgetRequest
    {
        public string WidgetType { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public int Position { get; set; }
        public string? Configuration { get; set; }
        public bool IsVisible { get; set; } = true;
    }

    public class UpdateWidgetRequest
    {
        public string? Title { get; set; }
        public int? Position { get; set; }
        public string? Configuration { get; set; }
        public bool? IsVisible { get; set; }
    }

    public class ReorderWidgetsRequest
    {
        public List<int> WidgetIds { get; set; } = new();
    }

    #endregion
}
