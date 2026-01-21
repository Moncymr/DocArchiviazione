namespace DocN.Data.Constants;

/// <summary>
/// Enhanced document processing states with better error handling and state management
/// </summary>
public static class DocumentProcessingState
{
    /// <summary>
    /// Document is queued for initial upload processing
    /// </summary>
    public const string Queued = "Queued";
    
    /// <summary>
    /// Document content is being extracted (text, metadata, etc.)
    /// </summary>
    public const string Extracting = "Extracting";
    
    /// <summary>
    /// Document is being analyzed by AI (category, tags, metadata extraction)
    /// </summary>
    public const string Analyzing = "Analyzing";
    
    /// <summary>
    /// AI has suggested metadata, waiting for user confirmation
    /// </summary>
    public const string AwaitingConfirmation = "AwaitingConfirmation";
    
    /// <summary>
    /// Metadata confirmed, document is being chunked for embedding
    /// </summary>
    public const string Chunking = "Chunking";
    
    /// <summary>
    /// Document chunks are being embedded with vector representations
    /// </summary>
    public const string Embedding = "Embedding";
    
    /// <summary>
    /// Document is being indexed for search
    /// </summary>
    public const string Indexing = "Indexing";
    
    /// <summary>
    /// Document processing completed successfully and is ready for use
    /// </summary>
    public const string Completed = "Completed";
    
    /// <summary>
    /// Processing failed with errors, may be retryable
    /// </summary>
    public const string Failed = "Failed";
    
    /// <summary>
    /// Processing failed and retries exhausted
    /// </summary>
    public const string PermanentFailure = "PermanentFailure";
    
    /// <summary>
    /// Document processing has been paused/cancelled by user
    /// </summary>
    public const string Cancelled = "Cancelled";
    
    /// <summary>
    /// Document is in retry queue after a transient failure
    /// </summary>
    public const string Retrying = "Retrying";
}

/// <summary>
/// Error types for structured error handling
/// </summary>
public static class DocumentErrorType
{
    /// <summary>
    /// File format not supported or corrupted
    /// </summary>
    public const string InvalidFileFormat = "InvalidFileFormat";
    
    /// <summary>
    /// Text extraction failed
    /// </summary>
    public const string ExtractionError = "ExtractionError";
    
    /// <summary>
    /// AI service error (rate limits, service down, etc.)
    /// </summary>
    public const string AIServiceError = "AIServiceError";
    
    /// <summary>
    /// Embedding generation failed
    /// </summary>
    public const string EmbeddingError = "EmbeddingError";
    
    /// <summary>
    /// Database error during processing
    /// </summary>
    public const string DatabaseError = "DatabaseError";
    
    /// <summary>
    /// Network/connectivity error
    /// </summary>
    public const string NetworkError = "NetworkError";
    
    /// <summary>
    /// File too large to process
    /// </summary>
    public const string FileTooLarge = "FileTooLarge";
    
    /// <summary>
    /// Quota/limit exceeded
    /// </summary>
    public const string QuotaExceeded = "QuotaExceeded";
    
    /// <summary>
    /// Permission/authorization error
    /// </summary>
    public const string PermissionError = "PermissionError";
    
    /// <summary>
    /// Unknown or unexpected error
    /// </summary>
    public const string UnknownError = "UnknownError";
}

/// <summary>
/// Valid state transitions for document processing workflow
/// </summary>
public static class DocumentStateTransitions
{
    private static readonly Dictionary<string, List<string>> ValidTransitions = new()
    {
        { DocumentProcessingState.Queued, new() { 
            DocumentProcessingState.Extracting,
            DocumentProcessingState.Failed,
            DocumentProcessingState.Cancelled
        }},
        { DocumentProcessingState.Extracting, new() { 
            DocumentProcessingState.Analyzing,
            DocumentProcessingState.Failed,
            DocumentProcessingState.Retrying,
            DocumentProcessingState.Cancelled
        }},
        { DocumentProcessingState.Analyzing, new() { 
            DocumentProcessingState.AwaitingConfirmation,
            DocumentProcessingState.Chunking, // Can skip confirmation
            DocumentProcessingState.Failed,
            DocumentProcessingState.Retrying,
            DocumentProcessingState.Cancelled
        }},
        { DocumentProcessingState.AwaitingConfirmation, new() { 
            DocumentProcessingState.Chunking,
            DocumentProcessingState.Cancelled
        }},
        { DocumentProcessingState.Chunking, new() { 
            DocumentProcessingState.Embedding,
            DocumentProcessingState.Failed,
            DocumentProcessingState.Retrying,
            DocumentProcessingState.Cancelled
        }},
        { DocumentProcessingState.Embedding, new() { 
            DocumentProcessingState.Indexing,
            DocumentProcessingState.Failed,
            DocumentProcessingState.Retrying,
            DocumentProcessingState.Cancelled
        }},
        { DocumentProcessingState.Indexing, new() { 
            DocumentProcessingState.Completed,
            DocumentProcessingState.Failed,
            DocumentProcessingState.Retrying
        }},
        { DocumentProcessingState.Completed, new() { 
            // Completed is a terminal state, but can be re-processed
            DocumentProcessingState.Extracting
        }},
        { DocumentProcessingState.Failed, new() { 
            DocumentProcessingState.Retrying,
            DocumentProcessingState.PermanentFailure,
            DocumentProcessingState.Extracting // Manual restart
        }},
        { DocumentProcessingState.PermanentFailure, new() { 
            DocumentProcessingState.Extracting // Manual restart only
        }},
        { DocumentProcessingState.Cancelled, new() { 
            DocumentProcessingState.Extracting // Manual restart
        }},
        { DocumentProcessingState.Retrying, new() { 
            DocumentProcessingState.Extracting,
            DocumentProcessingState.Analyzing,
            DocumentProcessingState.Chunking,
            DocumentProcessingState.Embedding,
            DocumentProcessingState.Indexing,
            DocumentProcessingState.Failed,
            DocumentProcessingState.PermanentFailure
        }}
    };

    /// <summary>
    /// Check if a state transition is valid
    /// </summary>
    public static bool IsValidTransition(string fromState, string toState)
    {
        if (string.IsNullOrEmpty(fromState))
            return true; // First state assignment is always valid
            
        if (!ValidTransitions.ContainsKey(fromState))
            return false;
            
        return ValidTransitions[fromState].Contains(toState);
    }

    /// <summary>
    /// Get valid next states for a given state
    /// </summary>
    public static List<string> GetValidNextStates(string currentState)
    {
        if (string.IsNullOrEmpty(currentState) || !ValidTransitions.ContainsKey(currentState))
            return new List<string>();
            
        return new List<string>(ValidTransitions[currentState]);
    }
}
