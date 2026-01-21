using Microsoft.AspNetCore.Mvc;
using DocN.Core.Interfaces;
using Microsoft.AspNetCore.RateLimiting;

namespace DocN.Server.Controllers;

/// <summary>
/// Controller for alert management and monitoring
/// </summary>
[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("api")]
public class AlertsController : ControllerBase
{
    private readonly IAlertingService _alertingService;
    private readonly ILogger<AlertsController> _logger;

    public AlertsController(
        IAlertingService alertingService,
        ILogger<AlertsController> logger)
    {
        _alertingService = alertingService;
        _logger = logger;
    }

    /// <summary>
    /// Get all active alerts
    /// </summary>
    [HttpGet("active")]
    public async Task<IActionResult> GetActiveAlerts(CancellationToken cancellationToken)
    {
        try
        {
            var alerts = await _alertingService.GetActiveAlertsAsync(cancellationToken);
            return Ok(alerts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active alerts");
            return StatusCode(500, new { error = "Failed to retrieve active alerts" });
        }
    }

    /// <summary>
    /// Get alert statistics
    /// </summary>
    [HttpGet("statistics")]
    public async Task<IActionResult> GetStatistics(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        try
        {
            var statistics = await _alertingService.GetAlertStatisticsAsync(from, to, cancellationToken);
            return Ok(statistics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting alert statistics");
            return StatusCode(500, new { error = "Failed to retrieve alert statistics" });
        }
    }

    /// <summary>
    /// Acknowledge an alert
    /// </summary>
    [HttpPost("{alertId}/acknowledge")]
    public async Task<IActionResult> AcknowledgeAlert(
        string alertId,
        [FromBody] AcknowledgeRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _alertingService.AcknowledgeAlertAsync(
                alertId, 
                request.AcknowledgedBy, 
                cancellationToken);
            
            return Ok(new { message = "Alert acknowledged successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error acknowledging alert: {AlertId}", alertId);
            return StatusCode(500, new { error = "Failed to acknowledge alert" });
        }
    }

    /// <summary>
    /// Resolve an alert
    /// </summary>
    [HttpPost("{alertId}/resolve")]
    public async Task<IActionResult> ResolveAlert(
        string alertId,
        [FromBody] ResolveRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _alertingService.ResolveAlertAsync(
                alertId, 
                request.ResolvedBy, 
                cancellationToken);
            
            return Ok(new { message = "Alert resolved successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving alert: {AlertId}", alertId);
            return StatusCode(500, new { error = "Failed to resolve alert" });
        }
    }

    /// <summary>
    /// Send a test alert
    /// </summary>
    [HttpPost("test")]
    public async Task<IActionResult> SendTestAlert(
        [FromBody] TestAlertRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var alert = new Alert
            {
                Name = request.Name ?? "Test Alert",
                Description = request.Description ?? "This is a test alert",
                Severity = Enum.Parse<AlertSeverity>(request.Severity ?? "Info", true),
                Source = "AlertsController"
            };
            
            await _alertingService.SendAlertAsync(alert, cancellationToken);
            
            return Ok(new { message = "Test alert sent successfully", alertId = alert.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending test alert");
            return StatusCode(500, new { error = "Failed to send test alert" });
        }
    }

    /// <summary>
    /// Generate sample alerts for demonstration
    /// </summary>
    [HttpPost("generate-samples")]
    public async Task<IActionResult> GenerateSampleAlerts(CancellationToken cancellationToken)
    {
        try
        {
            var sampleAlerts = new List<Alert>
            {
                new Alert
                {
                    Name = "HighCPU",
                    Description = "CPU usage è al 92% da 5 minuti",
                    Severity = AlertSeverity.Critical,
                    Source = "SystemMonitor",
                    Labels = new Dictionary<string, object>
                    {
                        ["cpu_usage"] = 92.5,
                        ["threshold"] = 90.0
                    },
                    Annotations = new Dictionary<string, object>
                    {
                        ["runbook"] = "docs/ALERTING_RUNBOOK.md#1-highcpu"
                    }
                },
                new Alert
                {
                    Name = "HighLatency",
                    Description = "Latenza API /api/search è 2.5s (P95)",
                    Severity = AlertSeverity.Warning,
                    Source = "APIMonitor",
                    Labels = new Dictionary<string, object>
                    {
                        ["latency_ms"] = 2500,
                        ["endpoint"] = "/api/search"
                    }
                },
                new Alert
                {
                    Name = "LowRAGQuality",
                    Description = "Confidence score RAG è sceso a 0.65 (soglia: 0.70)",
                    Severity = AlertSeverity.Warning,
                    Source = "RAGQualityMonitor",
                    Labels = new Dictionary<string, object>
                    {
                        ["confidence_score"] = 0.65,
                        ["threshold"] = 0.70
                    }
                },
                new Alert
                {
                    Name = "HallucinationsDetected",
                    Description = "Rilevate 3 potenziali allucinazioni nelle ultime 10 risposte",
                    Severity = AlertSeverity.Warning,
                    Source = "RAGQualityMonitor",
                    Labels = new Dictionary<string, object>
                    {
                        ["hallucination_count"] = 3,
                        ["total_responses"] = 10
                    }
                },
                new Alert
                {
                    Name = "DatabaseConnectionSlow",
                    Description = "Connessioni database > 500ms",
                    Severity = AlertSeverity.Info,
                    Source = "DatabaseMonitor"
                }
            };

            foreach (var alert in sampleAlerts)
            {
                await _alertingService.SendAlertAsync(alert, cancellationToken);
            }
            
            return Ok(new 
            { 
                message = "Sample alerts generated successfully", 
                count = sampleAlerts.Count,
                alerts = sampleAlerts.Select(a => new { a.Name, a.Severity, a.Description })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating sample alerts");
            return StatusCode(500, new { error = "Failed to generate sample alerts" });
        }
    }
}

public record AcknowledgeRequest(string AcknowledgedBy);
public record ResolveRequest(string ResolvedBy);
public record TestAlertRequest(string? Name, string? Description, string? Severity);
