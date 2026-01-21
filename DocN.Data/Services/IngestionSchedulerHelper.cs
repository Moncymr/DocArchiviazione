using DocN.Data.Constants;
using DocN.Data.Jobs;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DocN.Data.Services;

/// <summary>
/// Servizio helper per schedulazione job ingestion documenti in background tramite Hangfire
/// </summary>
/// <remarks>
/// <para><strong>Scopo:</strong> Gestire creazione, aggiornamento e rimozione job Hangfire per ingestion automatica documenti da connettori</para>
/// 
/// <para><strong>Funzionalità chiave:</strong></para>
/// <list type="bullet">
/// <item><description>Conversione schedule database in recurring job Hangfire</description></item>
/// <item><description>Supporto 3 tipi schedule: Scheduled (cron), Continuous (intervallo), Manual (on-demand)</description></item>
/// <item><description>Generazione automatica espressioni cron da intervalli minuti</description></item>
/// <item><description>Gestione timezone UTC per consistenza cross-region</description></item>
/// <item><description>Enable/disable dinamico job senza restart applicazione</description></item>
/// </list>
/// 
/// <para><strong>Tipi schedule supportati:</strong></para>
/// <list type="bullet">
/// <item><description><strong>Scheduled:</strong> Usa espressione cron personalizzata (es. "0 9 * * MON-FRI" = 9am giorni feriali)</description></item>
/// <item><description><strong>Continuous:</strong> Intervallo fisso in minuti (es. 30 = ogni 30 minuti)</description></item>
/// <item><description><strong>Manual:</strong> Esecuzione solo su trigger manuale (non schedulato automaticamente)</description></item>
/// </list>
/// 
/// <para><strong>Conversione intervalli in cron:</strong></para>
/// <list type="bullet">
/// <item><description>≤59 minuti: */N pattern (es. */15 = ogni 15 minuti)</description></item>
/// <item><description>60-1439 minuti: */H pattern orario (es. */2 = ogni 2 ore)</description></item>
/// <item><description>≥1440 minuti (24h+): Daily pattern "0 0 * * *" (1 volta al giorno)</description></item>
/// </list>
/// 
/// <para><strong>Integrazione Hangfire:</strong> Usa IRecurringJobManager per gestire job persistenti 
/// che sopravvivono a restart applicazione (storage SQL Server/PostgreSQL)</para>
/// </remarks>
public class IngestionSchedulerHelper : IIngestionSchedulerHelper
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IRecurringJobManager _recurringJobManager;
    private readonly ILogger<IngestionSchedulerHelper> _logger;

    /// <summary>
    /// Inizializza una nuova istanza dell'helper schedulazione ingestion
    /// </summary>
    /// <param name="serviceProvider">Service provider per creare scope e risolvere DocArcContext</param>
    /// <param name="recurringJobManager">Manager Hangfire per gestire recurring jobs</param>
    /// <param name="logger">Logger per diagnostica schedulazione</param>
    public IngestionSchedulerHelper(
        IServiceProvider serviceProvider,
        IRecurringJobManager recurringJobManager,
        ILogger<IngestionSchedulerHelper> logger)
    {
        _serviceProvider = serviceProvider;
        _recurringJobManager = recurringJobManager;
        _logger = logger;
    }

    /// <summary>
    /// Crea o aggiorna un job Hangfire per uno schedule ingestion
    /// </summary>
    /// <param name="scheduleId">ID schedule da configurare</param>
    /// <returns>Task completato quando job è stato creato/aggiornato</returns>
    /// <remarks>
    /// <para><strong>Processo configurazione:</strong></para>
    /// <list type="number">
    /// <item><description>Recupera schedule da database</description></item>
    /// <item><description>Se non trovato o disabilitato, rimuove job esistente</description></item>
    /// <item><description>Valida configurazione schedule (cron expression o intervallo)</description></item>
    /// <item><description>Converte intervallo in cron se tipo Continuous</description></item>
    /// <item><description>Crea/aggiorna recurring job Hangfire</description></item>
    /// </list>
    /// 
    /// <para><strong>Naming convention job:</strong> "ingestion-schedule-{scheduleId}" per identificazione univoca</para>
    /// 
    /// <para><strong>Timezone:</strong> Tutti i job usano UTC per evitare problemi DST e deployment multi-region</para>
    /// </remarks>
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

    /// <summary>
    /// Rimuove un job Hangfire schedulato
    /// </summary>
    /// <param name="scheduleId">ID schedule da rimuovere</param>
    /// <remarks>
    /// <para><strong>Use cases:</strong></para>
    /// <list type="bullet">
    /// <item><description>Schedule disabilitato (IsEnabled = false)</description></item>
    /// <item><description>Schedule tipo Manual (non richiede recurring job)</description></item>
    /// <item><description>Schedule eliminato</description></item>
    /// </list>
    /// 
    /// <para><strong>Idempotente:</strong> RemoveIfExists non genera errore se job non esiste</para>
    /// </remarks>
    public void RemoveScheduledJob(int scheduleId)
    {
        var jobId = $"ingestion-schedule-{scheduleId}";
        _recurringJobManager.RemoveIfExists(jobId);
        _logger.LogInformation("Removed scheduled job {JobId}", jobId);
    }
}
