using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DocN.Data;
using DocN.Core.Interfaces;

namespace DocN.Server.Controllers;

/// <summary>
/// Endpoints per il sistema RAG basato su Semantic Kernel con ricerca vettoriale.
/// Fornisce ricerca semantica e risposte intelligenti basate sui documenti.
/// </summary>
/// <remarks>
/// Il provider RAG viene iniettato automaticamente via Dependency Injection.
/// Per dettagli sull'inizializzazione: Vedi RAG_PROVIDER_INITIALIZATION_GUIDE.md
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class SemanticChatController : ControllerBase
{
    private readonly ISemanticRAGService _ragService;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SemanticChatController> _logger;
    private readonly IWebHostEnvironment _environment;

    /// <summary>
    /// Constructor - Il provider RAG viene iniettato automaticamente dal DI container.
    /// </summary>
    public SemanticChatController(
        ISemanticRAGService ragService,      // ← Provider RAG iniettato qui
        ApplicationDbContext context,
        ILogger<SemanticChatController> logger,
        IWebHostEnvironment environment)
    {
        _ragService = ragService;
        _context = context;
        _logger = logger;
        _environment = environment;
    }

    /// <summary>
    /// Elabora una query di chat utilizzando Semantic Kernel RAG con ricerca vettoriale
    /// </summary>
    /// <param name="request">Richiesta di chat contenente messaggio utente, userId, parametri opzionali</param>
    /// <returns>SemanticChatResponse con risposta AI, documenti fonte, score similarità e metadati</returns>
    /// <response code="200">Risposta generata con successo</response>
    /// <response code="400">Richiesta non valida (message o userId mancanti)</response>
    /// <response code="503">Provider AI non configurato</response>
    /// <response code="500">Errore interno del server</response>
    /// <remarks>
    /// Scopo: Endpoint principale per chat RAG che implementa l'intero flusso di retrieval e generation
    /// 
    /// Processo:
    /// 1. Validazione input (message e userId obbligatori)
    /// 2. Chiamata ISemanticRAGService.GenerateResponseAsync()
    ///    - Ricerca vettoriale documenti rilevanti
    ///    - Costruzione contesto da documenti
    ///    - Generazione risposta con Semantic Kernel
    ///    - Salvataggio conversazione
    /// 3. Formattazione risposta con documenti fonte e metadati
    /// 4. Gestione errori specifici (provider non configurato, errori AI, etc.)
    /// 
    /// Input:
    /// - Message: Query utente in linguaggio naturale
    /// - UserId: ID utente per accesso documenti
    /// - ConversationId (opzionale): Per mantenere contesto conversazione
    /// - SpecificDocumentIds (opzionale): Limita ricerca a documenti specifici
    /// - TopK (opzionale): Numero massimo documenti da recuperare
    /// 
    /// Output atteso:
    /// - Answer: Risposta generata dall'AI in linguaggio naturale
    /// - SourceDocuments: Lista documenti utilizzati con score di rilevanza
    /// - ConversationId: ID conversazione (nuovo o esistente)
    /// - ResponseTimeMs: Tempo elaborazione in millisecondi
    /// - Metadata: Info aggiuntive (num. documenti, top score, etc.)
    /// 
    /// Errori gestiti:
    /// - AI_PROVIDER_NOT_CONFIGURED (503): Nessun provider AI configurato
    /// - INVALID_REQUEST (400): Parametri mancanti o non validi
    /// - INTERNAL_ERROR (500): Errori generici elaborazione
    /// </remarks>
    [HttpPost("query")]
    [ProducesResponseType(typeof(SemanticChatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SemanticChatResponse>> Query([FromBody] SemanticChatRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(new { error = "Message cannot be empty" });
            }

            if (string.IsNullOrWhiteSpace(request.UserId))
            {
                return BadRequest(new { error = "User ID is required" });
            }

            _logger.LogInformation(
                "Processing semantic chat query: {Query} for user: {UserId}",
                request.Message, request.UserId);

            // Process query using Semantic RAG service
            var result = await _ragService.GenerateResponseAsync(
                request.Message,
                request.UserId,
                request.ConversationId,
                request.SpecificDocumentIds,
                request.TopK);

            return Ok(new SemanticChatResponse
            {
                ConversationId = result.ConversationId,
                Answer = result.Answer,
                SourceDocuments = result.SourceDocuments.Select(d => new SemanticDocumentReference
                {
                    DocumentId = d.DocumentId,
                    FileName = d.FileName,
                    Category = d.Category,
                    SimilarityScore = d.SimilarityScore,
                    RelevantChunk = d.RelevantChunk,
                    ChunkIndex = d.ChunkIndex
                }).ToList(),
                ResponseTimeMs = result.ResponseTimeMs,
                FromCache = result.FromCache,
                Metadata = result.Metadata
            });
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Contains("nessun provider AI ha una chiave", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("no AI provider has a valid", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("Nessuna configurazione AI attiva", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(ex, "AI provider configuration error");
            
            const string errorMessage = "AI provider not configured. Please configure at least one AI provider (Gemini, OpenAI, or Azure OpenAI) in the Settings page.";
            const string errorCode = "AI_PROVIDER_NOT_CONFIGURED";
            
            // Only include details in development environment
            if (_environment.IsDevelopment())
            {
                return StatusCode(503, new 
                { 
                    error = errorMessage,
                    errorCode = errorCode,
                    details = ex.Message
                });
            }
            
            return StatusCode(503, new 
            { 
                error = errorMessage,
                errorCode = errorCode
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing semantic chat query");
            
            // Only include detailed error messages in development
            if (_environment.IsDevelopment())
            {
                return StatusCode(500, new { error = "An error occurred while processing your request", details = ex.Message });
            }
            
            return StatusCode(500, new { error = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Process a chat query with streaming response
    /// </summary>
    [HttpPost("query/stream")]
    [Produces("text/event-stream")]
    public async IAsyncEnumerable<string> QueryStream([FromBody] SemanticChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message) || string.IsNullOrWhiteSpace(request.UserId))
        {
            yield return "data: {\"error\": \"Invalid request\"}\n\n";
            yield break;
        }

        _logger.LogInformation(
            "Processing streaming semantic chat query: {Query}",
            request.Message);

        await foreach (var chunk in _ragService.GenerateStreamingResponseAsync(
            request.Message,
            request.UserId,
            request.ConversationId,
            request.SpecificDocumentIds))
        {
            yield return $"data: {System.Text.Json.JsonSerializer.Serialize(new { content = chunk })}\n\n";
        }

        yield return "data: [DONE]\n\n";
    }

    /// <summary>
    /// Search documents using vector embeddings
    /// </summary>
    [HttpPost("search")]
    [ProducesResponseType(typeof(SearchDocumentsResponse), 200)]
    public async Task<ActionResult<SearchDocumentsResponse>> SearchDocuments(
        [FromBody] SearchDocumentsRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Query))
            {
                return BadRequest(new { error = "Query cannot be empty" });
            }

            if (string.IsNullOrWhiteSpace(request.UserId))
            {
                return BadRequest(new { error = "User ID is required" });
            }

            _logger.LogInformation(
                "Searching documents with query: {Query} for user: {UserId}",
                request.Query, request.UserId);

            var results = await _ragService.SearchDocumentsAsync(
                request.Query,
                request.UserId,
                request.TopK,
                request.MinSimilarity);

            return Ok(new SearchDocumentsResponse
            {
                Results = results.Select(r => new SemanticDocumentReference
                {
                    DocumentId = r.DocumentId,
                    FileName = r.FileName,
                    Category = r.Category,
                    SimilarityScore = r.SimilarityScore,
                    RelevantChunk = r.RelevantChunk,
                    ChunkIndex = r.ChunkIndex
                }).ToList(),
                TotalResults = results.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching documents");
            return StatusCode(500, new { error = "An error occurred" });
        }
    }

    /// <summary>
    /// Get all conversations for a user
    /// </summary>
    [HttpGet("conversations")]
    [ProducesResponseType(typeof(List<ConversationSummary>), 200)]
    public async Task<ActionResult<List<ConversationSummary>>> GetConversations(
        [FromQuery] string userId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return BadRequest(new { error = "User ID is required" });
            }

            var conversations = await _context.Conversations
                .Where(c => c.UserId == userId && !c.IsArchived)
                .OrderByDescending(c => c.LastMessageAt)
                .Select(c => new ConversationSummary
                {
                    Id = c.Id,
                    Title = c.Title,
                    CreatedAt = c.CreatedAt,
                    LastMessageAt = c.LastMessageAt,
                    MessageCount = c.Messages.Count,
                    IsStarred = c.IsStarred
                })
                .ToListAsync();

            return Ok(conversations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving conversations");
            return StatusCode(500, new { error = "An error occurred" });
        }
    }

    /// <summary>
    /// Get messages for a specific conversation
    /// </summary>
    [HttpGet("conversations/{id}/messages")]
    [ProducesResponseType(typeof(ConversationMessagesResponse), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<ConversationMessagesResponse>> GetMessages(int id)
    {
        try
        {
            var conversation = await _context.Conversations
                .Include(c => c.Messages)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (conversation == null)
            {
                return NotFound(new { error = "Conversation not found" });
            }

            var messages = conversation.Messages
                .OrderBy(m => m.Timestamp)
                .Select(m => new ConversationMessage
                {
                    Id = m.Id,
                    Role = m.Role,
                    Content = m.Content,
                    Timestamp = m.Timestamp,
                    ReferencedDocumentIds = m.ReferencedDocumentIds ?? new List<int>()
                })
                .ToList();

            return Ok(new ConversationMessagesResponse
            {
                ConversationId = conversation.Id,
                Title = conversation.Title,
                Messages = messages
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving messages");
            return StatusCode(500, new { error = "An error occurred" });
        }
    }

    /// <summary>
    /// Delete a conversation
    /// </summary>
    [HttpDelete("conversations/{id}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteConversation(int id)
    {
        try
        {
            var conversation = await _context.Conversations.FindAsync(id);
            if (conversation == null)
            {
                return NotFound(new { error = "Conversation not found" });
            }

            _context.Conversations.Remove(conversation);
            await _context.SaveChangesAsync();

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting conversation");
            return StatusCode(500, new { error = "An error occurred" });
        }
    }
}

#region Request/Response Models

/// <summary>
/// Request model for semantic chat
/// </summary>
public class SemanticChatRequest
{
    /// <summary>
    /// The user's message/query
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// User ID
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Optional conversation ID for context
    /// </summary>
    public int? ConversationId { get; set; }

    /// <summary>
    /// Optional specific document IDs to search within
    /// </summary>
    public List<int>? SpecificDocumentIds { get; set; }

    /// <summary>
    /// Number of top documents to retrieve (default: 5)
    /// </summary>
    public int TopK { get; set; } = 5;
}

/// <summary>
/// Response model for semantic chat
/// </summary>
public class SemanticChatResponse
{
    public int ConversationId { get; set; }
    public string Answer { get; set; } = string.Empty;
    public List<SemanticDocumentReference> SourceDocuments { get; set; } = new();
    public long ResponseTimeMs { get; set; }
    public bool FromCache { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Document reference with similarity score for semantic search
/// </summary>
public class SemanticDocumentReference
{
    public int DocumentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string? Category { get; set; }
    public double SimilarityScore { get; set; }
    public string? RelevantChunk { get; set; }
    public int? ChunkIndex { get; set; }
}

/// <summary>
/// Request model for document search
/// </summary>
public class SearchDocumentsRequest
{
    public string Query { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public int TopK { get; set; } = 10;
    public double MinSimilarity { get; set; } = 0.7;
}

/// <summary>
/// Response model for document search
/// </summary>
public class SearchDocumentsResponse
{
    public List<SemanticDocumentReference> Results { get; set; } = new();
    public int TotalResults { get; set; }
}

/// <summary>
/// Conversation message
/// </summary>
public class ConversationMessage
{
    public int Id { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public List<int> ReferencedDocumentIds { get; set; } = new();
}

/// <summary>
/// Response with conversation messages
/// </summary>
public class ConversationMessagesResponse
{
    public int ConversationId { get; set; }
    public string Title { get; set; } = string.Empty;
    public List<ConversationMessage> Messages { get; set; } = new();
}

#endregion
