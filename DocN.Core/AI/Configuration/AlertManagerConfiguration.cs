namespace DocN.Core.AI.Configuration;

/// <summary>
/// AlertManager configuration for Prometheus
/// </summary>
public class AlertManagerConfiguration
{
    /// <summary>
    /// AlertManager endpoint URL
    /// </summary>
    public string Endpoint { get; set; } = "http://localhost:9093";
    
    /// <summary>
    /// Whether AlertManager integration is enabled
    /// </summary>
    public bool Enabled { get; set; } = false;
    
    /// <summary>
    /// Alert routing configuration
    /// </summary>
    public AlertRoutingConfiguration Routing { get; set; } = new();
    
    /// <summary>
    /// Alert rules configuration
    /// </summary>
    public List<AlertRuleConfiguration> Rules { get; set; } = new();
}

/// <summary>
/// Alert routing configuration
/// </summary>
public class AlertRoutingConfiguration
{
    /// <summary>
    /// Email notification configuration
    /// </summary>
    public EmailNotificationConfig? Email { get; set; }
    
    /// <summary>
    /// Slack notification configuration
    /// </summary>
    public SlackNotificationConfig? Slack { get; set; }
    
    /// <summary>
    /// Webhook notification configuration
    /// </summary>
    public List<WebhookNotificationConfig> Webhooks { get; set; } = new();
    
    /// <summary>
    /// Default receiver for unmatched alerts
    /// </summary>
    public string DefaultReceiver { get; set; } = "default";
}

/// <summary>
/// Email notification configuration
/// </summary>
public class EmailNotificationConfig
{
    public bool Enabled { get; set; } = false;
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public string SmtpUsername { get; set; } = string.Empty;
    public string SmtpPassword { get; set; } = string.Empty;
    public bool UseSsl { get; set; } = true;
    public string FromAddress { get; set; } = string.Empty;
    public List<string> ToAddresses { get; set; } = new();
}

/// <summary>
/// Slack notification configuration
/// </summary>
public class SlackNotificationConfig
{
    public bool Enabled { get; set; } = false;
    public string WebhookUrl { get; set; } = string.Empty;
    public string Channel { get; set; } = "#alerts";
    public string Username { get; set; } = "DocN Alert Bot";
    public string IconEmoji { get; set; } = ":alert:";
}

/// <summary>
/// Webhook notification configuration
/// </summary>
public class WebhookNotificationConfig
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public Dictionary<string, string> Headers { get; set; } = new();
    public string Method { get; set; } = "POST";
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Alert rule configuration
/// </summary>
public class AlertRuleConfiguration
{
    /// <summary>
    /// Rule name
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Rule description
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Alert severity
    /// </summary>
    public string Severity { get; set; } = "warning";
    
    /// <summary>
    /// Metric to monitor
    /// </summary>
    public string Metric { get; set; } = string.Empty;
    
    /// <summary>
    /// Threshold value
    /// </summary>
    public double Threshold { get; set; }
    
    /// <summary>
    /// Comparison operator (>, <, >=, <=, ==, !=)
    /// </summary>
    public string Operator { get; set; } = ">";
    
    /// <summary>
    /// Duration for which condition must be true before alerting
    /// </summary>
    public TimeSpan Duration { get; set; } = TimeSpan.FromMinutes(5);
    
    /// <summary>
    /// Whether this rule is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;
    
    /// <summary>
    /// Labels to attach to this alert
    /// </summary>
    public Dictionary<string, string> Labels { get; set; } = new();
    
    /// <summary>
    /// Annotations for this alert
    /// </summary>
    public Dictionary<string, string> Annotations { get; set; } = new();
}

/// <summary>
/// Escalation policy configuration
/// </summary>
public class EscalationPolicyConfiguration
{
    /// <summary>
    /// Policy name
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Alert severity this policy applies to
    /// </summary>
    public string Severity { get; set; } = string.Empty;
    
    /// <summary>
    /// Escalation levels
    /// </summary>
    public List<EscalationLevel> Levels { get; set; } = new();
}

/// <summary>
/// Escalation level
/// </summary>
public class EscalationLevel
{
    /// <summary>
    /// Time to wait before escalating to this level
    /// </summary>
    public TimeSpan WaitTime { get; set; }
    
    /// <summary>
    /// Notification channels for this level
    /// </summary>
    public List<string> NotificationChannels { get; set; } = new();
    
    /// <summary>
    /// Recipients for this level
    /// </summary>
    public List<string> Recipients { get; set; } = new();
}
