using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.SemanticKernel;
using DocN.Data.Services;

namespace DocN.Server.Services.HealthChecks;

/// <summary>
/// Health check for Semantic Kernel orchestration
/// Now uses database configuration through IKernelProvider
/// </summary>
public class SemanticKernelHealthCheck : IHealthCheck
{
    private readonly IKernelProvider _kernelProvider;
    private readonly ILogger<SemanticKernelHealthCheck> _logger;

    public SemanticKernelHealthCheck(
        IKernelProvider kernelProvider,
        ILogger<SemanticKernelHealthCheck> logger)
    {
        _kernelProvider = kernelProvider;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get kernel from database configuration
            var kernel = await _kernelProvider.GetKernelAsync();
            
            // Check if kernel has services configured
            var hasServices = kernel.Services != null;
            
            if (!hasServices)
            {
                return HealthCheckResult.Degraded(
                    "Semantic Kernel has no services configured. Check AI provider configuration in database.");
            }

            return HealthCheckResult.Healthy(
                "Semantic Kernel orchestration is operational with database configuration");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Semantic Kernel health check failed");
            return HealthCheckResult.Unhealthy(
                "Semantic Kernel health check failed. Check AI provider configuration in database.",
                ex);
        }
    }
}
