namespace DocN.Core.Interfaces;

/// <summary>
/// Service for managing alerts and notifications
/// </summary>
public interface IAlertingService
{
    /// <summary>
    /// Send an alert through configured channels
    /// </summary>
    Task SendAlertAsync(Alert alert, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all active alerts
    /// </summary>
    Task<IEnumerable<Alert>> GetActiveAlertsAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Acknowledge an alert
    /// </summary>
    Task AcknowledgeAlertAsync(string alertId, string acknowledgedBy, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Resolve an alert
    /// </summary>
    Task ResolveAlertAsync(string alertId, string resolvedBy, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get alert statistics
    /// </summary>
    Task<AlertStatistics> GetAlertStatisticsAsync(DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents an alert
/// </summary>
public class Alert
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public AlertSeverity Severity { get; set; }
    public string Source { get; set; } = string.Empty;
    public Dictionary<string, object> Labels { get; set; } = new();
    public Dictionary<string, object> Annotations { get; set; } = new();
    public DateTime StartsAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndsAt { get; set; }
    public AlertStatus Status { get; set; } = AlertStatus.Firing;
    public string? AcknowledgedBy { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public string? ResolvedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

/// <summary>
/// Alert severity levels
/// </summary>
public enum AlertSeverity
{
    Info,
    Warning,
    Critical
}

/// <summary>
/// Alert status
/// </summary>
public enum AlertStatus
{
    Firing,
    Acknowledged,
    Resolved
}

/// <summary>
/// Alert statistics
/// </summary>
public class AlertStatistics
{
    public int TotalAlerts { get; set; }
    public int ActiveAlerts { get; set; }
    public int AcknowledgedAlerts { get; set; }
    public int ResolvedAlerts { get; set; }
    public Dictionary<AlertSeverity, int> AlertsBySeverity { get; set; } = new();
    public Dictionary<string, int> AlertsBySource { get; set; } = new();
    public TimeSpan AverageTimeToResolve { get; set; }
}
