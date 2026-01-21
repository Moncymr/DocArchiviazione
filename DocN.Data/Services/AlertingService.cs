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
/// Servizio per gestione avvisi e notifiche del sistema RAG
/// </summary>
/// <remarks>
/// <para><strong>Scopo:</strong> Fornire sistema di alerting centralizzato per monitoraggio qualità RAG e anomalie sistema</para>
/// 
/// <para><strong>Funzionalità chiave:</strong></para>
/// <list type="bullet">
/// <item><description>Gestione avvisi in memoria con ConcurrentDictionary (thread-safe)</description></item>
/// <item><description>Integrazione con Prometheus AlertManager (opzionale)</description></item>
/// <item><description>Notifiche multi-canale (email, webhook, Slack)</description></item>
/// <item><description>Ciclo vita avvisi: Firing → Acknowledged → Resolved</description></item>
/// <item><description>Severità configurabile (Critical, Warning, Info)</description></item>
/// </list>
/// 
/// <para><strong>Use cases:</strong></para>
/// <list type="bullet">
/// <item><description>Qualità RAG bassa (hallucinations, low confidence)</description></item>
/// <item><description>Performance degradation (latenza embedding, timeout AI)</description></item>
/// <item><description>Errori sistema (database down, API failures)</description></item>
/// <item><description>Anomalie utilizzo (quota exceeded, rate limits)</description></item>
/// </list>
/// 
/// <para><strong>Integrazione Prometheus:</strong> Quando abilitato, invia avvisi a AlertManager per 
/// aggregazione con metriche Prometheus e routing avanzato (PagerDuty, OpsGenie, etc.)</para>
/// </remarks>
public class AlertingService : IAlertingService
{
    private readonly ILogger<AlertingService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AlertManagerConfiguration _config;
    private readonly ConcurrentDictionary<string, Alert> _activeAlerts = new();

