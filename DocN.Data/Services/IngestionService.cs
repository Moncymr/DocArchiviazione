using DocN.Data;
using DocN.Data.Models;
using DocN.Data.Constants;
using DocN.Data.Services.Connectors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Cronos;
using System.Security.Cryptography;

namespace DocN.Data.Services;

/// <summary>
/// Servizio per gestione schedule di ingestion e esecuzione task di importazione documenti.
/// </summary>
/// <remarks>
/// Responsabilità:
/// - Gestione CRUD schedule ingestion con supporto cron e intervalli
/// - Esecuzione ingestion da connettori esterni (SharePoint, OneDrive, etc.)
/// - Tracciamento log e metriche per ogni esecuzione
/// - Deduplicazione documenti basata su hash e percorso sorgente
/// - Integrazione con Hangfire per scheduling automatico
/// </remarks>
public class IngestionService : IIngestionService
{
    private readonly DocArcContext _context;
    private readonly ApplicationDbContext _appContext;
    private readonly ILogger<IngestionService> _logger;
    private readonly IConnectorService _connectorService;
    private readonly ConnectorHandlerFactory _handlerFactory;
    private readonly IIngestionSchedulerHelper? _schedulerHelper;

    /// <summary>
    /// Costruttore con dependency injection delle dipendenze richieste.
    /// </summary>
    /// <param name="context">Contesto database DocArc per schedule e log</param>
    /// <param name="appContext">Contesto database applicazione per utenti</param>
    /// <param name="logger">Logger per diagnostica e monitoraggio</param>
    /// <param name="connectorService">Servizio per interazione con connettori</param>
    /// <param name="schedulerHelper">Helper opzionale per integrazione Hangfire</param>
    public IngestionService(
        DocArcContext context,
        ApplicationDbContext appContext,
        ILogger<IngestionService> logger,
        IConnectorService connectorService,
        IIngestionSchedulerHelper? schedulerHelper = null)
    {
        _context = context;
        _appContext = appContext;
        _logger = logger;
        _connectorService = connectorService;
        _handlerFactory = new ConnectorHandlerFactory(logger);
        _schedulerHelper = schedulerHelper;
    }

