using DocN.Data.Models;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace DocN.Data.Services;

/// <summary>
/// Service for managing document ingestion queue with retry logic
/// Provides async processing, progress tracking, and failure handling
/// </summary>
public interface IIngestionQueueService
{
    /// <summary>
    /// Enqueue a document for processing
    /// </summary>
    Task<string> EnqueueDocumentAsync(int documentId, IngestionJobType jobType, Dictionary<string, object>? parameters = null);

    /// <summary>
    /// Enqueue multiple documents for batch processing
    /// </summary>
    Task<List<string>> EnqueueBatchAsync(IEnumerable<int> documentIds, IngestionJobType jobType, Dictionary<string, object>? parameters = null);

    /// <summary>
    /// Get job status
    /// </summary>
    Task<IngestionJobStatus?> GetJobStatusAsync(string jobId);

    /// <summary>
    /// Get jobs for a document
    /// </summary>
    Task<List<IngestionJobStatus>> GetDocumentJobsAsync(int documentId);

    /// <summary>
    /// Cancel a job
    /// </summary>
    Task<bool> CancelJobAsync(string jobId);

    /// <summary>
    /// Retry a failed job
    /// </summary>
    Task<bool> RetryJobAsync(string jobId);

    /// <summary>
    /// Get queue statistics
    /// </summary>
    Task<QueueStatistics> GetStatisticsAsync();

