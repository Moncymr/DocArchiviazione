using DocN.Data.Constants;
using DocN.Data.Configuration;
using DocN.Data.Models;
using DocN.Data.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocN.Data.Services;

/// <summary>
/// Servizio di background per l'elaborazione batch degli embeddings dei documenti con gestione avanzata del workflow
/// </summary>
/// <remarks>
/// <para><strong>Scopo:</strong> Processare automaticamente documenti e chunk in background per generare embeddings vettoriali</para>
/// 
/// <para><strong>Funzionalità chiave:</strong></para>
/// <list type="bullet">
/// <item><description>Elaborazione ciclica automatica con intervallo configurabile</description></item>
/// <item><description>Circuit breaker per gestione errori consecutivi con timeout configurabile</description></item>
/// <item><description>Coda retry per documenti falliti con tentativi multipli</description></item>
/// <item><description>Elaborazione batch ottimizzata (10 documenti, 50 chunk per ciclo)</description></item>
/// <item><description>Validazione dimensioni embedding (768/1536) con gestione errori vettoriali</description></item>
/// <item><description>Logging dettagliato opzionale per diagnostica</description></item>
/// </list>
/// 
/// <para><strong>Pattern di elaborazione:</strong></para>
/// <list type="number">
/// <item><description>Verifica circuit breaker (salta ciclo se aperto)</description></item>
/// <item><description>Processa coda retry se abilitata</description></item>
/// <item><description>Processa documenti pending (crea chunk + embeddings)</description></item>
/// <item><description>Processa chunk pending (genera embeddings mancanti)</description></item>
/// <item><description>Aggiorna stato documenti completati</description></item>
/// </list>
/// 
/// <para><strong>Gestione stati workflow:</strong></para>
/// <list type="bullet">
/// <item><description><strong>Pending:</strong> Documento pronto per chunking</description></item>
/// <item><description><strong>Processing:</strong> Chunking in corso o completato parzialmente</description></item>
/// <item><description><strong>Completed:</strong> Tutti i chunk hanno embeddings</description></item>
/// <item><description><strong>Retrying:</strong> In coda retry dopo errore temporaneo</description></item>
/// <item><description><strong>NotRequired:</strong> Nessun chunk generabile</description></item>
/// </list>
/// 
/// <para><strong>Circuit breaker:</strong></para>
/// <list type="bullet">
/// <item><description>Soglia errori consecutivi: Configurabile (default 3)</description></item>
/// <item><description>Durata apertura: Configurabile (default 300 secondi = 5 minuti)</description></item>
/// <item><description>Auto-reset: Dopo timeout, riprova con stato "half-open"</description></item>
/// </list>
/// 
/// <para><strong>Ottimizzazioni:</strong></para>
/// <list type="bullet">
/// <item><description>Batch processing per ridurre overhead database</description></item>
/// <item><description>Priorità documenti in Processing per completamento veloce</description></item>
/// <item><description>Connection pooling EF Core tramite scoped services</description></item>
/// <item><description>Logging condizionale per performance (EnableDetailedLogging)</description></item>
/// <item><description>Validazione dimensioni embedding con helper dedicato</description></item>
/// </list>
/// 
/// <para><strong>Integrazione Hangfire:</strong> Questo servizio è un BackgroundService autonomo, 
/// ma può coesistere con job Hangfire schedulati per trigger manuali</para>
/// </remarks>
public class BatchEmbeddingProcessor : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BatchEmbeddingProcessor> _logger;
    private readonly BatchProcessingConfiguration _config;
    private readonly TimeSpan _processingInterval;

    // Circuit breaker state
    private int _consecutiveFailures = 0;
    private DateTime? _circuitOpenUntil = null;

    /// <summary>
    /// Inizializza una nuova istanza del processore batch embeddings
    /// </summary>
    /// <param name="serviceProvider">Service provider per risoluzione dipendenze scoped</param>
    /// <param name="logger">Logger per diagnostica e monitoraggio</param>
    /// <param name="config">Configurazione batch processing (intervallo, batch size, circuit breaker)</param>
    /// <remarks>
    /// Il service provider viene usato per creare scope separati ad ogni ciclo,
    /// garantendo isolation del DbContext e prevenendo memory leaks
    /// </remarks>
    public BatchEmbeddingProcessor(
        IServiceProvider serviceProvider,
        ILogger<BatchEmbeddingProcessor> logger,
        IOptions<BatchProcessingConfiguration> config)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _config = config.Value;
        _processingInterval = TimeSpan.FromSeconds(_config.ProcessingIntervalSeconds);
    }

    /// <summary>
    /// Esegue il ciclo principale di elaborazione batch in background
    /// </summary>
    /// <param name="stoppingToken">Token per cancellazione durante shutdown applicazione</param>
    /// <returns>Task completato quando il servizio viene arrestato</returns>
    /// <remarks>
    /// <para><strong>Ciclo di elaborazione:</strong></para>
    /// <list type="number">
    /// <item><description>Verifica stato circuit breaker (salta se aperto)</description></item>
    /// <item><description>Processa coda retry documenti falliti (se abilitata)</description></item>
    /// <item><description>Processa documenti pending (crea chunk + embeddings)</description></item>
    /// <item><description>Processa chunk pending (genera embeddings mancanti)</description></item>
    /// <item><description>Attende intervallo configurato prima del prossimo ciclo</description></item>
    /// </list>
    /// 
    /// <para><strong>Circuit breaker logic:</strong></para>
    /// <list type="bullet">
    /// <item><description>Incrementa contatore errori consecutivi ad ogni eccezione</description></item>
    /// <item><description>Apre circuit dopo soglia errori (previene tempesta di errori)</description></item>
    /// <item><description>Attende timeout configurato prima di riprovare</description></item>
    /// <item><description>Reset contatore su ciclo successful</description></item>
    /// </list>
    /// 
    /// <para><strong>Gestione errori:</strong> Errori non fatali vengono loggati ma non interrompono il servizio.
    /// Il circuit breaker protegge da errori ripetuti (es. database offline, API AI down)</para>
    /// </remarks>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Batch Embedding Processor started - will run every {Interval} seconds (MaxBatchSize: {MaxBatch}, MaxConcurrency: {MaxConcurrency})", 
            _processingInterval.TotalSeconds, _config.MaxBatchSize, _config.MaxConcurrency);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Check circuit breaker
                if (_circuitOpenUntil.HasValue)
                {
                    if (DateTime.UtcNow < _circuitOpenUntil.Value)
                    {
                        _logger.LogWarning("Circuit breaker is open until {OpenUntil}. Skipping batch processing.", 
                            _circuitOpenUntil.Value);
                        await Task.Delay(_processingInterval, stoppingToken);
                        continue;
                    }
                    else
                    {
                        // Circuit breaker timeout expired, try half-open
                        _logger.LogInformation("Circuit breaker timeout expired. Attempting to resume processing.");
                        _circuitOpenUntil = null;
                        _consecutiveFailures = 0;
                    }
                }

                if (_config.EnableDetailedLogging)
                {
                    _logger.LogDebug("=== Starting batch processing cycle ===");
                }

                // Process retry queue if enabled
                if (_config.EnableRetryQueue)
                {
                    await ProcessRetryQueueAsync(stoppingToken);
                }

                await ProcessPendingDocumentsAsync(stoppingToken);
                await ProcessPendingChunksAsync(stoppingToken);

                // Reset failure counter on successful cycle
                _consecutiveFailures = 0;

                if (_config.EnableDetailedLogging)
                {
                    _logger.LogDebug("=== Batch processing cycle complete ===");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in batch embedding processor");
                
                // Increment failure counter
                _consecutiveFailures++;

                // Open circuit breaker if threshold exceeded
                if (_consecutiveFailures >= _config.CircuitBreakerFailureThreshold)
                {
                    _circuitOpenUntil = DateTime.UtcNow.AddSeconds(_config.CircuitBreakerOpenDurationSeconds);
                    _logger.LogError(
                        "Circuit breaker opened due to {FailureCount} consecutive failures. Will retry at {OpenUntil}",
                        _consecutiveFailures, _circuitOpenUntil.Value);
                }
            }

            await Task.Delay(_processingInterval, stoppingToken);
        }

        _logger.LogInformation("Batch Embedding Processor stopped");
    }

    /// <summary>
    /// Processa documenti nella coda retry per riprovare elaborazioni fallite
    /// </summary>
    /// <param name="cancellationToken">Token per cancellazione durante shutdown</param>
    /// <returns>Task completato quando tutti i documenti retry sono stati processati</returns>
    /// <remarks>
    /// <para><strong>Logica retry:</strong></para>
    /// <list type="number">
    /// <item><description>Recupera documenti pronti per retry dal DocumentWorkflowService</description></item>
    /// <item><description>Determina stato target basato su PreviousWorkflowState</description></item>
    /// <item><description>Transizione a stato "Retrying" (audit trail)</description></item>
    /// <item><description>Transizione a stato target per riprendere elaborazione</description></item>
    /// <item><description>Registra errori e schedula prossimo retry se fallisce</description></item>
    /// </list>
    /// 
    /// <para><strong>Mapping stati:</strong></para>
    /// <list type="bullet">
    /// <item><description>AwaitingConfirmation → Analyzing (ricomincia analisi)</description></item>
    /// <item><description>Completed/Cancelled → Extracting (ricomincia da estrazione)</description></item>
    /// <item><description>Altri stati validi → Riprendi da quello stato</description></item>
    /// <item><description>Nessun stato precedente → Extracting (ricomincia da zero)</description></item>
    /// </list>
    /// 
    /// <para><strong>Integrazione workflow:</strong> Usa IDocumentWorkflowService per gestione stati FSM (Finite State Machine)
    /// con validazione transizioni e audit trail completo</para>
    /// </remarks>
    private async Task ProcessRetryQueueAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var workflowService = scope.ServiceProvider.GetRequiredService<IDocumentWorkflowService>();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        try
        {
            var documentsToRetry = await workflowService.GetDocumentsReadyForRetryAsync(_config.MaxRetryBatchSize);
            
            if (!documentsToRetry.Any())
            {
                if (_config.EnableDetailedLogging)
                {
                    _logger.LogDebug("No documents ready for retry");
                }
                return;
            }

            _logger.LogInformation("Found {Count} documents ready for retry", documentsToRetry.Count);

            foreach (var document in documentsToRetry)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    // Determine which state to restart from based on previous state
                    string targetState;
                    
                    if (!string.IsNullOrEmpty(document.PreviousWorkflowState))
                    {
                        // Map previous state to appropriate retry state
                        // Some states like AwaitingConfirmation should restart from Analyzing
                        targetState = document.PreviousWorkflowState switch
                        {
                            DocN.Data.Constants.DocumentProcessingState.AwaitingConfirmation => 
                                DocN.Data.Constants.DocumentProcessingState.Analyzing,
                            DocN.Data.Constants.DocumentProcessingState.Completed => 
                                DocN.Data.Constants.DocumentProcessingState.Extracting,
                            DocN.Data.Constants.DocumentProcessingState.Cancelled => 
                                DocN.Data.Constants.DocumentProcessingState.Extracting,
                            // For valid processing states, retry from that state
                            _ => document.PreviousWorkflowState
                        };
                    }
                    else
                    {
                        // No previous state, restart from beginning
                        targetState = DocN.Data.Constants.DocumentProcessingState.Extracting;
                    }

                    // Transition to Retrying state first
                    await workflowService.TransitionToStateAsync(
                        document.Id,
                        DocN.Data.Constants.DocumentProcessingState.Retrying,
                        $"Automatic retry {document.RetryCount}/{document.MaxRetries}");

                    // Then transition back to the target state to restart processing
                    await workflowService.TransitionToStateAsync(
                        document.Id,
                        targetState,
                        "Resuming from retry");

                    _logger.LogInformation(
                        "Retrying document {DocumentId} from state {State}. Retry {RetryCount}/{MaxRetries}",
                        document.Id, targetState, document.RetryCount, document.MaxRetries);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error retrying document {DocumentId}", document.Id);
                    
                    // Record the error and schedule next retry
                    await workflowService.RecordErrorAsync(
                        document.Id,
                        ex,
                        DocN.Data.Constants.DocumentErrorType.UnknownError);
                    
                    await workflowService.ScheduleRetryAsync(document.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing retry queue");
        }
    }

    /// <summary>
    /// Processa documenti pending per creare chunk e generare embeddings
    /// </summary>
    /// <param name="cancellationToken">Token per cancellazione durante shutdown</param>
    /// <returns>Task completato quando tutti i documenti pending sono stati processati</returns>
    /// <remarks>
    /// <para><strong>Processo elaborazione:</strong></para>
    /// <list type="number">
    /// <item><description>Query documenti con stato Pending O Processing senza chunk (stuck documents)</description></item>
    /// <item><description>Genera embedding documento intero (per ricerca full-document)</description></item>
    /// <item><description>Crea chunk usando IChunkingService (sliding window + overlap)</description></item>
    /// <item><description>Genera embeddings per ogni chunk (batch async)</description></item>
    /// <item><description>Aggiorna stato a Completed se tutti i chunk hanno embeddings</description></item>
    /// <item><description>Mantiene stato Processing se alcuni chunk falliscono (retry successivo)</description></item>
    /// </list>
    /// 
    /// <para><strong>Query optimization:</strong></para>
    /// <list type="bullet">
    /// <item><description>Filtro su ExtractedText not null/empty (evita documenti invalidi)</description></item>
    /// <item><description>Include documenti Processing senza chunk (recovery stuck documents)</description></item>
    /// <item><description>Batch size 10 documenti per ciclo (bilanciamento throughput/latency)</description></item>
    /// <item><description>Log statistiche distribuzione stati per diagnostica</description></item>
    /// </list>
    /// 
    /// <para><strong>Gestione errori:</strong></para>
    /// <list type="bullet">
    /// <item><description>DbUpdateException → Verifica vector dimension mismatch con helper dedicato</description></item>
    /// <item><description>Reset status a Pending su errore per retry successivo</description></item>
    /// <item><description>Log dettagliato embedding dimensions per debugging</description></item>
    /// <item><description>Continua con prossimo documento anche se uno fallisce</description></item>
    /// </list>
    /// 
    /// <para><strong>Validazione embeddings:</strong> Usa EmbeddingValidationHelper per verificare 
    /// dimensioni 768/1536 e compatibilità con schema pgvector PostgreSQL</para>
    /// </remarks>
    private async Task ProcessPendingDocumentsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var embeddingService = scope.ServiceProvider.GetRequiredService<IMultiProviderAIService>();
        var chunkingService = scope.ServiceProvider.GetRequiredService<IChunkingService>();

        try
        {
            // First, log the overall status distribution to help diagnose issues
            var statusStats = await context.Documents
                .GroupBy(d => d.ChunkEmbeddingStatus)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken);
            
            _logger.LogInformation("Document status distribution: {Stats}", 
                string.Join(", ", statusStats.Select(s => $"{s.Status}={s.Count}")));
            
            // Count documents with Pending status but no ExtractedText
            var pendingWithoutText = await context.Documents
                .CountAsync(d => d.ChunkEmbeddingStatus == ChunkEmbeddingStatus.Pending && 
                                string.IsNullOrEmpty(d.ExtractedText), cancellationToken);
            
            if (pendingWithoutText > 0)
            {
                _logger.LogWarning("{Count} documents have Pending status but no ExtractedText (will be skipped)", 
                    pendingWithoutText);
            }
            
            // Find documents that need chunks created
            // Include both Pending documents AND Processing documents that have no chunks
            // (Processing documents without chunks are likely stuck from a previous failed attempt)
            var pendingDocuments = await context.Documents
                .Where(d => !string.IsNullOrEmpty(d.ExtractedText) && 
                           (d.ChunkEmbeddingStatus == ChunkEmbeddingStatus.Pending ||
                            (d.ChunkEmbeddingStatus == ChunkEmbeddingStatus.Processing &&
                             !context.DocumentChunks.Any(c => c.DocumentId == d.Id))))
                .Take(10) // Process 10 at a time to avoid overload
                .ToListAsync(cancellationToken);

            if (!pendingDocuments.Any())
            {
                _logger.LogDebug("No documents found that need chunks created (this is normal if all docs are Processing with chunks or Completed)");
                return;
            }

            // Count how many are Pending vs Processing for better diagnostics
            var pendingCount = pendingDocuments.Count(d => d.ChunkEmbeddingStatus == ChunkEmbeddingStatus.Pending);
            var processingCount = pendingDocuments.Count(d => d.ChunkEmbeddingStatus == ChunkEmbeddingStatus.Processing);
            
            _logger.LogInformation("Processing {Count} documents for chunk creation and embeddings ({PendingCount} Pending, {ProcessingCount} Processing without chunks)", 
                pendingDocuments.Count, pendingCount, processingCount);

            foreach (var document in pendingDocuments)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    // Log if this is a retry of a stuck Processing document
                    if (document.ChunkEmbeddingStatus == ChunkEmbeddingStatus.Processing)
                    {
                        _logger.LogWarning("Retrying document {Id}: {FileName} - was stuck in Processing status without chunks", 
                            document.Id, document.FileName);
                    }
                    
                    // Generate embedding for document only if it doesn't have one
                    if (document.EmbeddingVector == null && !string.IsNullOrWhiteSpace(document.ExtractedText))
                    {
                        var embedding = await embeddingService.GenerateEmbeddingAsync(document.ExtractedText);
                        if (embedding != null)
                        {
                            document.EmbeddingVector = embedding;
                            document.EmbeddingDimension = embedding.Length;
                            
                            // Log embedding info before saving
                            _logger.LogInformation("Generated embedding for document {Id}: {FileName}", 
                                document.Id, document.FileName);
                            _logger.LogInformation("Embedding details - Length: {Length}, First 5 values: [{Values}]",
                                embedding.Length, 
                                string.Join(", ", embedding.Take(5).Select(v => v.ToString("F6"))));
                        }
                    }
                    else
                    {
                        _logger.LogInformation("Document {Id} already has embedding, skipping embedding generation", 
                            document.Id);
                    }

                    // Create chunks for the document
                    var chunks = chunkingService.ChunkDocument(document);
                    _logger.LogInformation("ChunkDocument returned {ChunkCount} chunks for document {Id}", 
                        chunks.Count, document.Id);
                    
                    if (chunks.Count == 0)
                    {
                        _logger.LogWarning("No chunks created for document {Id}: {FileName}. ExtractedText length: {Length}", 
                            document.Id, document.FileName, document.ExtractedText?.Length ?? 0);
                        
                        // Mark as NotRequired if no chunks can be created
                        document.ChunkEmbeddingStatus = ChunkEmbeddingStatus.NotRequired;
                        await context.SaveChangesAsync(cancellationToken);
                        continue;
                    }
                    
                    // Update status to Processing
                    document.ChunkEmbeddingStatus = ChunkEmbeddingStatus.Processing;
                    await context.SaveChangesAsync(cancellationToken);
                    
                    // Generate embeddings for chunks
                    var chunksWithEmbeddings = 0;
                    var failedChunks = 0;
                    foreach (var chunk in chunks)
                    {
                        if (cancellationToken.IsCancellationRequested)
                            break;

                        _logger.LogDebug("Processing chunk {ChunkIndex} of document {DocumentId}", 
                            chunk.ChunkIndex, document.Id);

                        try
                        {
                            var chunkEmbedding = await embeddingService.GenerateEmbeddingAsync(chunk.ChunkText);
                            if (chunkEmbedding != null)
                            {
                                // Validate embedding dimensions
                                EmbeddingValidationHelper.ValidateEmbeddingDimensions(chunkEmbedding, _logger);
                                chunk.ChunkEmbedding = chunkEmbedding;
                                chunk.EmbeddingDimension = chunkEmbedding.Length;
                                chunksWithEmbeddings++;
                                _logger.LogDebug("Successfully generated embedding for chunk {ChunkIndex} of document {DocumentId}: {Dimension} dimensions", 
                                    chunk.ChunkIndex, document.Id, chunkEmbedding.Length);
                            }
                            else
                            {
                                failedChunks++;
                                _logger.LogWarning("Embedding generation returned null for chunk {ChunkIndex} of document {DocumentId}", 
                                    chunk.ChunkIndex, document.Id);
                            }
                        }
                        catch (Exception ex)
                        {
                            failedChunks++;
                            _logger.LogError(ex, "Failed to generate embedding for chunk {ChunkIndex} of document {DocumentId}: {Error}", 
                                chunk.ChunkIndex, document.Id, ex.Message);
                        }
                        
                        context.DocumentChunks.Add(chunk);
                    }

                    // Only mark as Completed if ALL chunks have embeddings
                    // Otherwise keep as Processing so ProcessPendingChunksAsync can retry
                    if (chunksWithEmbeddings == chunks.Count)
                    {
                        document.ChunkEmbeddingStatus = ChunkEmbeddingStatus.Completed;
                        _logger.LogInformation("Created {ChunkCount} chunks for document {Id} - All chunks have embeddings - Status: Completed", 
                            chunks.Count, document.Id);
                    }
                    else
                    {
                        // Some chunks don't have embeddings, keep as Processing
                        // ProcessPendingChunksAsync will retry failed chunks
                        _logger.LogWarning("Created {TotalChunks} chunks for document {Id}, but only {SuccessCount} have embeddings ({FailedCount} failed). Status remains Processing for retry.",
                            chunks.Count, document.Id, chunksWithEmbeddings, failedChunks);
                    }
                    
                    await context.SaveChangesAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    // Extract the inner exception details for better error reporting
                    if (ex is DbUpdateException dbEx)
                    {
                        var innerMessage = dbEx.InnerException?.Message ?? dbEx.Message;
                        
                        // Check for vector dimension mismatch error
                        if (EmbeddingValidationHelper.IsVectorDimensionMismatchError(innerMessage))
                        {
                            _logger.LogError(ex, "Vector dimension mismatch for document {Id}: {FileName}", 
                                document.Id, document.FileName);
                            _logger.LogError("Database save failed with dimension mismatch. Original error: {Error}", innerMessage);
                        }
                        else
                        {
                            _logger.LogError(ex, "Database error processing document {Id}: {FileName}", 
                                document.Id, document.FileName);
                        }
                    }
                    else
                    {
                        _logger.LogError(ex, "Error processing document {Id}: {FileName}", 
                            document.Id, document.FileName);
                    }
                    
                    // Reset status to Pending so it can be retried later
                    try
                    {
                        document.ChunkEmbeddingStatus = ChunkEmbeddingStatus.Pending;
                        await context.SaveChangesAsync(cancellationToken);
                    }
                    catch (Exception saveEx)
                    {
                        _logger.LogError(saveEx, "Failed to reset status for document {Id}", document.Id);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ProcessPendingDocumentsAsync");
        }
    }

    /// <summary>
    /// Processa chunk senza embeddings per completare documenti in Processing
    /// </summary>
    /// <param name="cancellationToken">Token per cancellazione durante shutdown</param>
    /// <returns>Task completato quando tutti i chunk pending sono stati processati</returns>
    /// <remarks>
    /// <para><strong>Strategia elaborazione:</strong></para>
    /// <list type="number">
    /// <item><description>Query chunk senza embeddings (ChunkEmbedding768 e ChunkEmbedding1536 null)</description></item>
    /// <item><description>Prioritizza chunk da documenti Processing (completamento veloce)</description></item>
    /// <item><description>Batch size 50 chunk per ciclo (aumentato da 20 per throughput)</description></item>
    /// <item><description>Genera embeddings async con gestione errori individuali</description></item>
    /// <item><description>Verifica completamento documenti e aggiorna stati</description></item>
    /// </list>
    /// 
    /// <para><strong>Query optimization:</strong></para>
    /// <list type="bullet">
    /// <item><description>Include(c => c.Document) → Eager loading per evitare N+1 queries</description></item>
    /// <item><description>OrderByDescending priorità Processing documents (completa documenti in corso prima)</description></item>
    /// <item><description>ThenBy DocumentId, ChunkIndex → Ordinamento deterministico per testing</description></item>
    /// <item><description>Log statistiche totali vs batch per monitoraggio progresso</description></item>
    /// </list>
    /// 
    /// <para><strong>Aggiornamento stati:</strong></para>
    /// <list type="bullet">
    /// <item><description>Conta chunk per documento direttamente da DB (evita EF cache issues)</description></item>
    /// <item><description>Confronta total chunks vs chunks with embeddings</description></item>
    /// <item><description>Aggiorna a Completed solo se 100% chunk hanno embeddings</description></item>
    /// <item><description>Batch update documenti in singola transazione (performance)</description></item>
    /// </list>
    /// 
    /// <para><strong>Fault tolerance:</strong> Se salvataggio embeddings fallisce, log CRITICAL e re-throw
    /// per trigger circuit breaker. Previene data loss parziali.</para>
    /// </remarks>
    private async Task ProcessPendingChunksAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var embeddingService = scope.ServiceProvider.GetRequiredService<IMultiProviderAIService>();

        try
        {
            // First, log overall statistics about chunks needing processing
            var totalChunksWithoutEmbeddings = await context.DocumentChunks
                .CountAsync(c => c.ChunkEmbedding768 == null && c.ChunkEmbedding1536 == null, cancellationToken);
            
            if (totalChunksWithoutEmbeddings == 0)
            {
                _logger.LogDebug("No chunks without embeddings found");
                return;
            }
            
            _logger.LogInformation("Found {TotalChunks} total chunks without embeddings, processing up to 50", 
                totalChunksWithoutEmbeddings);
            
            // Find chunks without embeddings - prioritize chunks from Processing documents
            // This ensures documents in Processing status get completed first
            // Note: OrderByDescending on boolean is acceptable for current scale (50 chunks per batch)
            // If performance becomes an issue, can split into two separate queries with UNION
            var pendingChunks = await context.DocumentChunks
                .Include(c => c.Document)
                .Where(c => c.ChunkEmbedding768 == null && c.ChunkEmbedding1536 == null)
                .OrderByDescending(c => c.Document!.ChunkEmbeddingStatus == ChunkEmbeddingStatus.Processing)
                .ThenBy(c => c.DocumentId)
                .ThenBy(c => c.ChunkIndex)
                .Take(50) // Increased from 20 to 50 for faster processing
                .ToListAsync(cancellationToken);

            if (!pendingChunks.Any())
            {
                _logger.LogWarning("Query returned 0 chunks but {TotalChunks} chunks without embeddings exist. Possible query issue!", 
                    totalChunksWithoutEmbeddings);
                return;
            }

            // Log statistics about what we're processing
            var processingDocCount = pendingChunks.Count(c => c.Document!.ChunkEmbeddingStatus == ChunkEmbeddingStatus.Processing);
            var pendingDocCount = pendingChunks.Count(c => c.Document!.ChunkEmbeddingStatus == ChunkEmbeddingStatus.Pending);
            _logger.LogInformation("Processing {Count} chunks for embeddings ({ProcessingDocs} from Processing docs, {PendingDocs} from Pending docs)", 
                pendingChunks.Count, processingDocCount, pendingDocCount);

            var successfulEmbeddings = 0;
            var failedEmbeddings = 0;

            foreach (var chunk in pendingChunks)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    var embedding = await embeddingService.GenerateEmbeddingAsync(chunk.ChunkText);
                    if (embedding != null)
                    {
                        chunk.ChunkEmbedding = embedding;
                        chunk.EmbeddingDimension = embedding.Length;
                        successfulEmbeddings++;
                        _logger.LogDebug("Generated embedding for chunk {Id} of document {DocumentId}", 
                            chunk.Id, chunk.DocumentId);
                    }
                    else
                    {
                        failedEmbeddings++;
                        _logger.LogWarning("Embedding generation returned null for chunk {Id} of document {DocumentId}", 
                            chunk.Id, chunk.DocumentId);
                    }
                }
                catch (Exception ex)
                {
                    failedEmbeddings++;
                    _logger.LogError(ex, "Error processing chunk {Id} of document {DocumentId}: {Error}", 
                        chunk.Id, chunk.DocumentId, ex.Message);
                }
            }
            
            _logger.LogInformation("Embedding generation complete: {Success} successful, {Failed} failed out of {Total} chunks", 
                successfulEmbeddings, failedEmbeddings, pendingChunks.Count);

            try
            {
                var savedCount = await context.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Successfully saved {Count} chunk embedding(s) to database (SaveChanges returned {SavedCount})", 
                    pendingChunks.Count, savedCount);
            }
            catch (Exception saveEx)
            {
                _logger.LogError(saveEx, "CRITICAL: Failed to save chunk embeddings to database. Changes were not persisted!");
                throw; // Re-throw to be caught by outer exception handler
            }
            
            // Check if any documents now have all their chunks with embeddings and update their status
            var documentIds = pendingChunks.Select(c => c.DocumentId).Distinct().ToList();
            var updatedDocuments = 0;
            
            _logger.LogInformation("Checking {Count} document(s) to see if they can be marked as Completed", documentIds.Count);
            
            foreach (var documentId in documentIds)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;
                    
                var document = await context.Documents.FindAsync(new object[] { documentId }, cancellationToken);
                if (document == null)
                {
                    _logger.LogWarning("Document {DocumentId} not found when checking for completion", documentId);
                    continue;
                }
                
                if (document.ChunkEmbeddingStatus != ChunkEmbeddingStatus.Processing)
                {
                    _logger.LogDebug("Document {DocumentId} has status {Status}, skipping completion check", 
                        documentId, document.ChunkEmbeddingStatus);
                    continue;
                }
                
                // Count chunks directly in database to get accurate count (avoid EF Core cache issues)
                var totalChunks = await context.DocumentChunks
                    .Where(c => c.DocumentId == documentId)
                    .CountAsync(cancellationToken);
                    
                var chunksWithEmbeddings = await context.DocumentChunks
                    .Where(c => c.DocumentId == documentId && 
                               (c.ChunkEmbedding768 != null || c.ChunkEmbedding1536 != null))
                    .CountAsync(cancellationToken);
                
                _logger.LogInformation("Document {DocumentId}: {CompletedChunks}/{TotalChunks} chunks have embeddings", 
                    documentId, chunksWithEmbeddings, totalChunks);
                
                if (totalChunks > 0 && chunksWithEmbeddings == totalChunks)
                {
                    document.ChunkEmbeddingStatus = ChunkEmbeddingStatus.Completed;
                    _logger.LogInformation("✅ Document {Id}: {FileName} now has all {ChunkCount} chunks with embeddings - Status updated to Completed", 
                        documentId, document.FileName, totalChunks);
                    updatedDocuments++;
                }
                else if (chunksWithEmbeddings > 0)
                {
                    _logger.LogInformation("Document {DocumentId} still has {PendingCount} chunks without embeddings, will retry in next cycle", 
                        documentId, totalChunks - chunksWithEmbeddings);
                }
            }
            
            // Save all document status updates in a single batch
            if (updatedDocuments > 0)
            {
                await context.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Updated {Count} document(s) status to Completed", updatedDocuments);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ProcessPendingChunksAsync");
        }
    }
}

