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
/// Servizio di background che monitora e schedula task di ingestion documenti automatica
/// </summary>
/// <remarks>
/// <para><strong>Scopo:</strong> Background service che sincronizza schedule database con job Hangfire recurring</para>
/// 
/// <para><strong>Funzionalità chiave:</strong></para>
/// <list type="bullet">
/// <item><description>Polling ogni 1 minuto per verificare schedule abilitati</description></item>
/// <item><description>Creazione/aggiornamento automatico recurring job Hangfire</description></item>
/// <item><description>Supporto schedule cron-based e interval-based</description></item>
/// <item><description>Rimozione automatica job per schedule disabilitati/manual</description></item>
/// <item><description>Delay iniziale 10 secondi per permettere startup completo applicazione</description></item>
/// </list>
/// 
/// <para><strong>Differenze con IngestionSchedulerHelper:</strong></para>
/// <list type="bullet">
/// <item><description><strong>IngestionSchedulerHelper:</strong> Logica helper riutilizzabile per single schedule (on-demand)</description></item>
/// <item><description><strong>IngestionSchedulerService:</strong> Background service che monitora TUTTI gli schedule (polling)</description></item>
/// </list>
/// 
/// <para><strong>Pattern polling:</strong></para>
/// <list type="number">
/// <item><description>Attende 10 secondi dopo startup (permette DB inizializzazione)</description></item>
/// <item><description>Query tutti schedule abilitati da database</description></item>
/// <item><description>Crea/aggiorna recurring job Hangfire per ciascuno</description></item>
/// <item><description>Attende 1 minuto prima del prossimo check</description></item>
/// <item><description>Su errore, attende 5 minuti prima di riprovare (circuit breaker semplice)</description></item>
/// </list>
/// 
/// <para><strong>Integrazione Hangfire:</strong> Usa IRecurringJobManager per gestire job persistenti.
/// Job creati sopravvivono a restart applicazione (storage SQL/PostgreSQL)</para>
/// </remarks>
public class IngestionSchedulerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<IngestionSchedulerService> _logger;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly IRecurringJobManager _recurringJobManager;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Inizializza una nuova istanza del servizio scheduler ingestion
    /// </summary>
    /// <param name="serviceProvider">Service provider per creare scope e risolvere DocArcContext</param>
    /// <param name="logger">Logger per diagnostica scheduling</param>
    /// <param name="backgroundJobClient">Client Hangfire per fire-and-forget jobs (attualmente non usato)</param>
    /// <param name="recurringJobManager">Manager Hangfire per gestire recurring jobs</param>
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

    /// <summary>
    /// Esegue il ciclo principale di monitoraggio schedule
    /// </summary>
    /// <param name="stoppingToken">Token per cancellazione durante shutdown applicazione</param>
    /// <returns>Task completato quando il servizio viene arrestato</returns>
    /// <remarks>
    /// <para><strong>Ciclo esecuzione:</strong></para>
    /// <list type="number">
    /// <item><description>Delay iniziale 10 secondi (permette startup completo)</description></item>
    /// <item><description>Loop infinito fino a cancellazione</description></item>
    /// <item><description>ScheduleIngestionsAsync() per processare schedule</description></item>
    /// <item><description>Attesa 1 minuto prima del prossimo check</description></item>
    /// <item><description>Su errore, attesa 5 minuti (backoff semplice)</description></item>
    /// </list>
    /// </remarks>
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

    /// <summary>
    /// Processa tutti gli schedule abilitati e crea/aggiorna job Hangfire
    /// </summary>
    /// <returns>Task completato quando tutti gli schedule sono stati processati</returns>
    /// <remarks>
    /// <para><strong>Processo:</strong></para>
    /// <list type="number">
    /// <item><description>Query schedule abilitati (IsEnabled = true)</description></item>
    /// <item><description>Per ogni schedule, delega a metodo specifico per tipo</description></item>
    /// <item><description>Errori su singolo schedule vengono loggati ma non bloccano gli altri</description></item>
    /// </list>
    /// 
    /// <para><strong>Tipi schedule:</strong></para>
    /// <list type="bullet">
    /// <item><description>Scheduled → ScheduleCronBasedIngestion()</description></item>
    /// <item><description>Continuous → ScheduleContinuousIngestion()</description></item>
    /// <item><description>Manual → RemoveScheduleJobs() (non auto-scheduled)</description></item>
    /// </list>
    /// </remarks>
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

    /// <summary>
    /// Schedula ingestion con espressione cron personalizzata
    /// </summary>
    /// <param name="schedule">Schedule con CronExpression configurato</param>
    /// <remarks>
    /// <para><strong>Validazione:</strong> Verifica che CronExpression non sia vuoto</para>
    /// <para><strong>Esempi cron:</strong></para>
    /// <list type="bullet">
    /// <item><description>"0 9 * * MON-FRI" - 9am giorni feriali</description></item>
    /// <item><description>"0 0 * * 0" - Mezzanotte ogni domenica</description></item>
    /// <item><description>"*/15 * * * *" - Ogni 15 minuti</description></item>
    /// </list>
    /// </remarks>
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

    /// <summary>
    /// Schedula ingestion continua con intervallo fisso in minuti
    /// </summary>
    /// <param name="schedule">Schedule con IntervalMinutes configurato</param>
    /// <remarks>
    /// <para><strong>Conversione intervallo in cron:</strong></para>
    /// <list type="bullet">
    /// <item><description>1-59 minuti: "*/N * * * *" (es. */15 = ogni 15 min)</description></item>
    /// <item><description>60-1439 minuti: "0 */H * * *" (es. */2 = ogni 2 ore)</description></item>
    /// <item><description>1440+ minuti: "0 0 * * *" (daily a mezzanotte)</description></item>
    /// </list>
    /// 
    /// <para><strong>Limitazione:</strong> Intervalli >23 ore vengono convertiti in daily per semplicità</para>
    /// </remarks>
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

    /// <summary>
    /// Rimuove job Hangfire per schedule manual o disabilitato
    /// </summary>
    /// <param name="scheduleId">ID schedule di cui rimuovere job</param>
    /// <remarks>
    /// <para><strong>Use case:</strong> Schedule tipo Manual non richiedono recurring job automatico,
    /// vengono eseguiti solo su trigger manuale (API call o UI button)</para>
    /// </remarks>
    private void RemoveScheduleJobs(int scheduleId)
    {
        var jobId = $"ingestion-schedule-{scheduleId}";
        _recurringJobManager.RemoveIfExists(jobId);
        _logger.LogDebug("Removed scheduled job {JobId} for manual or disabled schedule", jobId);
    }


}