    /// <summary>
    /// Recupera tutti gli schedule di ingestion per un utente specifico.
    /// </summary>
    /// <param name="userId">ID utente proprietario degli schedule</param>
    /// <returns>Lista schedule ordinati per data creazione (più recenti prima)</returns>
    /// <remarks>
    /// Include dati del connettore associato tramite eager loading.
    /// </remarks>
    public async Task<List<IngestionSchedule>> GetUserSchedulesAsync(string userId)
    {
        try
        {
            return await _context.IngestionSchedules
                .Include(s => s.Connector)
                .Where(s => s.OwnerId == userId)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving schedules for user {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Recupera un singolo schedule di ingestion verificando ownership utente.
    /// </summary>
    /// <param name="scheduleId">ID dello schedule da recuperare</param>
    /// <param name="userId">ID utente per verifica autorizzazione</param>
    /// <returns>Schedule trovato o null se non esistente/non autorizzato</returns>
    /// <remarks>
    /// Include dati del connettore associato tramite eager loading.
    /// Filtro su OwnerId previene accesso non autorizzato cross-utente.
    /// </remarks>
    public async Task<IngestionSchedule?> GetScheduleAsync(int scheduleId, string userId)
    {
        try
        {
            return await _context.IngestionSchedules
                .Include(s => s.Connector)
                .FirstOrDefaultAsync(s => s.Id == scheduleId && s.OwnerId == userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving schedule {ScheduleId} for user {UserId}", scheduleId, userId);
            throw;
        }
    }

    /// <summary>
    /// Crea un nuovo schedule di ingestion con calcolo automatico prossima esecuzione.
    /// </summary>
    /// <param name="schedule">Dati schedule da creare</param>
    /// <returns>Schedule creato con ID assegnato dal database</returns>
    /// <remarks>
    /// Operazioni eseguite:
    /// 1. Imposta timestamp CreatedAt e UpdatedAt a UTC corrente
    /// 2. Calcola NextExecutionAt da CronExpression se schedule è abilitato e di tipo Scheduled
    /// 3. Persiste su database
    /// 4. Registra job in Hangfire per esecuzione automatica (se schedulerHelper disponibile)
    /// </remarks>
    public async Task<IngestionSchedule> CreateScheduleAsync(IngestionSchedule schedule)
    {
        try
        {
            schedule.CreatedAt = DateTime.UtcNow;
            schedule.UpdatedAt = DateTime.UtcNow;
            
            // Calculate next execution time if schedule is enabled
            if (schedule.IsEnabled && schedule.ScheduleType == ScheduleTypes.Scheduled)
            {
                schedule.NextExecutionAt = CalculateNextExecutionTime(schedule.CronExpression);
            }
            
            _context.IngestionSchedules.Add(schedule);
            await _context.SaveChangesAsync();
            
            // Schedule the job in Hangfire
            if (_schedulerHelper != null)
            {
                await _schedulerHelper.ScheduleOrUpdateJobAsync(schedule.Id);
            }
            
            _logger.LogInformation("Created ingestion schedule {ScheduleId} for user {UserId}", schedule.Id, schedule.OwnerId);
            return schedule;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating schedule for user {UserId}", schedule.OwnerId);
            throw;
        }
    }

    /// <summary>
    /// Aggiorna uno schedule esistente verificando ownership utente.
    /// </summary>
    /// <param name="schedule">Dati aggiornati dello schedule</param>
    /// <param name="userId">ID utente per verifica autorizzazione</param>
    /// <returns>Schedule aggiornato</returns>
    /// <exception cref="UnauthorizedAccessException">Se schedule non trovato o utente non autorizzato</exception>
    /// <remarks>
    /// Campi aggiornabili: Name, ScheduleType, CronExpression, IntervalMinutes, IsEnabled,
    /// DefaultCategory, DefaultVisibility, FilterConfiguration, GenerateEmbeddingsImmediately,
    /// EnableAIAnalysis, Description.
    /// Ricalcola NextExecutionAt se schedule abilitato e tipo Scheduled.
    /// Aggiorna job in Hangfire per riflettere modifiche scheduling.
    /// </remarks>
    public async Task<IngestionSchedule> UpdateScheduleAsync(IngestionSchedule schedule, string userId)
    {
        try
        {
            var existing = await _context.IngestionSchedules
                .FirstOrDefaultAsync(s => s.Id == schedule.Id && s.OwnerId == userId);
            
            if (existing == null)
            {
                throw new UnauthorizedAccessException("Schedule not found or access denied");
            }
            
            existing.Name = schedule.Name;
            existing.ScheduleType = schedule.ScheduleType;
            existing.CronExpression = schedule.CronExpression;
            existing.IntervalMinutes = schedule.IntervalMinutes;
            existing.IsEnabled = schedule.IsEnabled;
            existing.DefaultCategory = schedule.DefaultCategory;
            existing.DefaultVisibility = schedule.DefaultVisibility;
            existing.FilterConfiguration = schedule.FilterConfiguration;
            existing.GenerateEmbeddingsImmediately = schedule.GenerateEmbeddingsImmediately;
            existing.EnableAIAnalysis = schedule.EnableAIAnalysis;
            existing.Description = schedule.Description;
            existing.UpdatedAt = DateTime.UtcNow;
            
            // Recalculate next execution time if schedule is enabled and type is Scheduled
            if (existing.IsEnabled && existing.ScheduleType == ScheduleTypes.Scheduled)
            {
                existing.NextExecutionAt = CalculateNextExecutionTime(existing.CronExpression);
            }
            else
            {
                existing.NextExecutionAt = null;
            }
            
            await _context.SaveChangesAsync();
            
            // Reschedule the job in Hangfire
            if (_schedulerHelper != null)
            {
                await _schedulerHelper.ScheduleOrUpdateJobAsync(existing.Id);
            }
            
            _logger.LogInformation("Updated schedule {ScheduleId}", schedule.Id);
            return existing;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating schedule {ScheduleId}", schedule.Id);
            throw;
        }
    }

    /// <summary>
    /// Elimina uno schedule di ingestion verificando ownership utente.
    /// </summary>
    /// <param name="scheduleId">ID dello schedule da eliminare</param>
    /// <param name="userId">ID utente per verifica autorizzazione</param>
    /// <returns>true se eliminato con successo, false se non trovato</returns>
    /// <remarks>
    /// Rimuove il job schedulato da Hangfire prima di eliminare dal database.
    /// </remarks>
    public async Task<bool> DeleteScheduleAsync(int scheduleId, string userId)
    {
        try
        {
            var schedule = await _context.IngestionSchedules
                .FirstOrDefaultAsync(s => s.Id == scheduleId && s.OwnerId == userId);
            
            if (schedule == null)
            {
                return false;
            }
            
            // Remove the scheduled job from Hangfire
            _schedulerHelper?.RemoveScheduledJob(scheduleId);
            
            _context.IngestionSchedules.Remove(schedule);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Deleted schedule {ScheduleId}", scheduleId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting schedule {ScheduleId}", scheduleId);
            throw;
        }
    }

    /// <summary>
    /// Esegue manualmente un'ingestion da uno schedule specifico.
    /// </summary>
    /// <param name="scheduleId">ID dello schedule da eseguire</param>
    /// <param name="userId">ID utente che avvia l'esecuzione manuale</param>
    /// <returns>Log ingestion con risultati e statistiche</returns>
    /// <exception cref="UnauthorizedAccessException">Se schedule non trovato o utente non autorizzato</exception>
    /// <exception cref="InvalidOperationException">Se connettore non trovato</exception>
    /// <remarks>
    /// Workflow completo:
    /// 1. Crea log ingestion con status Running
    /// 2. Recupera schedule e connettore
    /// 3. Valida esistenza owner in AspNetUsers (per integrità FK)
    /// 4. Lista file da connettore
    /// 5. Filtra cartelle e file già processati (deduplicazione)
    /// 6. Scarica file non duplicati
    /// 7. Calcola hash SHA256 per ogni file
    /// 8. Crea entry Document nel database
    /// 9. Batch save ogni 20 documenti per performance
    /// 10. Aggiorna statistiche schedule e connettore
    /// 
    /// OTTIMIZZAZIONI:
    /// - Batch duplicate detection con dizionario in-memory
    /// - Batch save documenti ogni 20 record
    /// - AsNoTracking su query read-only esistenti documenti
    /// - Deduplicazione per path e hash file
    /// </remarks>
    public async Task<IngestionLog> ExecuteIngestionAsync(int scheduleId, string userId)
    {
        var log = new IngestionLog
        {
            IngestionScheduleId = scheduleId,
            StartedAt = DateTime.UtcNow,
            Status = IngestionStatus.Running,
            IsManualExecution = true,
            TriggeredByUserId = userId
        };
        
        try
        {
            _context.IngestionLogs.Add(log);
            await _context.SaveChangesAsync();
            
            var schedule = await GetScheduleAsync(scheduleId, userId);
            if (schedule == null)
            {
                throw new UnauthorizedAccessException("Schedule not found or access denied");
            }
            
            _logger.LogInformation("Starting manual ingestion for schedule {ScheduleId}", scheduleId);
            
            // Get connector details
            var connector = await _context.DocumentConnectors
                .FirstOrDefaultAsync(c => c.Id == schedule.ConnectorId);
            
            if (connector == null)
            {
                throw new InvalidOperationException("Connector not found");
            }
            
            // Validate that schedule owner exists in AspNetUsers table
            // If not, we'll use null to avoid foreign key constraint violation
            string? validatedOwnerId = null;
            if (!string.IsNullOrEmpty(schedule.OwnerId))
            {
                var ownerExists = await UserExistsAsync(schedule.OwnerId);
                if (ownerExists)
                {
                    validatedOwnerId = schedule.OwnerId;
                    _logger.LogDebug("Schedule owner {OwnerId} validated successfully", schedule.OwnerId);
                }
                else
                {
                    _logger.LogWarning("Schedule owner {OwnerId} does not exist in AspNetUsers table. Documents will be created with null OwnerId.", schedule.OwnerId);
                }
            }
            
            // Get files from connector
            var files = await _connectorService.ListFilesAsync(schedule.ConnectorId, userId);
            log.DocumentsDiscovered = files.Count;
            await _context.SaveChangesAsync();
            
            // Skip folders
            var nonFolderFiles = files.Where(f => !f.IsFolder).ToList();
            log.DocumentsSkipped += files.Count - nonFolderFiles.Count;
            
            // Batch duplicate detection - get all file paths from this connector
            // OTTIMIZZAZIONE: AsNoTracking per query read-only, non serve tracking EF
            var filePaths = nonFolderFiles.Select(f => f.Path).ToList();
            var existingDocuments = await _context.Documents
                .AsNoTracking()
                .Where(d => d.SourceConnectorId == connector.Id && filePaths.Contains(d.SourceFilePath))
                .Select(d => new { d.SourceFilePath, d.SourceLastModified, d.SourceFileHash })
                .ToListAsync();
            
            // OTTIMIZZAZIONE: Crea dizionario in-memory per lookup O(1) invece di query ripetute
            var existingDocumentsByPath = existingDocuments.ToDictionary(d => d.SourceFilePath ?? "");
            
            // Get connector handler for file download
            var handler = _handlerFactory.CreateHandler(connector.ConnectorType);
            
            // Get upload directory for storing files
            var uploadDirectory = GetUploadDirectory();
            Directory.CreateDirectory(uploadDirectory);
            
            // Batch for saving documents
            var documentsToAdd = new List<Document>();
            
            // Process each file
            foreach (var file in nonFolderFiles)
            {
                try
                {
                    // Check if file already exists using in-memory dictionary
                    if (existingDocumentsByPath.TryGetValue(file.Path, out var existingDoc))
                    {
                        // Check if file has been modified
                        if (file.ModifiedDate.HasValue && 
                            existingDoc.SourceLastModified.HasValue &&
                            file.ModifiedDate.Value <= existingDoc.SourceLastModified.Value)
                        {
                            // File hasn't changed, skip it
                            _logger.LogDebug("Skipping unchanged file: {FilePath}", file.Path);
                            log.DocumentsSkipped++;
                            continue;
                        }
                        
                        // File has been modified, we could update it, but for now skip to avoid complexity
                        _logger.LogInformation("File has been modified but update not implemented: {FilePath}", file.Path);
                        log.DocumentsSkipped++;
                        continue;
                    }
                    
                    // Download file
                    using var fileStream = await handler.DownloadFileAsync(
                        connector.Configuration, 
                        connector.EncryptedCredentials, 
                        file.Path);
                    
                    // Compute hash using BaseConnectorHandler method
                    fileStream.Position = 0;
                    using var sha256 = SHA256.Create();
                    var hashBytes = await sha256.ComputeHashAsync(fileStream);
                    var fileHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                    
                    // Check for duplicate by hash in existing documents
                    if (existingDocuments.Any(d => d.SourceFileHash == fileHash))
                    {
                        _logger.LogDebug("Skipping duplicate file by hash: {FilePath}", file.Path);
                        log.DocumentsSkipped++;
                        continue;
                    }
                    
                    // Save file to disk
                    var fileName = Path.GetFileName(file.Name);
                    var safeFileName = GetSafeFileName(fileName);
                    var localFilePath = Path.Combine(uploadDirectory, safeFileName);
                    
                    // Ensure unique filename
                    localFilePath = GetUniqueFilePath(localFilePath);
                    
                    fileStream.Position = 0;
                    using (var outputStream = File.Create(localFilePath))
                    {
                        await fileStream.CopyToAsync(outputStream);
                    }
                    
                    // Create document entry
                    var document = new Document
                    {
                        FileName = fileName,
                        FilePath = localFilePath,
                        ContentType = file.ContentType ?? "application/octet-stream",
                        FileSize = file.Size,
                        ExtractedText = "", // Will be filled by document processing
                        ProcessingStatus = "Pending",
                        ChunkEmbeddingStatus = schedule.GenerateEmbeddingsImmediately ? "Pending" : "NotRequired",
                        ActualCategory = schedule.DefaultCategory,
                        Visibility = schedule.DefaultVisibility,
                        // Use validated owner ID (null if owner doesn't exist in AspNetUsers)
                        // Documents with null OwnerId are orphaned and may need admin intervention
                        OwnerId = validatedOwnerId,
                        TenantId = connector.TenantId,
                        UploadedAt = DateTime.UtcNow,
                        
                        // Source tracking for deduplication
                        SourceConnectorId = connector.Id,
                        SourceFilePath = file.Path,
                        SourceFileHash = fileHash,
                        SourceLastModified = file.ModifiedDate
                    };
                    
                    documentsToAdd.Add(document);
                    log.DocumentsProcessed++;
                    
                    // OTTIMIZZAZIONE: Batch save ogni 20 documenti per ridurre roundtrip DB
                    if (documentsToAdd.Count >= 20)
                    {
                        _context.Documents.AddRange(documentsToAdd);
                        await _context.SaveChangesAsync();
                        
                        _logger.LogInformation("Saved batch of {Count} documents", documentsToAdd.Count);
                        documentsToAdd.Clear();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing file {FileName}", file.Name);
                    log.DocumentsFailed++;
                }
            }
            
            // Save remaining documents
            if (documentsToAdd.Count > 0)
            {
                _context.Documents.AddRange(documentsToAdd);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Saved final batch of {Count} documents", documentsToAdd.Count);
            }
            
            log.CompletedAt = DateTime.UtcNow;
            log.Status = IngestionStatus.Completed;
            log.DurationSeconds = (int)(log.CompletedAt.Value - log.StartedAt).TotalSeconds;
            
            // Update schedule statistics
            schedule.LastExecutedAt = DateTime.UtcNow;
            schedule.LastExecutionDocumentCount = log.DocumentsProcessed;
            schedule.LastExecutionStatus = IngestionStatus.Completed;
            
            // Update connector's last synced time
            connector.LastSyncedAt = DateTime.UtcNow;
            
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Completed ingestion for schedule {ScheduleId}. Discovered: {Discovered}, Processed: {Processed}, Skipped: {Skipped}, Failed: {Failed}", 
                scheduleId, log.DocumentsDiscovered, log.DocumentsProcessed, log.DocumentsSkipped, log.DocumentsFailed);
            
            return log;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing ingestion for schedule {ScheduleId}", scheduleId);
            
            log.CompletedAt = DateTime.UtcNow;
            log.Status = IngestionStatus.Failed;
            log.ErrorMessage = ex.Message;
            log.DurationSeconds = (int)(log.CompletedAt.Value - log.StartedAt).TotalSeconds;
            
            await _context.SaveChangesAsync();
            
            throw;
        }
    }

    /// <summary>
    /// Recupera i log di esecuzione di uno schedule specifico.
    /// </summary>
    /// <param name="scheduleId">ID dello schedule</param>
    /// <param name="userId">ID utente per verifica autorizzazione</param>
    /// <param name="count">Numero massimo log da restituire (default 20)</param>
    /// <returns>Lista log ordinati per data avvio (più recenti prima)</returns>
    /// <exception cref="UnauthorizedAccessException">Se schedule non trovato o utente non autorizzato</exception>
    public async Task<List<IngestionLog>> GetIngestionLogsAsync(int scheduleId, string userId, int count = 20)
    {
        try
        {
            // Verify user owns the schedule
            var schedule = await GetScheduleAsync(scheduleId, userId);
            if (schedule == null)
            {
                throw new UnauthorizedAccessException("Schedule not found or access denied");
            }
            
            return await _context.IngestionLogs
                .Where(l => l.IngestionScheduleId == scheduleId)
                .OrderByDescending(l => l.StartedAt)
                .Take(count)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving logs for schedule {ScheduleId}", scheduleId);
            throw;
        }
    }

    /// <summary>
    /// Aggiorna il timestamp della prossima esecuzione per uno schedule.
    /// </summary>
    /// <param name="scheduleId">ID dello schedule da aggiornare</param>
    /// <remarks>
    /// Chiamato dopo ogni esecuzione completata per ricalcolare prossima esecuzione.
    /// Funziona solo per schedule abilitati di tipo Scheduled con cron expression valida.
    /// </remarks>
    public async Task UpdateNextExecutionTimeAsync(int scheduleId)
    {
        try
        {
            var schedule = await _context.IngestionSchedules.FindAsync(scheduleId);
            if (schedule == null || !schedule.IsEnabled || schedule.ScheduleType != ScheduleTypes.Scheduled)
            {
                return;
            }
            
            schedule.NextExecutionAt = CalculateNextExecutionTime(schedule.CronExpression);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Updated next execution time for schedule {ScheduleId} to {NextExecution}", 
                scheduleId, schedule.NextExecutionAt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating next execution time for schedule {ScheduleId}", scheduleId);
            throw;
        }
    }

    /// <summary>
    /// Calcola prossima data/ora di esecuzione da cron expression.
    /// </summary>
    /// <param name="cronExpression">Cron expression in formato standard (es. "0 0 * * *" = mezzanotte ogni giorno)</param>
    /// <returns>DateTime UTC della prossima occorrenza, o null se expression invalida/null</returns>
    /// <remarks>
    /// Usa libreria Cronos per parsing e calcolo.
    /// Timezone: UTC per consistenza con timestamp database.
    /// </remarks>
    private DateTime? CalculateNextExecutionTime(string? cronExpression)
    {
        if (string.IsNullOrEmpty(cronExpression))
        {
            return null;
        }
        
        try
        {
            var expression = CronExpression.Parse(cronExpression);
            return expression.GetNextOccurrence(DateTime.UtcNow, TimeZoneInfo.Utc);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Invalid cron expression: {CronExpression}", cronExpression);
            return null;
        }
    }
    
    /// <summary>
    /// Ottiene directory upload per salvataggio file ingestionati.
    /// </summary>
    /// <returns>Percorso assoluto directory uploads</returns>
    /// <remarks>
    /// Usa stessa directory degli upload manuali: wwwroot/uploads
    /// </remarks>
    private string GetUploadDirectory()
    {
        // Use the same uploads directory as manual uploads
        var baseDirectory = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
        return baseDirectory;
    }
    
    /// <summary>
    /// Sanitizza nome file rimuovendo caratteri non validi per filesystem.
    /// </summary>
    /// <param name="fileName">Nome file originale potenzialmente con caratteri invalidi</param>
    /// <returns>Nome file sicuro con caratteri invalidi sostituiti da underscore</returns>
    private string GetSafeFileName(string fileName)
    {
        // Remove invalid characters from filename
        var invalidChars = Path.GetInvalidFileNameChars();
        var safeFileName = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
        return safeFileName;
    }
    
    /// <summary>
    /// Genera percorso file univoco aggiungendo suffisso numerico se necessario.
    /// </summary>
    /// <param name="filePath">Percorso file desiderato</param>
    /// <returns>Percorso file garantito univoco con suffisso _N se originale esiste</returns>
    /// <remarks>
    /// Previene sovrascrittura file esistenti aggiungendo _1, _2, etc. prima estensione.
    /// </remarks>
    private string GetUniqueFilePath(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return filePath;
        }
        
        var directory = Path.GetDirectoryName(filePath) ?? "";
        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(filePath);
        var extension = Path.GetExtension(filePath);
        
        var counter = 1;
        string newFilePath;
        do
        {
            newFilePath = Path.Combine(directory, $"{fileNameWithoutExt}_{counter}{extension}");
            counter++;
        }
        while (File.Exists(newFilePath));
        
        return newFilePath;
    }
    
    /// <summary>
    /// Validates if a user exists in the AspNetUsers table
    /// </summary>
    private async Task<bool> UserExistsAsync(string? userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return false;
        }
        
        try
        {
            return await _appContext.Users.AnyAsync(u => u.Id == userId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking if user {UserId} exists", userId);
            return false;
        }
    }
}