/// <summary>
/// Servizio per trigger manuale dell'elaborazione batch embeddings
/// </summary>
/// <remarks>
/// Complementare a BatchEmbeddingProcessor per elaborazioni on-demand.
/// Utile per trigger manuali via API o Hangfire jobs schedulati.
/// </remarks>
public interface IBatchProcessingService
{
    /// <summary>
    /// Processa un documento specifico immediatamente
    /// </summary>
    /// <param name="documentId">ID documento da processare</param>
    /// <returns>Task completato quando documento è stato processato</returns>
    /// <exception cref="InvalidOperationException">Se dimensioni embedding non corrispondono allo schema database</exception>
    Task ProcessDocumentAsync(int documentId);

    /// <summary>
    /// Processa tutti i documenti pending in un'unica operazione
    /// </summary>
    /// <returns>Task completato quando tutti i documenti pending sono stati processati</returns>
    /// <remarks>
    /// Elaborazione sequenziale per evitare sovraccarico. Errori su singoli documenti
    /// vengono loggati ma non interrompono l'elaborazione degli altri.
    /// </remarks>
    Task ProcessAllPendingAsync();

    /// <summary>
    /// Ottiene statistiche sull'elaborazione embeddings
    /// </summary>
    /// <returns>Statistiche documenti e chunk con/senza embeddings e copertura percentuale</returns>
    Task<BatchProcessingStats> GetStatsAsync();
}

