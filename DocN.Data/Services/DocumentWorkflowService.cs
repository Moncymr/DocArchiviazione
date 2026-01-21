using System.Text.Json;
using DocN.Data.Constants;
using DocN.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DocN.Data.Services;

/// <summary>
/// Service for managing document processing workflow states and transitions
/// Implements robust state management, error handling, and retry logic
/// </summary>
public interface IDocumentWorkflowService
{
    /// <summary>
    /// Transition a document to a new workflow state with validation
    /// </summary>
    Task<WorkflowTransitionResult> TransitionToStateAsync(int documentId, string toState, string? reason = null);
    
    /// <summary>
    /// Record an error for a document and transition to appropriate error state
    /// </summary>
    Task<WorkflowTransitionResult> RecordErrorAsync(int documentId, Exception exception, string errorType, bool isRetryable = true);
    
    /// <summary>
    /// Schedule a retry for a failed document with exponential backoff
    /// </summary>
    Task ScheduleRetryAsync(int documentId, TimeSpan? customDelay = null);
    
    /// <summary>
    /// Get documents ready for retry
    /// </summary>
    Task<List<Document>> GetDocumentsReadyForRetryAsync(int maxCount = 10);
    
    /// <summary>
    /// Get detailed workflow status for a document including history
    /// </summary>
    Task<DocumentWorkflowStatus> GetWorkflowStatusAsync(int documentId);
    
    /// <summary>
    /// Reset workflow state to allow manual restart
    /// </summary>
    Task<WorkflowTransitionResult> ResetWorkflowAsync(int documentId);
}

/// <summary>
/// Result of a workflow state transition
/// </summary>
public class WorkflowTransitionResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? PreviousState { get; set; }
    public string? NewState { get; set; }
    public DateTime TransitionedAt { get; set; }
}

