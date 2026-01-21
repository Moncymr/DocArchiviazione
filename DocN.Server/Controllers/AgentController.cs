using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using DocN.Data.Models;
using DocN.Data.Services;
using System.Security.Claims;

namespace DocN.Server.Controllers;

/// <summary>
/// Endpoints per la gestione degli agenti AI configurabili
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class AgentController : ControllerBase
{
    private readonly IAgentConfigurationService _agentService;
    private readonly ILogger<AgentController> _logger;

    public AgentController(
        IAgentConfigurationService agentService,
        ILogger<AgentController> logger)
    {
        _agentService = agentService;
        _logger = logger;
    }

    private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
    private int? GetTenantId()
    {
        var tenantIdClaim = User.FindFirstValue("TenantId");
        return int.TryParse(tenantIdClaim, out var tenantId) ? tenantId : null;
    }

    /// <summary>
    /// Ottiene tutti i template di agenti disponibili per creare nuovi agenti
    /// </summary>
    /// <returns>Lista dei template disponibili</returns>
    /// <response code="200">Ritorna la lista dei template</response>
    /// <response code="500">Errore interno del server</response>
    [HttpGet("templates")]
    [ProducesResponseType(typeof(List<AgentTemplate>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<AgentTemplate>>> GetTemplates()
    {
        try
        {
            var templates = await _agentService.GetTemplatesAsync();
            return Ok(templates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting agent templates");
            return StatusCode(500, "Error retrieving templates");
        }
    }

    /// <summary>
    /// Ottiene un template specifico per ID
    /// </summary>
    /// <param name="id">ID del template</param>
    /// <returns>Il template richiesto</returns>
    /// <response code="200">Ritorna il template</response>
    /// <response code="404">Template non trovato</response>
    /// <response code="500">Errore interno del server</response>
    [HttpGet("templates/{id}")]
    [ProducesResponseType(typeof(AgentTemplate), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AgentTemplate>> GetTemplate(int id)
    {
        try
        {
            var template = await _agentService.GetTemplateByIdAsync(id);
            if (template == null)
            {
                return NotFound($"Template with ID {id} not found");
            }
            return Ok(template);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting template {TemplateId}", id);
            return StatusCode(500, "Error retrieving template");
        }
    }

    /// <summary>
    /// Ottiene tutti gli agenti dell'utente corrente
    /// </summary>
    /// <returns>Lista degli agenti dell'utente</returns>
    /// <response code="200">Ritorna la lista degli agenti</response>
    /// <response code="500">Errore interno del server</response>
    [HttpGet("my-agents")]
    [ProducesResponseType(typeof(List<AgentConfiguration>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<AgentConfiguration>>> GetMyAgents()
    {
        try
        {
            var userId = GetUserId();
            var tenantId = GetTenantId();
            
            var agents = await _agentService.GetAgentsByUserAsync(userId, tenantId);
            return Ok(agents);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user agents");
            return StatusCode(500, "Error retrieving agents");
        }
    }

    /// <summary>
    /// Ottiene tutti gli agenti pubblici disponibili al tenant
    /// </summary>
    /// <returns>Lista degli agenti pubblici</returns>
    /// <response code="200">Ritorna la lista degli agenti pubblici</response>
    /// <response code="500">Errore interno del server</response>
    [HttpGet("public")]
    [ProducesResponseType(typeof(List<AgentConfiguration>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<AgentConfiguration>>> GetPublicAgents()
    {
        try
        {
            var tenantId = GetTenantId();
            var agents = await _agentService.GetPublicAgentsAsync(tenantId);
            return Ok(agents);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting public agents");
            return StatusCode(500, "Error retrieving public agents");
        }
    }

    /// <summary>
    /// Get a specific agent by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<AgentConfiguration>> GetAgent(int id)
    {
        try
        {
            var agent = await _agentService.GetAgentByIdAsync(id);
            if (agent == null)
            {
                return NotFound($"Agent with ID {id} not found");
            }

            // Check authorization
            var userId = GetUserId();
            if (!agent.IsPublic && agent.OwnerId != userId)
            {
                return Forbid();
            }

            return Ok(agent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting agent {AgentId}", id);
            return StatusCode(500, "Error retrieving agent");
        }
    }

    /// <summary>
    /// Create a new agent from a template
    /// </summary>
    [HttpPost("create-from-template/{templateId}")]
    public async Task<ActionResult<AgentConfiguration>> CreateFromTemplate(int templateId)
    {
        try
        {
            var userId = GetUserId();
            var tenantId = GetTenantId();
            
            var agent = await _agentService.CreateAgentFromTemplateAsync(templateId, userId, tenantId);
            return CreatedAtAction(nameof(GetAgent), new { id = agent.Id }, agent);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating agent from template {TemplateId}", templateId);
            return StatusCode(500, "Error creating agent");
        }
    }

    /// <summary>
    /// Create a new custom agent
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<AgentConfiguration>> CreateAgent([FromBody] AgentConfiguration agent)
    {
        try
        {
            var userId = GetUserId();
            var tenantId = GetTenantId();
            
            agent.OwnerId = userId;
            agent.TenantId = tenantId;
            
            var createdAgent = await _agentService.CreateAgentAsync(agent);
            return CreatedAtAction(nameof(GetAgent), new { id = createdAgent.Id }, createdAgent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating agent");
            return StatusCode(500, "Error creating agent");
        }
    }

    /// <summary>
    /// Update an existing agent
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<AgentConfiguration>> UpdateAgent(int id, [FromBody] AgentConfiguration agent)
    {
        try
        {
            if (id != agent.Id)
            {
                return BadRequest("ID mismatch");
            }

            var existingAgent = await _agentService.GetAgentByIdAsync(id);
            if (existingAgent == null)
            {
                return NotFound($"Agent with ID {id} not found");
            }

            // Check authorization
            var userId = GetUserId();
            if (existingAgent.OwnerId != userId)
            {
                return Forbid();
            }

            var updatedAgent = await _agentService.UpdateAgentAsync(agent);
            return Ok(updatedAgent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating agent {AgentId}", id);
            return StatusCode(500, "Error updating agent");
        }
    }

    /// <summary>
    /// Delete an agent
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteAgent(int id)
    {
        try
        {
            var existingAgent = await _agentService.GetAgentByIdAsync(id);
            if (existingAgent == null)
            {
                return NotFound($"Agent with ID {id} not found");
            }

            // Check authorization
            var userId = GetUserId();
            if (existingAgent.OwnerId != userId)
            {
                return Forbid();
            }

            var deleted = await _agentService.DeleteAgentAsync(id);
            if (!deleted)
            {
                return NotFound();
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting agent {AgentId}", id);
            return StatusCode(500, "Error deleting agent");
        }
    }

    /// <summary>
    /// Test an agent with a sample query
    /// </summary>
    [HttpPost("{id}/test")]
    public async Task<ActionResult<TestAgentResult>> TestAgent(int id, [FromBody] TestAgentRequest request)
    {
        try
        {
            var agent = await _agentService.GetAgentByIdAsync(id);
            if (agent == null)
            {
                return NotFound($"Agent with ID {id} not found");
            }

            // Check authorization
            var userId = GetUserId();
            if (!agent.IsPublic && agent.OwnerId != userId)
            {
                return Forbid();
            }

            var isValid = await _agentService.TestAgentAsync(id, request.TestQuery);
            
            return Ok(new TestAgentResult
            {
                IsValid = isValid,
                Message = isValid ? "Agent is configured correctly" : "Agent configuration has issues"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing agent {AgentId}", id);
            return StatusCode(500, "Error testing agent");
        }
    }

    /// <summary>
    /// Get usage statistics for an agent
    /// </summary>
    [HttpGet("{id}/statistics")]
    public async Task<ActionResult<List<AgentUsageLog>>> GetAgentStatistics(
        int id,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        try
        {
            var agent = await _agentService.GetAgentByIdAsync(id);
            if (agent == null)
            {
                return NotFound($"Agent with ID {id} not found");
            }

            // Check authorization
            var userId = GetUserId();
            if (!agent.IsPublic && agent.OwnerId != userId)
            {
                return Forbid();
            }

            var stats = await _agentService.GetAgentUsageStatisticsAsync(id, from, to);
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting agent statistics {AgentId}", id);
            return StatusCode(500, "Error retrieving statistics");
        }
    }
}

public class TestAgentRequest
{
    public string TestQuery { get; set; } = string.Empty;
}

public class TestAgentResult
{
    public bool IsValid { get; set; }
    public string Message { get; set; } = string.Empty;
}