/// <summary>
/// Statistiche sull'elaborazione batch embeddings
/// </summary>
public class BatchProcessingStats
{
    /// <summary>
    /// Numero documenti senza embeddings
    /// </summary>
    public int DocumentsWithoutEmbeddings { get; set; }

    /// <summary>
    /// Numero chunk senza embeddings
    /// </summary>
    public int ChunksWithoutEmbeddings { get; set; }

    /// <summary>
    /// Totale documenti nel sistema
    /// </summary>
    public int TotalDocuments { get; set; }

    /// <summary>
    /// Totale chunk nel sistema
    /// </summary>
    public int TotalChunks { get; set; }

    /// <summary>
    /// Percentuale copertura embeddings (0-100)
    /// </summary>
    public double EmbeddingCoveragePercentage { get; set; }
}

/// <summary>
/// Implementazione servizio batch processing per trigger manuali
/// </summary>
/// <remarks>
/// <para><strong>Scopo:</strong> Fornire API programmatica per elaborazione embeddings on-demand</para>
/// 
/// <para><strong>Differenze da BatchEmbeddingProcessor:</strong></para>
/// <list type="bullet">
/// <item><description>Trigger manuale vs automatico</description></item>
/// <item><description>Elaborazione sincrona vs background continuo</description></item>
/// <item><description>Nessun circuit breaker (caller gestisce retry)</description></item>
/// <item><description>Ideale per API endpoints e Hangfire jobs</description></item>
/// </list>
/// </remarks>
public class BatchProcessingService : IBatchProcessingService
{
    private readonly ApplicationDbContext _context;
    private readonly IEmbeddingService _embeddingService;
    private readonly IChunkingService _chunkingService;
    private readonly ILogger<BatchProcessingService> _logger;