/// <summary>
/// Detailed workflow status for a document
/// </summary>
public class DocumentWorkflowStatus
{
    public int DocumentId { get; set; }
    public string CurrentState { get; set; } = string.Empty;
    public string? PreviousState { get; set; }
    public DateTime? StateEnteredAt { get; set; }
    public int RetryCount { get; set; }
    public int MaxRetries { get; set; }
    public DateTime? NextRetryAt { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorType { get; set; }
    public bool IsRetryable { get; set; }
    public List<string> ValidNextStates { get; set; } = new();
    public List<WorkflowStateChangeRecord> History { get; set; } = new();
}

/// <summary>
/// Record of a workflow state change
/// </summary>
public class WorkflowStateChangeRecord
{
    public DateTime ChangedAt { get; set; }
    public string FromState { get; set; } = string.Empty;
    public string ToState { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

/// <summary>
/// Error details for structured error logging
/// </summary>
public class WorkflowErrorDetails
{
    public string ErrorType { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public string? StackTrace { get; set; }
    public DateTime Timestamp { get; set; }
    public Dictionary<string, string> Context { get; set; } = new();
}

/// <summary>
/// Implementation of document workflow service
/// </summary>
public class DocumentWorkflowService : IDocumentWorkflowService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DocumentWorkflowService> _logger;
    
    // Exponential backoff configuration
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromMinutes(1);
    private static readonly double BackoffMultiplier = 2.0;
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromHours(4);

    public DocumentWorkflowService(
        ApplicationDbContext context,
        ILogger<DocumentWorkflowService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<WorkflowTransitionResult> TransitionToStateAsync(int documentId, string toState, string? reason = null)
    {
        try
        {
            var document = await _context.Documents.FindAsync(documentId);
            if (document == null)
            {
                return new WorkflowTransitionResult
                {
                    Success = false,
                    ErrorMessage = $"Document {documentId} not found"
                };
            }

            var fromState = document.WorkflowState;
            
            // Validate state transition
            if (!DocumentStateTransitions.IsValidTransition(fromState ?? "", toState))
            {
                var validStates = string.Join(", ", DocumentStateTransitions.GetValidNextStates(fromState ?? ""));
                _logger.LogWarning(
                    "Invalid state transition for document {DocumentId}: {FromState} → {ToState}. Valid transitions: {ValidStates}",
                    documentId, fromState, toState, validStates);
                
                return new WorkflowTransitionResult
                {
                    Success = false,
                    ErrorMessage = $"Invalid state transition from '{fromState}' to '{toState}'. Valid next states: {validStates}",
                    PreviousState = fromState,
                    NewState = toState
                };
            }

            // Perform transition
            document.PreviousWorkflowState = fromState;
            document.WorkflowState = toState;
            document.StateEnteredAt = DateTime.UtcNow;
            
            // Clear error information on successful transitions to non-error states
            if (toState != DocumentProcessingState.Failed && 
                toState != DocumentProcessingState.PermanentFailure &&
                toState != DocumentProcessingState.Retrying)
            {
                document.ErrorMessage = null;
                document.ErrorType = null;
                document.ErrorDetailsJson = null;
            }

            await _context.SaveChangesAsync();
            
            _logger.LogInformation(
                "Document {DocumentId} transitioned: {FromState} → {ToState}. Reason: {Reason}",
                documentId, fromState ?? "null", toState, reason ?? "none");

            return new WorkflowTransitionResult
            {
                Success = true,
                PreviousState = fromState,
                NewState = toState,
                TransitionedAt = document.StateEnteredAt.Value
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error transitioning document {DocumentId} to state {State}", documentId, toState);
            return new WorkflowTransitionResult
            {
                Success = false,
                ErrorMessage = $"Exception during state transition: {ex.Message}"
            };
        }
    }

    /// <inheritdoc/>
    public async Task<WorkflowTransitionResult> RecordErrorAsync(
        int documentId, 
        Exception exception, 
        string errorType,
        bool isRetryable = true)
    {
        try
        {
            var document = await _context.Documents.FindAsync(documentId);
            if (document == null)
            {
                return new WorkflowTransitionResult
                {
                    Success = false,
                    ErrorMessage = $"Document {documentId} not found"
                };
            }

            // Create structured error details
            var errorDetails = new WorkflowErrorDetails
            {
                ErrorType = errorType,
                ErrorMessage = exception.Message,
                StackTrace = exception.StackTrace,
                Timestamp = DateTime.UtcNow,
                Context = new Dictionary<string, string>
                {
                    { "CurrentState", document.WorkflowState ?? "null" },
                    { "RetryCount", document.RetryCount.ToString() },
                    { "ExceptionType", exception.GetType().Name }
                }
            };

            document.ErrorType = errorType;
            document.ErrorMessage = exception.Message;
            document.ErrorDetailsJson = JsonSerializer.Serialize(errorDetails);
            document.IsRetryable = isRetryable;
            
            // Determine next state based on retry policy
            string targetState;
            if (!isRetryable)
            {
                targetState = DocumentProcessingState.PermanentFailure;
                _logger.LogError(exception, 
                    "Non-retryable error for document {DocumentId}. Error type: {ErrorType}. Marking as permanent failure.",
                    documentId, errorType);
            }
            else if (document.RetryCount >= document.MaxRetries)
            {
                targetState = DocumentProcessingState.PermanentFailure;
                _logger.LogError(exception, 
                    "Document {DocumentId} exceeded max retries ({MaxRetries}). Error type: {ErrorType}. Marking as permanent failure.",
                    documentId, document.MaxRetries, errorType);
            }
            else
            {
                targetState = DocumentProcessingState.Failed;
                _logger.LogWarning(exception, 
                    "Transient error for document {DocumentId}. Error type: {ErrorType}. Retry {RetryCount}/{MaxRetries}.",
                    documentId, errorType, document.RetryCount, document.MaxRetries);
            }

            // Transition to error state
            var previousState = document.WorkflowState;
            document.PreviousWorkflowState = previousState;
            document.WorkflowState = targetState;
            document.StateEnteredAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return new WorkflowTransitionResult
            {
                Success = true,
                PreviousState = previousState,
                NewState = targetState,
                TransitionedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording error for document {DocumentId}", documentId);
            return new WorkflowTransitionResult
            {
                Success = false,
                ErrorMessage = $"Exception while recording error: {ex.Message}"
            };
        }
    }

    /// <inheritdoc/>
    public async Task ScheduleRetryAsync(int documentId, TimeSpan? customDelay = null)
    {
        var document = await _context.Documents.FindAsync(documentId);
        if (document == null)
        {
            _logger.LogWarning("Cannot schedule retry for non-existent document {DocumentId}", documentId);
            return;
        }

        // Calculate delay with exponential backoff
        TimeSpan delay;
        if (customDelay.HasValue)
        {
            delay = customDelay.Value;
        }
        else
        {
            var exponentialDelay = TimeSpan.FromSeconds(
                InitialRetryDelay.TotalSeconds * Math.Pow(BackoffMultiplier, document.RetryCount));
            delay = exponentialDelay > MaxRetryDelay ? MaxRetryDelay : exponentialDelay;
        }

        document.RetryCount++;
        document.LastRetryAt = DateTime.UtcNow;
        document.NextRetryAt = DateTime.UtcNow.Add(delay);

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Scheduled retry for document {DocumentId}. Retry {RetryCount}/{MaxRetries}. Next retry at {NextRetryAt}",
            documentId, document.RetryCount, document.MaxRetries, document.NextRetryAt);
    }

    /// <inheritdoc/>
    public async Task<List<Document>> GetDocumentsReadyForRetryAsync(int maxCount = 10)
    {
        var now = DateTime.UtcNow;
        
        return await _context.Documents
            .Where(d => 
                (d.WorkflowState == DocumentProcessingState.Failed ||
                 d.WorkflowState == DocumentProcessingState.Retrying) &&
                d.IsRetryable &&
                d.RetryCount < d.MaxRetries &&
                d.NextRetryAt.HasValue &&
                d.NextRetryAt.Value <= now)
            .OrderBy(d => d.NextRetryAt)
            .Take(maxCount)
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<DocumentWorkflowStatus> GetWorkflowStatusAsync(int documentId)
    {
        var document = await _context.Documents.FindAsync(documentId);
        if (document == null)
        {
            throw new ArgumentException($"Document {documentId} not found");
        }

        var status = new DocumentWorkflowStatus
        {
            DocumentId = documentId,
            CurrentState = document.WorkflowState ?? DocumentProcessingState.Queued,
            PreviousState = document.PreviousWorkflowState,
            StateEnteredAt = document.StateEnteredAt,
            RetryCount = document.RetryCount,
            MaxRetries = document.MaxRetries,
            NextRetryAt = document.NextRetryAt,
            ErrorMessage = document.ErrorMessage,
            ErrorType = document.ErrorType,
            IsRetryable = document.IsRetryable,
            ValidNextStates = DocumentStateTransitions.GetValidNextStates(document.WorkflowState ?? "")
        };

        // Parse error details if available
        if (!string.IsNullOrEmpty(document.ErrorDetailsJson))
        {
            try
            {
                var errorDetails = JsonSerializer.Deserialize<WorkflowErrorDetails>(document.ErrorDetailsJson);
                if (errorDetails != null)
                {
                    // Error details already captured in status
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse error details JSON for document {DocumentId}", documentId);
            }
        }

        return status;
    }

    /// <inheritdoc/>
    public async Task<WorkflowTransitionResult> ResetWorkflowAsync(int documentId)
    {
        try
        {
            var document = await _context.Documents.FindAsync(documentId);
            if (document == null)
            {
                return new WorkflowTransitionResult
                {
                    Success = false,
                    ErrorMessage = $"Document {documentId} not found"
                };
            }

            var previousState = document.WorkflowState;
            
            // Reset to Extracting state to restart workflow
            document.PreviousWorkflowState = previousState;
            document.WorkflowState = DocumentProcessingState.Extracting;
            document.StateEnteredAt = DateTime.UtcNow;
            
            // Reset error and retry information
            document.ErrorMessage = null;
            document.ErrorType = null;
            document.ErrorDetailsJson = null;
            document.IsRetryable = false;
            document.RetryCount = 0;
            document.NextRetryAt = null;
            document.LastRetryAt = null;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Reset workflow for document {DocumentId} from {PreviousState} to Extracting",
                documentId, previousState);

            return new WorkflowTransitionResult
            {
                Success = true,
                PreviousState = previousState,
                NewState = DocumentProcessingState.Extracting,
                TransitionedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting workflow for document {DocumentId}", documentId);
            return new WorkflowTransitionResult
            {
                Success = false,
                ErrorMessage = $"Exception during workflow reset: {ex.Message}"
            };
        }
    }
}
