using Microsoft.Extensions.Diagnostics.HealthChecks;
using DocN.Data.Services;

namespace DocN.Server.Services.HealthChecks;

/// <summary>
/// Health check for AI provider services
/// </summary>
public class AIProviderHealthCheck : IHealthCheck
{
    private readonly IMultiProviderAIService _aiService;
    private readonly ILogger<AIProviderHealthCheck> _logger;

    public AIProviderHealthCheck(
        IMultiProviderAIService aiService,
        ILogger<AIProviderHealthCheck> logger)
    {
        _aiService = aiService;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Try to get configuration - lightweight check
            var config = await _aiService.GetActiveConfigurationAsync();
            
            if (config == null)
            {
                return HealthCheckResult.Unhealthy(
                    "AI provider configuration not found");
            }

            // Check if at least one provider is configured
            var hasProvider = !string.IsNullOrEmpty(config.GeminiApiKey) ||
                            !string.IsNullOrEmpty(config.OpenAIApiKey) ||
                            !string.IsNullOrEmpty(config.AzureOpenAIKey);

            if (!hasProvider)
            {
                return HealthCheckResult.Degraded(
                    "No AI provider configured");
            }

            return HealthCheckResult.Healthy(
                "AI provider service is operational");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI provider health check failed");
            return HealthCheckResult.Unhealthy(
                "AI provider service is not responding",
                ex);
        }
    }
}
