using Microsoft.AspNetCore.Mvc;
using DocN.Data.Services;
using DocN.Data.Models;

namespace DocN.Server.Controllers;

/// <summary>
/// API Controller for managing ingestion schedules
/// </summary>
[ApiController]
[Route("[controller]")]
public class IngestionController : ControllerBase
{
    private readonly IIngestionService _ingestionService;
    private readonly ILogger<IngestionController> _logger;

    public IngestionController(IIngestionService ingestionService, ILogger<IngestionController> logger)
    {
        _ingestionService = ingestionService;
        _logger = logger;
    }

    /// <summary>
    /// Gets all ingestion schedules for the current user
    /// </summary>
    [HttpGet("schedules")]
    public async Task<ActionResult<IEnumerable<IngestionSchedule>>> GetSchedules()
    {
        try
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "system";
            
            var schedules = await _ingestionService.GetUserSchedulesAsync(userId);
            return Ok(schedules);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving schedules");
            return StatusCode(500, "An error occurred while retrieving schedules");
        }
    }

    /// <summary>
    /// Gets a specific ingestion schedule by ID
    /// </summary>
    [HttpGet("schedules/{id}")]
    public async Task<ActionResult<IngestionSchedule>> GetSchedule(int id)
    {
        try
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "system";
            
            var schedule = await _ingestionService.GetScheduleAsync(id, userId);
            if (schedule == null)
            {
                return NotFound();
            }
            
            return Ok(schedule);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving schedule {ScheduleId}", id);
            return StatusCode(500, "An error occurred while retrieving the schedule");
        }
    }

    /// <summary>
    /// Creates a new ingestion schedule
    /// </summary>
    [HttpPost("schedules")]
    public async Task<ActionResult<IngestionSchedule>> CreateSchedule(IngestionSchedule schedule)
    {
        try
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "system";
            
            schedule.OwnerId = userId;
            var created = await _ingestionService.CreateScheduleAsync(schedule);
            
            return CreatedAtAction(nameof(GetSchedule), new { id = created.Id }, created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating schedule");
            return StatusCode(500, "An error occurred while creating the schedule");
        }
    }

    /// <summary>
    /// Updates an existing ingestion schedule
    /// </summary>
    [HttpPut("schedules/{id}")]
    public async Task<ActionResult<IngestionSchedule>> UpdateSchedule(int id, IngestionSchedule schedule)
    {
        try
        {
            if (id != schedule.Id)
            {
                return BadRequest("ID mismatch");
            }
            
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "system";
            
            var updated = await _ingestionService.UpdateScheduleAsync(schedule, userId);
            return Ok(updated);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating schedule {ScheduleId}", id);
            return StatusCode(500, "An error occurred while updating the schedule");
        }
    }

    /// <summary>
    /// Deletes an ingestion schedule
    /// </summary>
    [HttpDelete("schedules/{id}")]
    public async Task<IActionResult> DeleteSchedule(int id)
    {
        try
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "system";
            
            var result = await _ingestionService.DeleteScheduleAsync(id, userId);
            if (!result)
            {
                return NotFound();
            }
            
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting schedule {ScheduleId}", id);
            return StatusCode(500, "An error occurred while deleting the schedule");
        }
    }

    /// <summary>
    /// Executes a manual ingestion for a schedule
    /// </summary>
    [HttpPost("schedules/{id}/execute")]
    public async Task<ActionResult<IngestionLog>> ExecuteIngestion(int id)
    {
        try
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "system";
            
            var log = await _ingestionService.ExecuteIngestionAsync(id, userId);
            return Ok(log);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing ingestion for schedule {ScheduleId}", id);
            return StatusCode(500, "An error occurred while executing ingestion");
        }
    }

    /// <summary>
    /// Gets ingestion logs for a schedule
    /// </summary>
    [HttpGet("schedules/{id}/logs")]
    public async Task<ActionResult<IEnumerable<IngestionLog>>> GetIngestionLogs(int id, [FromQuery] int count = 20)
    {
        try
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "system";
            
            var logs = await _ingestionService.GetIngestionLogsAsync(id, userId, count);
            return Ok(logs);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving logs for schedule {ScheduleId}", id);
            return StatusCode(500, "An error occurred while retrieving logs");
        }
    }
}
