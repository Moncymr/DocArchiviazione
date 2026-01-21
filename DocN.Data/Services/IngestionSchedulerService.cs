using DocN.Data.Constants;
using DocN.Data.Jobs;
using DocN.Data.Models;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DocN.Data.Services;

/// <summary>
/// Background service that monitors and schedules ingestion tasks
/// </summary>
public class IngestionSchedulerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<IngestionSchedulerService> _logger;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly IRecurringJobManager _recurringJobManager;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);

    public IngestionSchedulerService(
        IServiceProvider serviceProvider,
        ILogger<IngestionSchedulerService> logger,
        IBackgroundJobClient backgroundJobClient,
        IRecurringJobManager recurringJobManager)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _backgroundJobClient = backgroundJobClient;
        _recurringJobManager = recurringJobManager;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Ingestion Scheduler Service started");

        // Initial delay to allow application to fully start
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ScheduleIngestionsAsync();
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Ingestion Scheduler Service");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }

    private async Task ScheduleIngestionsAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DocArcContext>();

        // Get all enabled schedules
        var schedules = await context.IngestionSchedules
            .Where(s => s.IsEnabled)
            .ToListAsync();

        foreach (var schedule in schedules)
        {
            try
            {
                switch (schedule.ScheduleType)
                {
                    case ScheduleTypes.Scheduled:
                        ScheduleCronBasedIngestion(schedule);
                        break;

                    case ScheduleTypes.Continuous:
                        ScheduleContinuousIngestion(schedule);
                        break;

                    case ScheduleTypes.Manual:
                        // Manual schedules are not automatically executed
                        RemoveScheduleJobs(schedule.Id);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scheduling ingestion for schedule {ScheduleId}", schedule.Id);
            }
        }
    }

    private void ScheduleCronBasedIngestion(IngestionSchedule schedule)
    {
        if (string.IsNullOrEmpty(schedule.CronExpression))
        {
            _logger.LogWarning("Schedule {ScheduleId} has type Scheduled but no cron expression", schedule.Id);
            return;
        }

        var jobId = $"ingestion-schedule-{schedule.Id}";
        
        _recurringJobManager.AddOrUpdate<ScheduledIngestionJob>(
            jobId,
            job => job.ExecuteAsync(schedule.Id, schedule.OwnerId ?? "system"),
            schedule.CronExpression,
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });

        _logger.LogDebug("Scheduled cron-based ingestion {JobId} with expression {CronExpression}", 
            jobId, schedule.CronExpression);
    }

    private void ScheduleContinuousIngestion(IngestionSchedule schedule)
    {
        if (!schedule.IntervalMinutes.HasValue || schedule.IntervalMinutes <= 0)
        {
            _logger.LogWarning("Schedule {ScheduleId} has type Continuous but no valid interval", schedule.Id);
            return;
        }

        var jobId = $"ingestion-schedule-{schedule.Id}";
        
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
                    schedule.Id, schedule.IntervalMinutes, hourInterval);
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

        _logger.LogDebug("Scheduled continuous ingestion {JobId} with interval {IntervalMinutes} minutes (cron: {CronExpression})", 
            jobId, schedule.IntervalMinutes, cronExpression);
    }

    private void RemoveScheduleJobs(int scheduleId)
    {
        var jobId = $"ingestion-schedule-{scheduleId}";
        _recurringJobManager.RemoveIfExists(jobId);
        _logger.LogDebug("Removed scheduled job {JobId} for manual or disabled schedule", jobId);
    }


}
