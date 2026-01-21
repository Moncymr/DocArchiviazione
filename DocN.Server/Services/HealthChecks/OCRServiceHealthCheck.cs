using Microsoft.Extensions.Diagnostics.HealthChecks;
using DocN.Core.Interfaces;

namespace DocN.Server.Services.HealthChecks;

/// <summary>
/// Health check for OCR service (Tesseract)
/// </summary>
public class OCRServiceHealthCheck : IHealthCheck
{
    private readonly IOCRService? _ocrService;
    private readonly ILogger<OCRServiceHealthCheck> _logger;

    public OCRServiceHealthCheck(
        IServiceProvider serviceProvider,
        ILogger<OCRServiceHealthCheck> logger)
    {
        // OCR service may not be registered if Tesseract is not installed
        _ocrService = serviceProvider.GetService<IOCRService>();
        _logger = logger;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (_ocrService == null)
            {
                return Task.FromResult(HealthCheckResult.Degraded(
                    "OCR service not configured (Tesseract not installed)"));
            }

            // OCR service exists and is registered
            return Task.FromResult(HealthCheckResult.Healthy(
                "OCR service is available"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OCR service health check failed");
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "OCR service check failed",
                ex));
        }
    }
}
