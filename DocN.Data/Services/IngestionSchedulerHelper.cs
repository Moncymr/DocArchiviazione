using DocN.Data.Constants;
using DocN.Data.Jobs;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DocN.Data.Services;

/// <summary>
/// Helper service for scheduling background ingestion jobs
/// </summary>
public class IngestionSchedulerHelper : IIngestionSchedulerHelper
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IRecurringJobManager _recurringJobManager;
    private readonly ILogger<IngestionSchedulerHelper> _logger;

    public IngestionSchedulerHelper(
        IServiceProvider serviceProvider,
        IRecurringJobManager recurringJobManager,
        ILogger<IngestionSchedulerHelper> logger)
    {
        _serviceProvider = serviceProvider;
        _recurringJobManager = recurringJobManager;
        _logger = logger;
    }

    public async Task ScheduleOrUpdateJobAsync(int scheduleId)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DocArcContext>();

        var schedule = await context.IngestionSchedules.FindAsync(scheduleId);
        if (schedule == null)
        {
            _logger.LogWarning("Schedule {ScheduleId} not found", scheduleId);
            return;
        }

        if (!schedule.IsEnabled)
        {
            RemoveScheduledJob(scheduleId);
            return;
        }

        var jobId = $"ingestion-schedule-{schedule.Id}";

        switch (schedule.ScheduleType)
        {
            case ScheduleTypes.Scheduled:
                if (string.IsNullOrEmpty(schedule.CronExpression))
                {
                    _logger.LogWarning("Schedule {ScheduleId} has type Scheduled but no cron expression", scheduleId);
                    return;
                }

                _recurringJobManager.AddOrUpdate<ScheduledIngestionJob>(
                    jobId,
                    job => job.ExecuteAsync(schedule.Id, schedule.OwnerId ?? "system"),
                    schedule.CronExpression,
                    new RecurringJobOptions
                    {
                        TimeZone = TimeZoneInfo.Utc
                    });

                _logger.LogInformation("Scheduled cron-based ingestion {JobId} with expression {CronExpression}", 
                    jobId, schedule.CronExpression);
                break;

            case ScheduleTypes.Continuous:
                if (!schedule.IntervalMinutes.HasValue || schedule.IntervalMinutes <= 0)
                {
                    _logger.LogWarning("Schedule {ScheduleId} has type Continuous but no valid interval", scheduleId);
                    return;
                }

                // Convert interval to cron expression
                // For intervals <= 59 minutes, use */N pattern in minutes field
                // For intervals >= 60 minutes, use hourly pattern with calculated hour interval
                string cronExpression;
                if (schedule.IntervalMinutes <= 59)
                {
                    cronExpression = $"*/{schedule.IntervalMinutes} * * * *";
                }
                else
                {
                    // For intervals >= 60 minutes, convert to hours
                    // Round up to nearest hour for simplicity
                    int hourInterval = (int)Math.Ceiling(schedule.IntervalMinutes.Value / 60.0);
                    if (hourInterval > 23)
                    {
                        // For intervals > 23 hours, run daily
                        _logger.LogWarning("Schedule {ScheduleId} has interval {IntervalMinutes} minutes (>{HourInterval} hours), converting to daily execution", 
                            scheduleId, schedule.IntervalMinutes, hourInterval);
                        cronExpression = "0 0 * * *"; // Daily at midnight
                    }
                    else
                    {
                        cronExpression = $"0 */{hourInterval} * * *";
                    }
                }
                
                _recurringJobManager.AddOrUpdate<ScheduledIngestionJob>(
                    jobId,
                    job => job.ExecuteAsync(schedule.Id, schedule.OwnerId ?? "system"),
                    cronExpression,
                    new RecurringJobOptions
                    {
                        TimeZone = TimeZoneInfo.Utc
                    });

                _logger.LogInformation("Scheduled continuous ingestion {JobId} with interval {IntervalMinutes} minutes (cron: {CronExpression})", 
                    jobId, schedule.IntervalMinutes, cronExpression);
                break;

            case ScheduleTypes.Manual:
                // Manual schedules are not automatically executed
                RemoveScheduledJob(scheduleId);
                break;
        }
    }

    public void RemoveScheduledJob(int scheduleId)
    {
        var jobId = $"ingestion-schedule-{scheduleId}";
        _recurringJobManager.RemoveIfExists(jobId);
        _logger.LogInformation("Removed scheduled job {JobId}", jobId);
    }
}