    /// <summary>
    /// Inizializza una nuova istanza del servizio batch processing
    /// </summary>
    /// <param name="context">Database context per accesso documenti e chunk</param>
    /// <param name="embeddingService">Servizio generazione embeddings (deprecato, usa IMultiProviderAIService)</param>
    /// <param name="chunkingService">Servizio chunking documenti con sliding window</param>
    /// <param name="logger">Logger per diagnostica</param>
    public BatchProcessingService(
        ApplicationDbContext context,
        IEmbeddingService embeddingService,
        IChunkingService chunkingService,
        ILogger<BatchProcessingService> logger)
    {
        _context = context;
        _embeddingService = embeddingService;
        _chunkingService = chunkingService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task ProcessDocumentAsync(int documentId)
    {
        var document = await _context.Documents.FindAsync(documentId);
        if (document == null)
        {
            _logger.LogWarning("Document {Id} not found", documentId);
            return;
        }

        try
        {
            // Generate embedding if not present
            if (document.EmbeddingVector == null && !string.IsNullOrEmpty(document.ExtractedText))
            {
                var embedding = await _embeddingService.GenerateEmbeddingAsync(document.ExtractedText);
                if (embedding != null)
                {
                    document.EmbeddingVector = embedding;
                    document.EmbeddingDimension = embedding.Length;
                    
                    // Log embedding info before saving
                    _logger.LogInformation("Generated embedding for document {Id}: {FileName} - Length: {Length}",
                        document.Id, document.FileName, embedding.Length);
                    _logger.LogInformation("Embedding first 5 values: [{Values}]",
                        string.Join(", ", embedding.Take(5).Select(v => v.ToString("F6"))));
                }
            }

            // Create chunks if they don't exist
            var existingChunks = await _context.DocumentChunks
                .Where(c => c.DocumentId == documentId)
                .CountAsync();

            if (existingChunks == 0)
            {
                var chunks = _chunkingService.ChunkDocument(document);
                foreach (var chunk in chunks)
                {
                    var chunkEmbedding = await _embeddingService.GenerateEmbeddingAsync(chunk.ChunkText);
                    if (chunkEmbedding != null)
                    {
                        // Validate embedding dimensions
                        EmbeddingValidationHelper.ValidateEmbeddingDimensions(chunkEmbedding, _logger);
                        chunk.ChunkEmbedding = chunkEmbedding;
                        chunk.EmbeddingDimension = chunkEmbedding.Length;
                    }
                    _context.DocumentChunks.Add(chunk);
                }
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Successfully processed document {Id}", documentId);
        }
        catch (DbUpdateException ex)
        {
            // Extract the inner exception details for better error reporting
            var innerMessage = ex.InnerException?.Message ?? ex.Message;
            
            // Check for vector dimension mismatch error
            if (EmbeddingValidationHelper.IsVectorDimensionMismatchError(innerMessage))
            {
                _logger.LogError(ex, "Vector dimension mismatch for document {Id}", documentId);
                throw new InvalidOperationException(
                    EmbeddingValidationHelper.CreateDimensionMismatchErrorMessage(0, innerMessage),
                    ex);
            }
            
            _logger.LogError(ex, "Error processing document {Id}", documentId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing document {Id}", documentId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task ProcessAllPendingAsync()
    {
        var pendingDocuments = await _context.Documents
            .Where(d => !string.IsNullOrEmpty(d.ExtractedText) && 
                       ((d.EmbeddingVector768 == null && d.EmbeddingVector1536 == null) || 
                        !_context.DocumentChunks.Any(c => c.DocumentId == d.Id)))
            .Select(d => d.Id)
            .ToListAsync();

        _logger.LogInformation("Processing {Count} pending documents", pendingDocuments.Count);

        foreach (var docId in pendingDocuments)
        {
            try
            {
                await ProcessDocumentAsync(docId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing document {Id}", docId);
                // Continue with next document
            }
        }
    }

    /// <inheritdoc/>
    public async Task<BatchProcessingStats> GetStatsAsync()
    {
        var totalDocs = await _context.Documents.CountAsync();
        var docsWithoutEmbeddings = await _context.Documents
            .Where(d => d.EmbeddingVector768 == null && d.EmbeddingVector1536 == null)
            .CountAsync();
        
        var docsWithoutChunks = await _context.Documents
            .Where(d => !_context.DocumentChunks.Any(c => c.DocumentId == d.Id))
            .CountAsync();

        var totalChunks = await _context.DocumentChunks.CountAsync();
        var chunksWithoutEmbeddings = await _context.DocumentChunks
            .Where(c => c.ChunkEmbedding768 == null && c.ChunkEmbedding1536 == null)
            .CountAsync();

        var docsWithEmbeddings = totalDocs - docsWithoutEmbeddings;
        var coveragePercentage = totalDocs > 0 
            ? (double)docsWithEmbeddings / totalDocs * 100 
            : 0;

        return new BatchProcessingStats
        {
            DocumentsWithoutEmbeddings = docsWithoutEmbeddings,
            ChunksWithoutEmbeddings = chunksWithoutEmbeddings,
            TotalDocuments = totalDocs,
            TotalChunks = totalChunks,
            EmbeddingCoveragePercentage = coveragePercentage
        };
    }
}
