using DocN.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace DocN.Data.Services;

/// <summary>
/// Service for managing RAG agent configurations
/// </summary>
public interface IAgentConfigurationService
{
    Task<List<AgentConfiguration>> GetAgentsByUserAsync(string userId, int? tenantId = null);
    Task<List<AgentConfiguration>> GetPublicAgentsAsync(int? tenantId = null);
    Task<AgentConfiguration?> GetAgentByIdAsync(int agentId);
    Task<AgentConfiguration> CreateAgentAsync(AgentConfiguration agent);
    Task<AgentConfiguration> UpdateAgentAsync(AgentConfiguration agent);
    Task<bool> DeleteAgentAsync(int agentId);
    Task<AgentConfiguration> CreateAgentFromTemplateAsync(int templateId, string userId, int? tenantId = null);
    Task<bool> TestAgentAsync(int agentId, string testQuery);
    Task<List<AgentTemplate>> GetTemplatesAsync();
    Task<AgentTemplate?> GetTemplateByIdAsync(int templateId);
    Task LogAgentUsageAsync(AgentUsageLog log);
    Task<List<AgentUsageLog>> GetAgentUsageStatisticsAsync(int agentId, DateTime? from = null, DateTime? to = null);
}

public class AgentConfigurationService : IAgentConfigurationService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AgentConfigurationService> _logger;

    public AgentConfigurationService(
        ApplicationDbContext context,
        ILogger<AgentConfigurationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<AgentConfiguration>> GetAgentsByUserAsync(string userId, int? tenantId = null)
    {
        var query = _context.AgentConfigurations
            .Include(a => a.Template)
            .Where(a => a.OwnerId == userId && a.IsActive);

        if (tenantId.HasValue)
        {
            query = query.Where(a => a.TenantId == tenantId.Value);
        }

        return await query
            .OrderByDescending(a => a.LastUsedAt ?? a.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<AgentConfiguration>> GetPublicAgentsAsync(int? tenantId = null)
    {
        var query = _context.AgentConfigurations
            .Include(a => a.Template)
            .Where(a => a.IsPublic && a.IsActive);

        if (tenantId.HasValue)
        {
            query = query.Where(a => a.TenantId == tenantId.Value || a.TenantId == null);
        }

        return await query
            .OrderByDescending(a => a.UsageCount)
            .ToListAsync();
    }

    public async Task<AgentConfiguration?> GetAgentByIdAsync(int agentId)
    {
        return await _context.AgentConfigurations
            .Include(a => a.Template)
            .Include(a => a.Owner)
            .FirstOrDefaultAsync(a => a.Id == agentId);
    }

    public async Task<AgentConfiguration> CreateAgentAsync(AgentConfiguration agent)
    {
        agent.CreatedAt = DateTime.UtcNow;
        agent.IsActive = true;
        agent.UsageCount = 0;

        _context.AgentConfigurations.Add(agent);
        await _context.SaveChangesAsync();

        _logger.LogInformation($"Agent created: {agent.Name} (ID: {agent.Id})");

        return agent;
    }

    public async Task<AgentConfiguration> UpdateAgentAsync(AgentConfiguration agent)
    {
        agent.UpdatedAt = DateTime.UtcNow;

        _context.AgentConfigurations.Update(agent);
        await _context.SaveChangesAsync();

        _logger.LogInformation($"Agent updated: {agent.Name} (ID: {agent.Id})");

        return agent;
    }

    public async Task<bool> DeleteAgentAsync(int agentId)
    {
        var agent = await _context.AgentConfigurations.FindAsync(agentId);
        if (agent == null)
        {
            return false;
        }

        // Soft delete
        agent.IsActive = false;
        agent.UpdatedAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync();

        _logger.LogInformation($"Agent deleted: {agent.Name} (ID: {agent.Id})");

        return true;
    }

    public async Task<AgentConfiguration> CreateAgentFromTemplateAsync(int templateId, string userId, int? tenantId = null)
    {
        var template = await _context.AgentTemplates.FindAsync(templateId);
        if (template == null)
        {
            throw new ArgumentException($"Template with ID {templateId} not found");
        }

        // Parse default parameters from template with validation
        Dictionary<string, object>? defaultParams = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(template.DefaultParametersJson))
            {
                defaultParams = JsonSerializer.Deserialize<Dictionary<string, object>>(template.DefaultParametersJson);
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse template parameters for template {TemplateId}", templateId);
            // Continue with empty parameters rather than failing
        }

        var agent = new AgentConfiguration
        {
            Name = $"{template.Name} - {DateTime.UtcNow:yyyy-MM-dd HH:mm}",
            Description = template.Description,
            AgentType = template.AgentType,
            PrimaryProvider = template.RecommendedProvider,
            ModelName = template.RecommendedModel,
            SystemPrompt = template.DefaultSystemPrompt,
            OwnerId = userId,
            TenantId = tenantId,
            TemplateId = templateId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        // Apply default parameters if available with safe conversion
        if (defaultParams != null && defaultParams.Count > 0)
        {
            if (defaultParams.TryGetValue("maxDocumentsToRetrieve", out var maxDocs) && 
                TryConvertToInt(maxDocs, out var maxDocsInt))
                agent.MaxDocumentsToRetrieve = maxDocsInt;
            
            if (defaultParams.TryGetValue("similarityThreshold", out var threshold) && 
                TryConvertToDouble(threshold, out var thresholdDouble))
                agent.SimilarityThreshold = thresholdDouble;
            
            if (defaultParams.TryGetValue("temperature", out var temp) && 
                TryConvertToDouble(temp, out var tempDouble))
                agent.Temperature = tempDouble;
            
            if (defaultParams.TryGetValue("maxTokensForContext", out var contextTokens) && 
                TryConvertToInt(contextTokens, out var contextTokensInt))
                agent.MaxTokensForContext = contextTokensInt;
            
            if (defaultParams.TryGetValue("maxTokensForResponse", out var responseTokens) && 
                TryConvertToInt(responseTokens, out var responseTokensInt))
                agent.MaxTokensForResponse = responseTokensInt;
        }

        _context.AgentConfigurations.Add(agent);
        
        // Increment template usage count
        template.UsageCount++;
        
        await _context.SaveChangesAsync();

        _logger.LogInformation($"Agent created from template: {agent.Name} (Template: {template.Name})");

        return agent;
    }

    private bool TryConvertToInt(object value, out int result)
    {
        result = 0;
        try
        {
            if (value is JsonElement jsonElement)
            {
                return jsonElement.TryGetInt32(out result);
            }
            result = Convert.ToInt32(value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool TryConvertToDouble(object value, out double result)
    {
        result = 0;
        try
        {
            if (value is JsonElement jsonElement)
            {
                return jsonElement.TryGetDouble(out result);
            }
            result = Convert.ToDouble(value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> TestAgentAsync(int agentId, string testQuery)
    {
        var agent = await GetAgentByIdAsync(agentId);
        if (agent == null)
        {
            return false;
        }

        try
        {
            // Simple validation test - just check if configuration is valid
            // Actual RAG testing would be done by the RAG service
            
            if (string.IsNullOrWhiteSpace(agent.SystemPrompt))
            {
                _logger.LogWarning($"Agent {agentId} has no system prompt");
                return false;
            }

            if (agent.MaxDocumentsToRetrieve <= 0)
            {
                _logger.LogWarning($"Agent {agentId} has invalid MaxDocumentsToRetrieve");
                return false;
            }

            _logger.LogInformation($"Agent {agentId} passed validation test");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error testing agent {agentId}");
            return false;
        }
    }

    public async Task<List<AgentTemplate>> GetTemplatesAsync()
    {
        return await _context.AgentTemplates
            .Where(t => t.IsActive)
            .OrderBy(t => t.Category)
            .ThenBy(t => t.Name)
            .ToListAsync();
    }

    public async Task<AgentTemplate?> GetTemplateByIdAsync(int templateId)
    {
        return await _context.AgentTemplates.FindAsync(templateId);
    }

    public async Task LogAgentUsageAsync(AgentUsageLog log)
    {
        log.CreatedAt = DateTime.UtcNow;
        
        _context.AgentUsageLogs.Add(log);
        
        // Update agent usage statistics
        var agent = await _context.AgentConfigurations.FindAsync(log.AgentConfigurationId);
        if (agent != null)
        {
            agent.UsageCount++;
            agent.LastUsedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
    }

    public async Task<List<AgentUsageLog>> GetAgentUsageStatisticsAsync(int agentId, DateTime? from = null, DateTime? to = null)
    {
        var query = _context.AgentUsageLogs
            .Where(l => l.AgentConfigurationId == agentId);

        if (from.HasValue)
        {
            query = query.Where(l => l.CreatedAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(l => l.CreatedAt <= to.Value);
        }

        return await query
            .OrderByDescending(l => l.CreatedAt)
            .Take(100) // Limit to last 100 logs
            .ToListAsync();
    }
}
