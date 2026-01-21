using DocN.Data.Services;
using Microsoft.Extensions.Logging;

namespace DocN.Data.Jobs;

/// <summary>
/// Hangfire job for executing scheduled ingestions
/// </summary>
public class ScheduledIngestionJob
{
    private readonly IIngestionService _ingestionService;
    private readonly ILogger<ScheduledIngestionJob> _logger;

    public ScheduledIngestionJob(
        IIngestionService ingestionService,
        ILogger<ScheduledIngestionJob> logger)
    {
        _ingestionService = ingestionService;
        _logger = logger;
    }

    /// <summary>
    /// Executes a scheduled ingestion - called by Hangfire
    /// </summary>
    public async Task ExecuteAsync(int scheduleId, string userId)
    {
        try
        {
            _logger.LogInformation("Starting scheduled ingestion for schedule {ScheduleId}", scheduleId);
            
            var log = await _ingestionService.ExecuteIngestionAsync(scheduleId, userId);
            
            _logger.LogInformation(
                "Completed scheduled ingestion for schedule {ScheduleId}. Status: {Status}, Processed: {Processed}, Skipped: {Skipped}, Failed: {Failed}",
                scheduleId, log.Status, log.DocumentsProcessed, log.DocumentsSkipped, log.DocumentsFailed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing scheduled ingestion for schedule {ScheduleId}", scheduleId);
            throw;
        }
    }
}
