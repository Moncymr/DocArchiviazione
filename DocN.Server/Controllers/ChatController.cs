using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DocN.Data;
using DocN.Data.Models;
using DocN.Data.Services.Agents;
using DocN.Core.Interfaces;
using DocN.Data.Services;

namespace DocN.Server.Controllers;

/// <summary>
/// Endpoints per il sistema RAG (Retrieval-Augmented Generation) con chat multi-agent
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ChatController : ControllerBase
{
    private readonly IAgentOrchestrator _orchestrator;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ChatController> _logger;
    private readonly IQueryIntentClassifier _queryIntentClassifier;
    private readonly IDocumentStatisticsService _documentStatisticsService;
    private readonly IStatisticalAnswerGenerator _statisticalAnswerGenerator;

    public ChatController(
        IAgentOrchestrator orchestrator,
        ApplicationDbContext context,
        ILogger<ChatController> logger,
        IQueryIntentClassifier queryIntentClassifier,
        IDocumentStatisticsService documentStatisticsService,
        IStatisticalAnswerGenerator statisticalAnswerGenerator)
    {
        _orchestrator = orchestrator;
        _context = context;
        _logger = logger;
        _queryIntentClassifier = queryIntentClassifier;
        _documentStatisticsService = documentStatisticsService;
        _statisticalAnswerGenerator = statisticalAnswerGenerator;
    }

    /// <summary>
    /// Elabora una query di chat utilizzando RAG multi-agent
    /// </summary>
    /// <param name="request">Richiesta di chat con messaggio e contesto</param>
    /// <returns>Risposta dell'AI con documenti di riferimento</returns>
    /// <response code="200">Risposta generata con successo</response>
    /// <response code="400">Richiesta non valida</response>
    /// <response code="500">Errore interno del server</response>
    [HttpPost("query")]
    [ProducesResponseType(typeof(ChatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ChatResponse>> Query([FromBody] ChatRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(new { error = "Message cannot be empty" });
            }

            _logger.LogInformation("Processing chat query: {Query}", request.Message);

            // Classify query intent to determine processing path
            var queryIntent = await _queryIntentClassifier.ClassifyAsync(request.Message);
            
            _logger.LogInformation("Query classified as: {Intent}", queryIntent);

            // Handle statistical and metadata queries without vector search
            if (queryIntent == QueryIntent.Statistical || queryIntent == QueryIntent.MetadataQuery)
            {
                var startTime = DateTime.UtcNow;
                
                // Get document statistics from database
                var statistics = await _documentStatisticsService.GetStatisticsAsync(request.UserId ?? string.Empty);
                
                // Generate natural language answer from statistics
                var answer = await _statisticalAnswerGenerator.GenerateAnswerAsync(
                    request.Message, 
                    statistics);
                
                var responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                
                _logger.LogInformation(
                    "Statistical query answered in {ResponseTime}ms without vector search",
                    responseTime);
                
                // Get or create conversation for saving
                Conversation? statConversation = null;
                if (request.ConversationId.HasValue)
                {
                    statConversation = await _context.Conversations
                        .Include(c => c.Messages)
                        .FirstOrDefaultAsync(c => c.Id == request.ConversationId.Value);
                }
                else if (!string.IsNullOrEmpty(request.UserId))
                {
                    statConversation = new Conversation
                    {
                        UserId = request.UserId,
                        Title = TruncateText(request.Message, 100),
                        CreatedAt = DateTime.UtcNow,
                        LastMessageAt = DateTime.UtcNow
                    };
                    _context.Conversations.Add(statConversation);
                    await _context.SaveChangesAsync();
                }
                
                // Save messages to conversation
                if (statConversation != null)
                {
                    var userMessage = new Message
                    {
                        ConversationId = statConversation.Id,
                        Role = "user",
                        Content = request.Message,
                        Timestamp = DateTime.UtcNow
                    };

                    var assistantMessage = new Message
                    {
                        ConversationId = statConversation.Id,
                        Role = "assistant",
                        Content = answer,
                        ReferencedDocumentIds = new List<int>(), // No specific documents for statistical queries
                        Timestamp = DateTime.UtcNow,
                        Metadata = System.Text.Json.JsonSerializer.Serialize(new
                        {
                            query_intent = queryIntent.ToString(),
                            total_documents = statistics.TotalDocuments,
                            processing_method = "statistical_aggregation",
                            response_time_ms = responseTime
                        })
                    };

                    _context.Messages.Add(userMessage);
                    _context.Messages.Add(assistantMessage);
                    statConversation.LastMessageAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
                
                return Ok(new ChatResponse
                {
                    ConversationId = statConversation?.Id,
                    Answer = answer,
                    ReferencedDocuments = new List<DocumentReference>(), // No specific documents
                    Metadata = new ChatMetadata
                    {
                        RetrievalTimeMs = responseTime,
                        SynthesisTimeMs = 0,
                        TotalTimeMs = responseTime,
                        RetrievalStrategy = "statistical_aggregation",
                        DocumentsRetrieved = 0,
                        ChunksRetrieved = 0
                    }
                });
            }

            // Get or create conversation
            Conversation? conversation = null;
            if (request.ConversationId.HasValue)
            {
                conversation = await _context.Conversations
                    .Include(c => c.Messages)
                    .FirstOrDefaultAsync(c => c.Id == request.ConversationId.Value);
            }
            else if (!string.IsNullOrEmpty(request.UserId))
            {
                // Create new conversation
                conversation = new Conversation
                {
                    UserId = request.UserId,
                    Title = TruncateText(request.Message, 100),
                    CreatedAt = DateTime.UtcNow,
                    LastMessageAt = DateTime.UtcNow
                };
                _context.Conversations.Add(conversation);
                await _context.SaveChangesAsync();
            }

            // Process query using agent orchestrator
            var result = await _orchestrator.ProcessQueryAsync(
                request.Message,
                request.UserId,
                conversation?.Id);

            // Save messages to conversation
            if (conversation != null)
            {
                var userMessage = new Message
                {
                    ConversationId = conversation.Id,
                    Role = "user",
                    Content = request.Message,
                    Timestamp = DateTime.UtcNow
                };

                var docIds = result.RetrievedDocuments.Select(d => d.Id).ToList();
                var assistantMessage = new Message
                {
                    ConversationId = conversation.Id,
                    Role = "assistant",
                    Content = result.Answer,
                    ReferencedDocumentIds = docIds,
                    Timestamp = DateTime.UtcNow,
                    Metadata = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        retrieval_time_ms = result.RetrievalTime.TotalMilliseconds,
                        synthesis_time_ms = result.SynthesisTime.TotalMilliseconds,
                        total_time_ms = result.TotalTime.TotalMilliseconds,
                        retrieval_strategy = result.RetrievalStrategy,
                        documents_retrieved = result.RetrievedDocuments.Count,
                        chunks_retrieved = result.RetrievedChunks.Count
                    })
                };

                _context.Messages.Add(userMessage);
                _context.Messages.Add(assistantMessage);

                conversation.LastMessageAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            return Ok(new ChatResponse
            {
                ConversationId = conversation?.Id,
                Answer = result.Answer,
                ReferencedDocuments = result.RetrievedDocuments.Select(d => new DocumentReference
                {
                    Id = d.Id,
                    FileName = d.FileName,
                    Category = d.ActualCategory ?? d.SuggestedCategory
                }).ToList(),
                Metadata = new ChatMetadata
                {
                    RetrievalTimeMs = result.RetrievalTime.TotalMilliseconds,
                    SynthesisTimeMs = result.SynthesisTime.TotalMilliseconds,
                    TotalTimeMs = result.TotalTime.TotalMilliseconds,
                    RetrievalStrategy = result.RetrievalStrategy,
                    DocumentsRetrieved = result.RetrievedDocuments.Count,
                    ChunksRetrieved = result.RetrievedChunks.Count
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat query");
            return StatusCode(500, new { error = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Ottiene tutte le conversazioni di un utente
    /// </summary>
    /// <param name="userId">ID dell'utente</param>
    /// <returns>Lista delle conversazioni dell'utente</returns>
    /// <response code="200">Ritorna la lista delle conversazioni</response>
    /// <response code="400">ID utente mancante</response>
    /// <response code="500">Errore interno del server</response>
    [HttpGet("conversations")]
    [ProducesResponseType(typeof(List<ConversationSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<ConversationSummary>>> GetConversations([FromQuery] string userId)
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
    /// Ottiene i messaggi di una conversazione specifica
    /// </summary>
    /// <param name="id">ID della conversazione</param>
    /// <returns>Lista dei messaggi nella conversazione</returns>
    /// <response code="200">Ritorna i messaggi</response>
    /// <response code="404">Conversazione non trovata</response>
    /// <response code="500">Errore interno del server</response>
    [HttpGet("conversations/{id}/messages")]
    [ProducesResponseType(typeof(List<Message>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<Message>>> GetMessages(int id)
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
                .ToList();

            return Ok(messages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving messages");
            return StatusCode(500, new { error = "An error occurred" });
        }
    }

    /// <summary>
    /// Elimina una conversazione
    /// </summary>
    /// <param name="id">ID della conversazione da eliminare</param>
    /// <returns>Nessun contenuto se l'operazione ha successo</returns>
    /// <response code="204">Conversazione eliminata con successo</response>
    /// <response code="404">Conversazione non trovata</response>
    /// <response code="500">Errore interno del server</response>
    [HttpDelete("conversations/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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

    private string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;
        
        return text.Substring(0, maxLength) + "...";
    }
}

/// <summary>
/// Modello di richiesta per operazioni di chat
/// </summary>
public class ChatRequest
{
    /// <summary>
    /// Il messaggio/query dell'utente
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// ID dell'utente che invia il messaggio
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// ID della conversazione (null per nuova conversazione)
    /// </summary>
    public int? ConversationId { get; set; }
}

/// <summary>
/// Modello di risposta per operazioni di chat
/// </summary>
public class ChatResponse
{
    /// <summary>
    /// ID della conversazione
    /// </summary>
    public int? ConversationId { get; set; }

    /// <summary>
    /// La risposta generata dall'AI
    /// </summary>
    public string Answer { get; set; } = string.Empty;

    /// <summary>
    /// Documenti referenziati nella risposta
    /// </summary>
    public List<DocumentReference> ReferencedDocuments { get; set; } = new();

    /// <summary>
    /// Metadati sull'elaborazione della query
    /// </summary>
    public ChatMetadata? Metadata { get; set; }
}

/// <summary>
/// Riferimento a un documento utilizzato nella risposta
/// </summary>
public class DocumentReference
{
    /// <summary>
    /// ID del documento
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// Nome del file
    /// </summary>
    public string FileName { get; set; } = string.Empty;
    
    /// <summary>
    /// Categoria del documento
    /// </summary>
    public string? Category { get; set; }
}

/// <summary>
/// Metadati sull'elaborazione della query di chat
/// </summary>
public class ChatMetadata
{
    /// <summary>
    /// Tempo di retrieval in millisecondi
    /// </summary>
    public double RetrievalTimeMs { get; set; }
    
    /// <summary>
    /// Tempo di sintesi in millisecondi
    /// </summary>
    public double SynthesisTimeMs { get; set; }
    
    /// <summary>
    /// Tempo totale in millisecondi
    /// </summary>
    public double TotalTimeMs { get; set; }
    
    /// <summary>
    /// Strategia di retrieval utilizzata
    /// </summary>
    public string RetrievalStrategy { get; set; } = string.Empty;
    
    /// <summary>
    /// Numero di documenti recuperati
    /// </summary>
    public int DocumentsRetrieved { get; set; }
    
    /// <summary>
    /// Numero di chunk recuperati
    /// </summary>
    public int ChunksRetrieved { get; set; }
}

/// <summary>
/// Riepilogo di una conversazione
/// </summary>
public class ConversationSummary
{
    /// <summary>
    /// ID della conversazione
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// Titolo della conversazione
    /// </summary>
    public string Title { get; set; } = string.Empty;
    
    /// <summary>
    /// Data di creazione
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// Data ultimo messaggio
    /// </summary>
    public DateTime LastMessageAt { get; set; }
    
    /// <summary>
    /// Numero di messaggi
    /// </summary>
    public int MessageCount { get; set; }
    
    /// <summary>
    /// Indica se la conversazione è preferita
    /// </summary>
    public bool IsStarred { get; set; }
}