    /// <summary>
    /// Inizializza una nuova istanza del servizio alerting
    /// </summary>
    /// <param name="logger">Logger per diagnostica</param>
    /// <param name="httpClientFactory">Factory per creazione HTTP client (notifiche webhook)</param>
    /// <param name="config">Configurazione AlertManager e canali notifica</param>
    public AlertingService(
        ILogger<AlertingService> logger,
        IHttpClientFactory httpClientFactory,
        IOptions<AlertManagerConfiguration> config)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _config = config.Value;
    }

    /// <summary>
    /// Invia un avviso al sistema di alerting
    /// </summary>
    /// <param name="alert">Avviso da inviare con dettagli (nome, severità, descrizione, labels)</param>
    /// <param name="cancellationToken">Token per cancellazione operazione</param>
    /// <returns>Task completato quando avviso è stato inviato a tutti i canali configurati</returns>
    /// <remarks>
    /// <para><strong>Processo invio:</strong></para>
    /// <list type="number">
    /// <item><description>Memorizza avviso in dizionario attivi (per query successive)</description></item>
    /// <item><description>Log avviso con severità appropriata</description></item>
    /// <item><description>Invia a Prometheus AlertManager se configurato</description></item>
    /// <item><description>Invia notifiche a canali configurati (email, webhook, Slack)</description></item>
    /// </list>
    /// 
    /// <para><strong>Gestione errori:</strong> Errori invio notifiche vengono loggati ma non bloccano il flusso.
    /// L'avviso viene comunque memorizzato per query successive.</para>
    /// </remarks>
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

    /// <summary>
    /// Ottiene tutti gli avvisi attivi (Firing o Acknowledged)
    /// </summary>
    /// <param name="cancellationToken">Token per cancellazione operazione</param>
    /// <returns>Collezione avvisi attivi ordinati per data inizio (più recenti prima)</returns>
    /// <remarks>
    /// <para><strong>Stati inclusi:</strong></para>
    /// <list type="bullet">
    /// <item><description>Firing: Avviso attivo non ancora gestito</description></item>
    /// <item><description>Acknowledged: Avviso preso in carico ma non ancora risolto</description></item>
    /// </list>
    /// 
    /// <para><strong>Performance:</strong> Query in-memory sul ConcurrentDictionary, molto veloce (< 1ms)</para>
    /// </remarks>
    public Task<IEnumerable<Alert>> GetActiveAlertsAsync(CancellationToken cancellationToken = default)
    {
        var activeAlerts = _activeAlerts.Values
            .Where(a => a.Status == AlertStatus.Firing || a.Status == AlertStatus.Acknowledged)
            .OrderByDescending(a => a.StartsAt);
        
        return Task.FromResult(activeAlerts.AsEnumerable());
    }

    /// <summary>
    /// Contrassegna un avviso come preso in carico
    /// </summary>
    /// <param name="alertId">ID univoco avviso da confermare</param>
    /// <param name="acknowledgedBy">Nome utente/sistema che prende in carico l'avviso</param>
    /// <param name="cancellationToken">Token per cancellazione operazione</param>
    /// <returns>Task completato quando avviso è stato aggiornato</returns>
    /// <remarks>
    /// <para><strong>Transizione stato:</strong> Firing → Acknowledged</para>
    /// <para><strong>Audit trail:</strong> Registra chi e quando ha preso in carico l'avviso per accountability</para>
    /// </remarks>
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

    /// <summary>
    /// Contrassegna un avviso come risolto
    /// </summary>
    /// <param name="alertId">ID univoco avviso da risolvere</param>
    /// <param name="resolvedBy">Nome utente/sistema che risolve l'avviso</param>
    /// <param name="cancellationToken">Token per cancellazione operazione</param>
    /// <returns>Task completato quando avviso è stato risolto</returns>
    /// <remarks>
    /// <para><strong>Transizione stato:</strong> Acknowledged/Firing → Resolved</para>
    /// <para><strong>Chiusura automatica:</strong> Imposta EndsAt a timestamp corrente per calcolo durata avviso</para>
    /// <para><strong>Audit trail:</strong> Registra chi e quando ha risolto l'avviso</para>
    /// </remarks>
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

    /// <summary>
    /// Ottiene statistiche aggregate sugli avvisi in un periodo temporale
    /// </summary>
    /// <param name="from">Data inizio periodo (default: 7 giorni fa)</param>
    /// <param name="to">Data fine periodo (default: ora corrente)</param>
    /// <param name="cancellationToken">Token per cancellazione operazione</param>
    /// <returns>Statistiche aggregate (totali, per stato, per severità, per sorgente, tempo medio risoluzione)</returns>
    /// <remarks>
    /// <para><strong>Metriche calcolate:</strong></para>
    /// <list type="bullet">
    /// <item><description>Totale avvisi nel periodo</description></item>
    /// <item><description>Conteggi per stato (Active, Acknowledged, Resolved)</description></item>
    /// <item><description>Conteggi per severità (Critical, Warning, Info)</description></item>
    /// <item><description>Conteggi per sorgente (RAGQuality, Performance, System)</description></item>
    /// <item><description>Tempo medio risoluzione (MTTR - Mean Time To Resolve)</description></item>
    /// </list>
    /// 
    /// <para><strong>Performance:</strong> Operazione in-memory su ConcurrentDictionary, molto veloce</para>
    /// </remarks>
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

    /// <summary>
    /// Invia avviso a Prometheus AlertManager
    /// </summary>
    /// <param name="alert">Avviso da inviare</param>
    /// <param name="cancellationToken">Token per cancellazione</param>
    /// <returns>Task completato quando avviso è stato inviato (o fallito con log errore)</returns>
    /// <remarks>
    /// <para><strong>Formato Prometheus:</strong> Converte Alert interno in formato AlertManager v2 API</para>
    /// <para><strong>Endpoint:</strong> POST /api/v2/alerts con payload JSON array</para>
    /// <para><strong>Gestione errori:</strong> Errori HTTP vengono loggati ma non propagati (fire-and-forget)</para>
    /// </remarks>
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

    /// <summary>
    /// Invia notifiche avviso a tutti i canali configurati (email, Slack, webhook)
    /// </summary>
    /// <param name="alert">Avviso da notificare</param>
    /// <param name="cancellationToken">Token per cancellazione</param>
    /// <returns>Task completato quando tutte le notifiche sono state inviate</returns>
    /// <remarks>
    /// <para><strong>Esecuzione parallela:</strong> Usa Task.WhenAll per inviare a tutti i canali simultaneamente</para>
    /// <para><strong>Canali supportati:</strong></para>
    /// <list type="bullet">
    /// <item><description>Email (SMTP)</description></item>
    /// <item><description>Slack (Incoming Webhooks)</description></item>
    /// <item><description>Webhook generici (HTTP POST JSON)</description></item>
    /// </list>
    /// </remarks>
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

    /// <summary>
    /// Invia notifica email per avviso
    /// </summary>
    /// <param name="alert">Avviso da notificare</param>
    /// <param name="cancellationToken">Token per cancellazione</param>
    /// <returns>Task completato quando email è stata inviata</returns>
    /// <remarks>
    /// <para><strong>Configurazione SMTP:</strong> Usa impostazioni da AlertManagerConfiguration.Routing.Email</para>
    /// <para><strong>Formato email:</strong> HTML formattato con severità, descrizione, timestamp, labels</para>
    /// </remarks>
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

    /// <summary>
    /// Invia notifica a canale Slack tramite Incoming Webhook
    /// </summary>
    /// <param name="alert">Avviso da notificare</param>
    /// <param name="cancellationToken">Token per cancellazione</param>
    /// <returns>Task completato quando messaggio Slack è stato inviato</returns>
    /// <remarks>
    /// <para><strong>Formato messaggio:</strong> Usa Slack attachments API con colori severità</para>
    /// <para><strong>Colori:</strong> Critical=danger (rosso), Warning=warning (giallo), Info=good (verde)</para>
    /// </remarks>
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
