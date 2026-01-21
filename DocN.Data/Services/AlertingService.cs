using DocN.Core.Interfaces;
using DocN.Core.AI.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Net.Mail;
using System.Net;

namespace DocN.Data.Services;

/// <summary>
/// Service for managing alerts and notifications
/// </summary>
public class AlertingService : IAlertingService
{
    private readonly ILogger<AlertingService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AlertManagerConfiguration _config;
    private readonly ConcurrentDictionary<string, Alert> _activeAlerts = new();

    public AlertingService(
        ILogger<AlertingService> logger,
        IHttpClientFactory httpClientFactory,
        IOptions<AlertManagerConfiguration> config)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _config = config.Value;
    }

    public async Task SendAlertAsync(Alert alert, CancellationToken cancellationToken = default)
    {
        try
        {
            // Store alert in memory
            _activeAlerts.AddOrUpdate(alert.Id, alert, (_, _) => alert);
            
            _logger.LogWarning(
                "Alert fired: {AlertName} (Severity: {Severity})", 
                alert.Name, 
                alert.Severity);

            // Send to AlertManager if configured
            if (_config.Enabled && !string.IsNullOrEmpty(_config.Endpoint))
            {
                await SendToAlertManagerAsync(alert, cancellationToken);
            }

            // Send to configured notification channels
            await SendNotificationsAsync(alert, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending alert: {AlertName}", alert.Name);
        }
    }

    public Task<IEnumerable<Alert>> GetActiveAlertsAsync(CancellationToken cancellationToken = default)
    {
        var activeAlerts = _activeAlerts.Values
            .Where(a => a.Status == AlertStatus.Firing || a.Status == AlertStatus.Acknowledged)
            .OrderByDescending(a => a.StartsAt);
        
        return Task.FromResult(activeAlerts.AsEnumerable());
    }

    public Task AcknowledgeAlertAsync(string alertId, string acknowledgedBy, CancellationToken cancellationToken = default)
    {
        if (_activeAlerts.TryGetValue(alertId, out var alert))
        {
            alert.Status = AlertStatus.Acknowledged;
            alert.AcknowledgedBy = acknowledgedBy;
            alert.AcknowledgedAt = DateTime.UtcNow;
            
            _logger.LogInformation(
                "Alert acknowledged: {AlertName} by {User}", 
                alert.Name, 
                acknowledgedBy);
        }
        
        return Task.CompletedTask;
    }

    public Task ResolveAlertAsync(string alertId, string resolvedBy, CancellationToken cancellationToken = default)
    {
        if (_activeAlerts.TryGetValue(alertId, out var alert))
        {
            alert.Status = AlertStatus.Resolved;
            alert.ResolvedBy = resolvedBy;
            alert.ResolvedAt = DateTime.UtcNow;
            alert.EndsAt = DateTime.UtcNow;
            
            _logger.LogInformation(
                "Alert resolved: {AlertName} by {User}", 
                alert.Name, 
                resolvedBy);
        }
        
        return Task.CompletedTask;
    }

    public Task<AlertStatistics> GetAlertStatisticsAsync(
        DateTime? from = null, 
        DateTime? to = null, 
        CancellationToken cancellationToken = default)
    {
        var fromDate = from ?? DateTime.UtcNow.AddDays(-7);
        var toDate = to ?? DateTime.UtcNow;
        
        var alerts = _activeAlerts.Values
            .Where(a => a.StartsAt >= fromDate && a.StartsAt <= toDate)
            .ToList();
        
        var statistics = new AlertStatistics
        {
            TotalAlerts = alerts.Count,
            ActiveAlerts = alerts.Count(a => a.Status == AlertStatus.Firing),
            AcknowledgedAlerts = alerts.Count(a => a.Status == AlertStatus.Acknowledged),
            ResolvedAlerts = alerts.Count(a => a.Status == AlertStatus.Resolved),
            AlertsBySeverity = alerts
                .GroupBy(a => a.Severity)
                .ToDictionary(g => g.Key, g => g.Count()),
            AlertsBySource = alerts
                .GroupBy(a => a.Source)
                .ToDictionary(g => g.Key, g => g.Count()),
            AverageTimeToResolve = CalculateAverageTimeToResolve(alerts)
        };
        
        return Task.FromResult(statistics);
    }

    private async Task SendToAlertManagerAsync(Alert alert, CancellationToken cancellationToken)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            var endpoint = $"{_config.Endpoint}/api/v2/alerts";
            
            var alertPayload = new[]
            {
                new
                {
                    labels = new Dictionary<string, string>
                    {
                        ["alertname"] = alert.Name,
                        ["severity"] = alert.Severity.ToString().ToLower(),
                        ["source"] = alert.Source
                    },
                    annotations = new Dictionary<string, string>
                    {
                        ["description"] = alert.Description,
                        ["summary"] = $"{alert.Name}: {alert.Description}"
                    },
                    startsAt = alert.StartsAt.ToString("o"),
                    endsAt = alert.EndsAt?.ToString("o")
                }
            };
            
            var response = await httpClient.PostAsJsonAsync(endpoint, alertPayload, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            _logger.LogInformation("Alert sent to AlertManager: {AlertName}", alert.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send alert to AlertManager: {AlertName}", alert.Name);
        }
    }

    private async Task SendNotificationsAsync(Alert alert, CancellationToken cancellationToken)
    {
        var tasks = new List<Task>();
        
        // Send email notification
        if (_config.Routing.Email?.Enabled == true)
        {
            tasks.Add(SendEmailNotificationAsync(alert, cancellationToken));
        }
        
        // Send Slack notification
        if (_config.Routing.Slack?.Enabled == true)
        {
            tasks.Add(SendSlackNotificationAsync(alert, cancellationToken));
        }
        
        // Send webhook notifications
        foreach (var webhook in _config.Routing.Webhooks.Where(w => w.Enabled))
        {
            tasks.Add(SendWebhookNotificationAsync(alert, webhook, cancellationToken));
        }
        
        await Task.WhenAll(tasks);
    }

    private async Task SendEmailNotificationAsync(Alert alert, CancellationToken cancellationToken)
    {
        try
        {
            var emailConfig = _config.Routing.Email!;
            using var smtpClient = new SmtpClient(emailConfig.SmtpHost, emailConfig.SmtpPort)
            {
                Credentials = new NetworkCredential(emailConfig.SmtpUsername, emailConfig.SmtpPassword),
                EnableSsl = emailConfig.UseSsl
            };
            
            var message = new MailMessage
            {
                From = new MailAddress(emailConfig.FromAddress),
                Subject = $"[{alert.Severity}] {alert.Name}",
                Body = FormatAlertEmail(alert),
                IsBodyHtml = true
            };
            
            foreach (var toAddress in emailConfig.ToAddresses)
            {
                message.To.Add(toAddress);
            }
            
            await smtpClient.SendMailAsync(message, cancellationToken);
            _logger.LogInformation("Email notification sent for alert: {AlertName}", alert.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email notification for alert: {AlertName}", alert.Name);
        }
    }

    private async Task SendSlackNotificationAsync(Alert alert, CancellationToken cancellationToken)
    {
        try
        {
            var slackConfig = _config.Routing.Slack!;
            var httpClient = _httpClientFactory.CreateClient();
            
            var payload = new
            {
                channel = slackConfig.Channel,
                username = slackConfig.Username,
                icon_emoji = slackConfig.IconEmoji,
                text = $"*[{alert.Severity}] {alert.Name}*",
                attachments = new[]
                {
                    new
                    {
                        color = GetSeverityColor(alert.Severity),
                        fields = new[]
                        {
                            new { title = "Description", value = alert.Description, @short = false },
                            new { title = "Source", value = alert.Source, @short = true },
                            new { title = "Time", value = alert.StartsAt.ToString("yyyy-MM-dd HH:mm:ss UTC"), @short = true }
                        }
                    }
                }
            };
            
            var response = await httpClient.PostAsJsonAsync(slackConfig.WebhookUrl, payload, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            _logger.LogInformation("Slack notification sent for alert: {AlertName}", alert.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Slack notification for alert: {AlertName}", alert.Name);
        }
    }

    private async Task SendWebhookNotificationAsync(
        Alert alert, 
        WebhookNotificationConfig webhook, 
        CancellationToken cancellationToken)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            var request = new HttpRequestMessage(
                new HttpMethod(webhook.Method), 
                webhook.Url);
            
            // Add custom headers
            foreach (var header in webhook.Headers)
            {
                request.Headers.Add(header.Key, header.Value);
            }
            
            // Add alert payload
            var payload = JsonSerializer.Serialize(new
            {
                alert.Id,
                alert.Name,
                alert.Description,
                alert.Severity,
                alert.Source,
                alert.Labels,
                alert.Annotations,
                alert.StartsAt,
                alert.Status
            });
            
            request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
            
            var response = await httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            _logger.LogInformation(
                "Webhook notification sent for alert: {AlertName} to {WebhookName}", 
                alert.Name, 
                webhook.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex, 
                "Failed to send webhook notification for alert: {AlertName} to {WebhookName}", 
                alert.Name, 
                webhook.Name);
        }
    }

    private string FormatAlertEmail(Alert alert)
    {
        // HTML encode to prevent XSS
        var encodedName = WebUtility.HtmlEncode(alert.Name);
        var encodedDescription = WebUtility.HtmlEncode(alert.Description);
        var encodedSource = WebUtility.HtmlEncode(alert.Source);
        
        return $@"
<html>
<body>
    <h2 style='color: {GetSeverityColor(alert.Severity)};'>[{alert.Severity}] {encodedName}</h2>
    <p><strong>Description:</strong> {encodedDescription}</p>
    <p><strong>Source:</strong> {encodedSource}</p>
    <p><strong>Started At:</strong> {alert.StartsAt:yyyy-MM-dd HH:mm:ss UTC}</p>
    <p><strong>Status:</strong> {alert.Status}</p>
    <hr/>
    <p><em>This is an automated alert from DocN Monitoring System</em></p>
</body>
</html>";
    }

    private string GetSeverityColor(AlertSeverity severity)
    {
        return severity switch
        {
            AlertSeverity.Critical => "#dc3545",
            AlertSeverity.Warning => "#ffc107",
            AlertSeverity.Info => "#17a2b8",
            _ => "#6c757d"
        };
    }

    private TimeSpan CalculateAverageTimeToResolve(List<Alert> alerts)
    {
        var resolvedAlerts = alerts
            .Where(a => a.Status == AlertStatus.Resolved && a.ResolvedAt.HasValue)
            .ToList();
        
        if (resolvedAlerts.Count == 0)
            return TimeSpan.Zero;
        
        var totalTicks = resolvedAlerts
            .Sum(a => (a.ResolvedAt!.Value - a.StartsAt).Ticks);
        
        return TimeSpan.FromTicks(totalTicks / resolvedAlerts.Count);
    }
}