    /// <summary>
    /// Process next job in queue (called by background worker)
    /// </summary>
    Task<IngestionJobStatus?> ProcessNextJobAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Type of ingestion job
/// </summary>
public enum IngestionJobType
{
    ExtractText,
    GenerateEmbedding,
    GenerateChunks,
    AnalyzeDocument,
    FullProcessing
}

/// <summary>
/// Status of an ingestion job
/// </summary>
public class IngestionJobStatus
{
    public string JobId { get; set; } = string.Empty;
    public int DocumentId { get; set; }
    public IngestionJobType JobType { get; set; }
    public JobState State { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int RetryCount { get; set; }
    public int MaxRetries { get; set; } = 3;
    public string? ErrorMessage { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new();
    public int ProgressPercentage { get; set; }
    public string? ProgressMessage { get; set; }
}

/// <summary>
/// Job state
/// </summary>
public enum JobState
{
    Queued,
    Processing,
    Completed,
    Failed,
    Cancelled,
    RetryScheduled
}

/// <summary>
/// Queue statistics
/// </summary>
public class QueueStatistics
{
    public int QueuedJobs { get; set; }
    public int ProcessingJobs { get; set; }
    public int CompletedJobs { get; set; }
    public int FailedJobs { get; set; }
    public int CancelledJobs { get; set; }
    public double AverageProcessingTimeSeconds { get; set; }
    public DateTime? OldestQueuedJob { get; set; }
}

/// <summary>
/// In-memory implementation of ingestion queue service
/// In production, use RabbitMQ, Azure Service Bus, or AWS SQS
/// </summary>
public class IngestionQueueService : IIngestionQueueService
{
    private readonly ConcurrentQueue<IngestionJobStatus> _queue = new();
    private readonly ConcurrentDictionary<string, IngestionJobStatus> _jobs = new();
    private readonly ILogger<IngestionQueueService> _logger;
    private readonly IIngestionService _ingestionService;

    public IngestionQueueService(
        ILogger<IngestionQueueService> logger,
        IIngestionService ingestionService)
    {
        _logger = logger;
        _ingestionService = ingestionService;
    }

    /// <summary>
    /// Enqueue a document for processing
    /// </summary>
    public async Task<string> EnqueueDocumentAsync(int documentId, IngestionJobType jobType, Dictionary<string, object>? parameters = null)
    {
        var jobId = Guid.NewGuid().ToString();
        var job = new IngestionJobStatus
        {
            JobId = jobId,
            DocumentId = documentId,
            JobType = jobType,
            State = JobState.Queued,
            CreatedAt = DateTime.UtcNow,
            Parameters = parameters ?? new Dictionary<string, object>()
        };

        _jobs.TryAdd(jobId, job);
        _queue.Enqueue(job);

        _logger.LogInformation("Enqueued job {JobId} for document {DocumentId} with type {JobType}", 
            jobId, documentId, jobType);

        return await Task.FromResult(jobId);
    }

    /// <summary>
    /// Enqueue multiple documents for batch processing
    /// </summary>
    public async Task<List<string>> EnqueueBatchAsync(IEnumerable<int> documentIds, IngestionJobType jobType, Dictionary<string, object>? parameters = null)
    {
        var jobIds = new List<string>();

        foreach (var documentId in documentIds)
        {
            var jobId = await EnqueueDocumentAsync(documentId, jobType, parameters);
            jobIds.Add(jobId);
        }

        _logger.LogInformation("Enqueued batch of {Count} jobs with type {JobType}", jobIds.Count, jobType);
        return jobIds;
    }

    /// <summary>
    /// Get job status
    /// </summary>
    public async Task<IngestionJobStatus?> GetJobStatusAsync(string jobId)
    {
        _jobs.TryGetValue(jobId, out var job);
        return await Task.FromResult(job);
    }

    /// <summary>
    /// Get jobs for a document
    /// </summary>
    public async Task<List<IngestionJobStatus>> GetDocumentJobsAsync(int documentId)
    {
        var jobs = _jobs.Values
            .Where(j => j.DocumentId == documentId)
            .OrderByDescending(j => j.CreatedAt)
            .ToList();

        return await Task.FromResult(jobs);
    }

    /// <summary>
    /// Cancel a job
    /// </summary>
    public async Task<bool> CancelJobAsync(string jobId)
    {
        if (_jobs.TryGetValue(jobId, out var job) && job.State == JobState.Queued)
        {
            job.State = JobState.Cancelled;
            job.CompletedAt = DateTime.UtcNow;
            _logger.LogInformation("Cancelled job {JobId}", jobId);
            return await Task.FromResult(true);
        }

        return await Task.FromResult(false);
    }

    /// <summary>
    /// Retry a failed job
    /// </summary>
    public async Task<bool> RetryJobAsync(string jobId)
    {
        if (_jobs.TryGetValue(jobId, out var job) && job.State == JobState.Failed)
        {
            if (job.RetryCount < job.MaxRetries)
            {
                job.State = JobState.Queued;
                job.RetryCount++;
                job.StartedAt = null;
                job.CompletedAt = null;
                job.ProgressPercentage = 0;
                job.ProgressMessage = null;
                
                _queue.Enqueue(job);
                
                _logger.LogInformation("Retrying job {JobId} (attempt {RetryCount}/{MaxRetries})", 
                    jobId, job.RetryCount, job.MaxRetries);
                
                return await Task.FromResult(true);
            }
        }

        return await Task.FromResult(false);
    }

    /// <summary>
    /// Get queue statistics
    /// </summary>
    public async Task<QueueStatistics> GetStatisticsAsync()
    {
        var allJobs = _jobs.Values.ToList();
        var completedJobs = allJobs.Where(j => j.State == JobState.Completed && j.StartedAt.HasValue && j.CompletedAt.HasValue);
        
        var avgProcessingTime = 0.0;
        if (completedJobs.Any())
        {
            avgProcessingTime = completedJobs
                .Average(j => (j.CompletedAt!.Value - j.StartedAt!.Value).TotalSeconds);
        }

        var queuedJobs = allJobs.Where(j => j.State == JobState.Queued).ToList();
        var oldestQueued = queuedJobs.Any() 
            ? queuedJobs.Min(j => j.CreatedAt)
            : (DateTime?)null;

        return await Task.FromResult(new QueueStatistics
        {
            QueuedJobs = queuedJobs.Count,
            ProcessingJobs = allJobs.Count(j => j.State == JobState.Processing),
            CompletedJobs = allJobs.Count(j => j.State == JobState.Completed),
            FailedJobs = allJobs.Count(j => j.State == JobState.Failed),
            CancelledJobs = allJobs.Count(j => j.State == JobState.Cancelled),
            AverageProcessingTimeSeconds = avgProcessingTime,
            OldestQueuedJob = oldestQueued
        });
    }

    /// <summary>
    /// Process next job in queue
    /// </summary>
    public async Task<IngestionJobStatus?> ProcessNextJobAsync(CancellationToken cancellationToken = default)
    {
        if (!_queue.TryDequeue(out var job))
        {
            return null;
        }

        // Skip if cancelled
        if (job.State == JobState.Cancelled)
        {
            return null;
        }

        job.State = JobState.Processing;
        job.StartedAt = DateTime.UtcNow;

        try
        {
            _logger.LogInformation("Processing job {JobId} for document {DocumentId} with type {JobType}", 
                job.JobId, job.DocumentId, job.JobType);

            // Process based on job type
            switch (job.JobType)
            {
                case IngestionJobType.GenerateEmbedding:
                    await ProcessEmbeddingJobAsync(job, cancellationToken);
                    break;
                    
                case IngestionJobType.GenerateChunks:
                    await ProcessChunkingJobAsync(job, cancellationToken);
                    break;
                    
                case IngestionJobType.FullProcessing:
                    await ProcessFullIngestionJobAsync(job, cancellationToken);
                    break;
                    
                default:
                    throw new NotSupportedException($"Job type {job.JobType} is not supported");
            }

            job.State = JobState.Completed;
            job.CompletedAt = DateTime.UtcNow;
            job.ProgressPercentage = 100;
            job.ProgressMessage = "Completed successfully";

            _logger.LogInformation("Completed job {JobId} in {Duration}ms", 
                job.JobId, 
                (job.CompletedAt.Value - job.StartedAt.Value).TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job {JobId} failed: {Error}", job.JobId, ex.Message);
            
            job.State = JobState.Failed;
            job.CompletedAt = DateTime.UtcNow;
            job.ErrorMessage = ex.Message;

            // Schedule retry with exponential backoff
            if (job.RetryCount < job.MaxRetries)
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, job.RetryCount)), cancellationToken);
                await RetryJobAsync(job.JobId);
            }
        }

