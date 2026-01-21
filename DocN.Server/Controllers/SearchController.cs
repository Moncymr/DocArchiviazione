using Microsoft.AspNetCore.Mvc;
using DocN.Data.Services;
using DocN.Data.Models;

namespace DocN.Server.Controllers;

/// <summary>
/// Endpoints per la funzionalità di ricerca documenti con supporto ibrido (vettoriale + full-text)
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class SearchController : ControllerBase
{
    private readonly IHybridSearchService _searchService;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<SearchController> _logger;

    public SearchController(
        IHybridSearchService searchService,
        IEmbeddingService embeddingService,
        ILogger<SearchController> logger)
    {
        _searchService = searchService;
        _embeddingService = embeddingService;
        _logger = logger;
    }

    /// <summary>
    /// Esegue una ricerca ibrida combinando similarità vettoriale e ricerca full-text
    /// </summary>
    /// <param name="request">Parametri della richiesta di ricerca</param>
    /// <returns>Lista dei risultati di ricerca con punteggi di rilevanza</returns>
    /// <response code="200">Ricerca completata con successo</response>
    /// <response code="400">Richiesta non valida (query vuota)</response>
    /// <response code="500">Errore interno del server</response>
    [HttpPost("hybrid")]
    [ProducesResponseType(typeof(SearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SearchResponse>> HybridSearch([FromBody] SearchRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Query))
            {
                return BadRequest(new { error = "Query cannot be empty" });
            }

            var startTime = DateTime.UtcNow;

            var options = new SearchOptions
            {
                TopK = request.TopK ?? 10,
                MinSimilarity = request.MinSimilarity ?? 0.3,
                CategoryFilter = request.CategoryFilter,
                OwnerId = request.UserId,
                VisibilityFilter = request.VisibilityFilter
            };

            var results = await _searchService.SearchAsync(request.Query, options);
            var elapsedTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

            // Truncate query for logging to prevent sensitive data exposure
            var logQuery = request.Query.Length > 50 ? request.Query.Substring(0, 47) + "..." : request.Query;

            _logger.LogInformation(
                "Hybrid search completed for query '{Query}' - Found {Count} results in {Time}ms",
                logQuery, results.Count, elapsedTime);

            return Ok(new SearchResponse
            {
                Query = request.Query,
                Results = results,
                TotalResults = results.Count,
                QueryTimeMs = elapsedTime,
                SearchType = "hybrid"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing hybrid search");
            return StatusCode(500, new { error = "An error occurred during search" });
        }
    }

    /// <summary>
    /// Esegue una ricerca solo vettoriale utilizzando la similarità semantica
    /// </summary>
    /// <param name="request">Parametri della richiesta di ricerca</param>
    /// <returns>Lista dei risultati di ricerca basati su similarità vettoriale</returns>
    /// <response code="200">Ricerca completata con successo</response>
    /// <response code="400">Richiesta non valida</response>
    /// <response code="500">Errore interno del server</response>
    [HttpPost("vector")]
    [ProducesResponseType(typeof(SearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SearchResponse>> VectorSearch([FromBody] SearchRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Query))
            {
                return BadRequest(new { error = "Query cannot be empty" });
            }

            var startTime = DateTime.UtcNow;

            var options = new SearchOptions
            {
                TopK = request.TopK ?? 10,
                MinSimilarity = request.MinSimilarity ?? 0.3,
                CategoryFilter = request.CategoryFilter,
                OwnerId = request.UserId
            };

            // Generate embedding first
            var embedding = await GetQueryEmbeddingAsync(request.Query);
            if (embedding == null)
            {
                _logger.LogWarning("Failed to generate query embedding for query: {Query}", request.Query);
                return BadRequest(new { error = "Failed to generate query embedding" });
            }

            _logger.LogDebug("Generated embedding with dimension {Dimension} for query", embedding.Length);

            var results = await _searchService.VectorSearchAsync(embedding, options);
            var elapsedTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

            // Truncate query for logging to prevent sensitive data exposure
            var logQuery = request.Query.Length > 50 ? request.Query.Substring(0, 47) + "..." : request.Query;
            
            _logger.LogInformation(
                "Vector search completed for query '{Query}' - Found {Count} results in {Time}ms (MinSimilarity: {MinSim:F2}, UserId: {UserId}, EmbeddingDim: {EmbedDim})",
                logQuery, results.Count, elapsedTime, options.MinSimilarity, request.UserId ?? "none", embedding.Length);

            return Ok(new SearchResponse
            {
                Query = request.Query,
                Results = results,
                TotalResults = results.Count,
                QueryTimeMs = elapsedTime,
                SearchType = "vector"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing vector search");
            return StatusCode(500, new { error = "An error occurred during search" });
        }
    }

    /// <summary>
    /// Esegue una ricerca solo full-text
    /// </summary>
    /// <param name="request">Parametri della richiesta di ricerca</param>
    /// <returns>Lista dei risultati di ricerca basati su corrispondenza testuale</returns>
    /// <response code="200">Ricerca completata con successo</response>
    /// <response code="400">Richiesta non valida</response>
    /// <response code="500">Errore interno del server</response>
    [HttpPost("text")]
    [ProducesResponseType(typeof(SearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SearchResponse>> TextSearch([FromBody] SearchRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Query))
            {
                return BadRequest(new { error = "Query cannot be empty" });
            }

            var startTime = DateTime.UtcNow;

            var options = new SearchOptions
            {
                TopK = request.TopK ?? 10,
                CategoryFilter = request.CategoryFilter,
                OwnerId = request.UserId
            };

            var results = await _searchService.TextSearchAsync(request.Query, options);
            var elapsedTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

            return Ok(new SearchResponse
            {
                Query = request.Query,
                Results = results,
                TotalResults = results.Count,
                QueryTimeMs = elapsedTime,
                SearchType = "text"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing text search");
            return StatusCode(500, new { error = "An error occurred during search" });
        }
    }

    private async Task<float[]?> GetQueryEmbeddingAsync(string query)
    {
        return await _embeddingService.GenerateEmbeddingAsync(query);
    }
}

/// <summary>
/// Modello di richiesta per operazioni di ricerca
/// </summary>
public class SearchRequest
{
    /// <summary>
    /// Il testo della query di ricerca
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Numero massimo di risultati da restituire (default: 10)
    /// </summary>
    public int? TopK { get; set; }

    /// <summary>
    /// Soglia minima di similarità (0-1, default: 0.7)
    /// </summary>
    public double? MinSimilarity { get; set; }

    /// <summary>
    /// Filtro per categoria
    /// </summary>
    public string? CategoryFilter { get; set; }

    /// <summary>
    /// Filtro per ID utente (per documenti privati)
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Filtro per livello di visibilità
    /// </summary>
    public DocumentVisibility? VisibilityFilter { get; set; }
}

/// <summary>
/// Modello di risposta per operazioni di ricerca
/// </summary>
public class SearchResponse
{
    /// <summary>
    /// La query originale
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Risultati di ricerca con punteggi
    /// </summary>
    public List<SearchResult> Results { get; set; } = new();

    /// <summary>
    /// Numero totale di risultati trovati
    /// </summary>
    public int TotalResults { get; set; }

    /// <summary>
    /// Tempo impiegato per la query in millisecondi
    /// </summary>
    public double QueryTimeMs { get; set; }

    /// <summary>
    /// Tipo di ricerca eseguita (hybrid, vector, text)
    /// </summary>
    public string SearchType { get; set; } = string.Empty;
}
