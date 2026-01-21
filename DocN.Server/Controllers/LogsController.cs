using Microsoft.AspNetCore.Mvc;
using DocN.Data.Models;
using DocN.Data.Services;

namespace DocN.Server.Controllers;

/// <summary>
/// Endpoints per la gestione dei log di sistema
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class LogsController : ControllerBase
{
    private readonly ILogService _logService;
    private readonly ILogger<LogsController> _logger;

    public LogsController(ILogService logService, ILogger<LogsController> logger)
    {
        _logService = logService;
        _logger = logger;
    }

    /// <summary>
    /// Ottiene i log di sistema con filtri opzionali
    /// </summary>
    /// <param name="category">Categoria del log (opzionale)</param>
    /// <param name="userId">ID utente (opzionale)</param>
    /// <param name="fromDate">Data di inizio (opzionale)</param>
    /// <param name="maxRecords">Numero massimo di record da restituire (default: 100)</param>
    /// <returns>Lista dei log filtrati</returns>
    /// <response code="200">Ritorna la lista dei log</response>
    /// <response code="500">Errore interno del server</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<LogEntry>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<LogEntry>>> GetLogs(
        [FromQuery] string? category = null,
        [FromQuery] string? userId = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] int maxRecords = 100)
    {
        try
        {
            var logs = await _logService.GetLogsAsync(category, userId, fromDate, maxRecords);
            return Ok(logs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving logs");
            return StatusCode(500, "An error occurred while retrieving logs");
        }
    }

    /// <summary>
    /// Ottiene i log di upload dei file
    /// </summary>
    /// <param name="userId">ID utente (opzionale)</param>
    /// <param name="fromDate">Data di inizio (opzionale)</param>
    /// <param name="maxRecords">Numero massimo di record da restituire (default: 100)</param>
    /// <returns>Lista dei log di upload</returns>
    /// <response code="200">Ritorna la lista dei log di upload</response>
    /// <response code="500">Errore interno del server</response>
    [HttpGet("upload")]
    [ProducesResponseType(typeof(List<LogEntry>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<LogEntry>>> GetUploadLogs(
        [FromQuery] string? userId = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] int maxRecords = 100)
    {
        try
        {
            var logs = await _logService.GetUploadLogsAsync(userId, fromDate, maxRecords);
            return Ok(logs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving upload logs");
            return StatusCode(500, "An error occurred while retrieving upload logs");
        }
    }

    /// <summary>
    /// Crea una nuova voce di log
    /// </summary>
    /// <param name="request">Dati del log da creare</param>
    /// <returns>Conferma di creazione</returns>
    /// <response code="200">Log creato con successo</response>
    /// <response code="500">Errore interno del server</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> CreateLog([FromBody] CreateLogRequest request)
    {
        try
        {
            switch (request.Level?.ToLower())
            {
                case "error":
                    await _logService.LogErrorAsync(request.Category, request.Message, request.Details, request.UserId, request.FileName, request.StackTrace);
                    break;
                case "warning":
                    await _logService.LogWarningAsync(request.Category, request.Message, request.Details, request.UserId, request.FileName);
                    break;
                case "debug":
                    await _logService.LogDebugAsync(request.Category, request.Message, request.Details, request.UserId, request.FileName);
                    break;
                default:
                    await _logService.LogInfoAsync(request.Category, request.Message, request.Details, request.UserId, request.FileName);
                    break;
            }

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating log entry");
            return StatusCode(500, "An error occurred while creating log entry");
        }
    }
}

/// <summary>
/// Modello di richiesta per la creazione di un log
/// </summary>
public class CreateLogRequest
{
    /// <summary>
    /// Livello del log (Info, Warning, Error, Debug)
    /// </summary>
    public string Level { get; set; } = "Info";
    
    /// <summary>
    /// Categoria del log
    /// </summary>
    public string Category { get; set; } = string.Empty;
    
    /// <summary>
    /// Messaggio del log
    /// </summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary>
    /// Dettagli aggiuntivi (opzionale)
    /// </summary>
    public string? Details { get; set; }
    
    /// <summary>
    /// ID dell'utente (opzionale)
    /// </summary>
    public string? UserId { get; set; }
    
    /// <summary>
    /// Nome del file (opzionale)
    /// </summary>
    public string? FileName { get; set; }
    
    /// <summary>
    /// Stack trace (opzionale, solo per errori)
    /// </summary>
    public string? StackTrace { get; set; }
}
