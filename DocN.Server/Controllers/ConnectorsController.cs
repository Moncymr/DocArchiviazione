using Microsoft.AspNetCore.Mvc;
using DocN.Data.Services;
using DocN.Data.Services.Connectors;
using DocN.Data.Models;

namespace DocN.Server.Controllers;

/// <summary>
/// API Controller for managing document connectors
/// </summary>
[ApiController]
[Route("[controller]")]
public class ConnectorsController : ControllerBase
{
    private readonly IConnectorService _connectorService;
    private readonly ILogger<ConnectorsController> _logger;

    public ConnectorsController(IConnectorService connectorService, ILogger<ConnectorsController> logger)
    {
        _connectorService = connectorService;
        _logger = logger;
    }

    /// <summary>
    /// Gets all connectors for the current user
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<DocumentConnector>>> GetConnectors()
    {
        try
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "system";
            
            var connectors = await _connectorService.GetUserConnectorsAsync(userId);
            return Ok(connectors);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving connectors");
            return StatusCode(500, "An error occurred while retrieving connectors");
        }
    }

    /// <summary>
    /// Gets a specific connector by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<DocumentConnector>> GetConnector(int id)
    {
        try
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "system";
            
            var connector = await _connectorService.GetConnectorAsync(id, userId);
            if (connector == null)
            {
                return NotFound();
            }
            
            return Ok(connector);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving connector {ConnectorId}", id);
            return StatusCode(500, "An error occurred while retrieving the connector");
        }
    }

    /// <summary>
    /// Creates a new connector
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<DocumentConnector>> CreateConnector(DocumentConnector connector)
    {
        try
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "system";
            
            connector.OwnerId = userId;
            var created = await _connectorService.CreateConnectorAsync(connector);
            
            return CreatedAtAction(nameof(GetConnector), new { id = created.Id }, created);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid connector configuration");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating connector");
            return StatusCode(500, "An error occurred while creating the connector");
        }
    }

    /// <summary>
    /// Updates an existing connector
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<DocumentConnector>> UpdateConnector(int id, DocumentConnector connector)
    {
        try
        {
            if (id != connector.Id)
            {
                return BadRequest("ID mismatch");
            }
            
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "system";
            
            var updated = await _connectorService.UpdateConnectorAsync(connector, userId);
            return Ok(updated);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating connector {ConnectorId}", id);
            return StatusCode(500, "An error occurred while updating the connector");
        }
    }

    /// <summary>
    /// Deletes a connector
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteConnector(int id)
    {
        try
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "system";
            
            var result = await _connectorService.DeleteConnectorAsync(id, userId);
            if (!result)
            {
                return NotFound();
            }
            
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting connector {ConnectorId}", id);
            return StatusCode(500, "An error occurred while deleting the connector");
        }
    }

    /// <summary>
    /// Tests connection to external repository
    /// </summary>
    [HttpPost("{id}/test")]
    public async Task<ActionResult<object>> TestConnection(int id)
    {
        try
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "system";
            
            var (success, message) = await _connectorService.TestConnectionAsync(id, userId);
            return Ok(new { success, message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing connection for connector {ConnectorId}", id);
            return StatusCode(500, "An error occurred while testing the connection");
        }
    }

    /// <summary>
    /// Lists files from the connector
    /// </summary>
    [HttpGet("{id}/files")]
    public async Task<ActionResult<IEnumerable<ConnectorFileInfo>>> ListFiles(int id, [FromQuery] string? path = null)
    {
        try
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "system";
            
            var files = await _connectorService.ListFilesAsync(id, userId, path);
            return Ok(files);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing files from connector {ConnectorId}", id);
            return StatusCode(500, "An error occurred while listing files");
        }
    }
}
