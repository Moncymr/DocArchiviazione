namespace DocN.Data.Services;

/// <summary>
/// Service for scheduling background ingestion jobs
/// </summary>
public interface IIngestionSchedulerHelper
{
    /// <summary>
    /// Schedules or updates a background job for an ingestion schedule
    /// </summary>
    Task ScheduleOrUpdateJobAsync(int scheduleId);
    
    /// <summary>
    /// Removes a scheduled background job
    /// </summary>
    void RemoveScheduledJob(int scheduleId);
}