        return job;
    }

    /// <summary>
    /// Process embedding generation job
    /// </summary>
    private async Task ProcessEmbeddingJobAsync(IngestionJobStatus job, CancellationToken cancellationToken)
    {
        job.ProgressMessage = "Generating embeddings...";
        job.ProgressPercentage = 50;
        
        // Placeholder: In production, this would call the embedding generation pipeline
        // await _ingestionService.GenerateDocumentEmbeddingAsync(job.DocumentId);
        await Task.Delay(100, cancellationToken);
        
        job.ProgressPercentage = 100;
    }

    /// <summary>
    /// Process chunking job
    /// </summary>
    private async Task ProcessChunkingJobAsync(IngestionJobStatus job, CancellationToken cancellationToken)
    {
        job.ProgressMessage = "Creating chunks...";
        job.ProgressPercentage = 50;
        
        // Placeholder: In production, this would call the chunking pipeline
        await Task.Delay(100, cancellationToken);
        
        job.ProgressPercentage = 100;
    }

    /// <summary>
    /// Process full ingestion job
    /// </summary>
    private async Task ProcessFullIngestionJobAsync(IngestionJobStatus job, CancellationToken cancellationToken)
    {
        job.ProgressMessage = "Starting full processing...";
        job.ProgressPercentage = 10;
        
        // Placeholder: In production, this would call the full ingestion pipeline
        // For now, just simulate processing
        await Task.Delay(500, cancellationToken);
        
        job.ProgressPercentage = 100;
    }
}
