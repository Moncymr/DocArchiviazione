using DocN.Data.Models;

namespace DocN.Data.Services;

/// <summary>
/// Service for managing ingestion schedules and executing ingestion tasks
/// </summary>
public interface IIngestionService
{
    /// <summary>
    /// Gets all ingestion schedules for a user
    /// </summary>
    Task<List<IngestionSchedule>> GetUserSchedulesAsync(string userId);
    
    /// <summary>
    /// Gets a specific ingestion schedule by ID
    /// </summary>
    Task<IngestionSchedule?> GetScheduleAsync(int scheduleId, string userId);
    
    /// <summary>
    /// Creates a new ingestion schedule
    /// </summary>
    Task<IngestionSchedule> CreateScheduleAsync(IngestionSchedule schedule);
    
    /// <summary>
    /// Updates an existing ingestion schedule
    /// </summary>
    Task<IngestionSchedule> UpdateScheduleAsync(IngestionSchedule schedule, string userId);
    
    /// <summary>
    /// Deletes an ingestion schedule
    /// </summary>
    Task<bool> DeleteScheduleAsync(int scheduleId, string userId);
    
    /// <summary>
    /// Executes a manual ingestion for a schedule
    /// </summary>
    Task<IngestionLog> ExecuteIngestionAsync(int scheduleId, string userId);
    
    /// <summary>
    /// Gets ingestion logs for a schedule
    /// </summary>
    Task<List<IngestionLog>> GetIngestionLogsAsync(int scheduleId, string userId, int count = 20);
    
    /// <summary>
    /// Calculates next execution time for a schedule
    /// </summary>
    Task UpdateNextExecutionTimeAsync(int scheduleId);
}
