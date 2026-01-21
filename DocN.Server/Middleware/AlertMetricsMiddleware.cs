using DocN.Core.Interfaces;
using System.Diagnostics;

namespace DocN.Server.Middleware;

/// <summary>
/// Middleware for collecting metrics and triggering alerts based on thresholds
/// </summary>
public class AlertMetricsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AlertMetricsMiddleware> _logger;
    
    // Track metrics
    private static long _totalRequests = 0;
    private static long _failedRequests = 0;
    private static readonly Dictionary<string, List<double>> _latencyByEndpoint = new();
    private static readonly object _metricsLock = new();

    public AlertMetricsMiddleware(
        RequestDelegate next,
        ILogger<AlertMetricsMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IAlertingService alertingService)
    {
        var stopwatch = Stopwatch.StartNew();
        var path = context.Request.Path.Value ?? "/";
        
        Interlocked.Increment(ref _totalRequests);
        
        try
        {
            await _next(context);
            
            // Check for error status codes
            if (context.Response.StatusCode >= 500)
            {
                Interlocked.Increment(ref _failedRequests);
                
                // Trigger alert for critical errors
                await TriggerErrorRateAlertIfNeeded(alertingService);
            }
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _failedRequests);
            _logger.LogError(ex, "Request failed: {Path}", path);
            
            // Trigger alert
            await alertingService.SendAlertAsync(new Alert
            {
                Name = "UnhandledException",
                Description = $"Unhandled exception in {path}: {ex.Message}",
                Severity = AlertSeverity.Critical,
                Source = "AlertMetricsMiddleware",
                Labels = new Dictionary<string, object>
                {
                    ["path"] = path,
                    ["exception_type"] = ex.GetType().Name
                }
            });
            
            throw;
        }
        finally
        {
            stopwatch.Stop();
            
            // Record latency
            lock (_metricsLock)
            {
                if (!_latencyByEndpoint.ContainsKey(path))
                {
                    _latencyByEndpoint[path] = new List<double>();
                }
                
                _latencyByEndpoint[path].Add(stopwatch.Elapsed.TotalMilliseconds);
                
                // Keep only last 100 measurements
                if (_latencyByEndpoint[path].Count > 100)
                {
                    _latencyByEndpoint[path].RemoveAt(0);
                }
            }
            
            // Check latency threshold
            await TriggerLatencyAlertIfNeeded(path, stopwatch.Elapsed.TotalMilliseconds, alertingService);
        }
    }

    private async Task TriggerErrorRateAlertIfNeeded(IAlertingService alertingService)
    {
        var total = Interlocked.Read(ref _totalRequests);
        var failed = Interlocked.Read(ref _failedRequests);
        
        if (total < 100) // Wait for enough samples
            return;
        
        var errorRate = (double)failed / total;
        
        // Alert if error rate > 5%
        if (errorRate > 0.05)
        {
            await alertingService.SendAlertAsync(new Alert
            {
                Name = "HighErrorRate",
                Description = $"Error rate is {errorRate:P2} ({failed}/{total} requests failed)",
                Severity = errorRate > 0.10 ? AlertSeverity.Critical : AlertSeverity.Warning,
                Source = "AlertMetricsMiddleware",
                Labels = new Dictionary<string, object>
                {
                    ["error_rate"] = errorRate,
                    ["failed_requests"] = failed,
                    ["total_requests"] = total
                }
            });
        }
    }

    private async Task TriggerLatencyAlertIfNeeded(
        string path, 
        double latencyMs, 
        IAlertingService alertingService)
    {
        // Alert if single request > 5 seconds
        if (latencyMs > 5000)
        {
            await alertingService.SendAlertAsync(new Alert
            {
                Name = "HighLatency",
                Description = $"Request to {path} took {latencyMs:F0}ms",
                Severity = latencyMs > 10000 ? AlertSeverity.Critical : AlertSeverity.Warning,
                Source = "AlertMetricsMiddleware",
                Labels = new Dictionary<string, object>
                {
                    ["path"] = path,
                    ["latency_ms"] = latencyMs
                }
            });
        }
    }

    /// <summary>
    /// Get current metrics (for monitoring endpoint)
    /// </summary>
    public static object GetMetrics()
    {
        lock (_metricsLock)
        {
            var total = Interlocked.Read(ref _totalRequests);
            var failed = Interlocked.Read(ref _failedRequests);
            
            return new
            {
                total_requests = total,
                failed_requests = failed,
                error_rate = total > 0 ? (double)failed / total : 0.0,
                latency_by_endpoint = _latencyByEndpoint.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new
                    {
                        avg = kvp.Value.Any() ? kvp.Value.Average() : 0.0,
                        p50 = kvp.Value.Any() ? Percentile(kvp.Value, 0.5) : 0.0,
                        p95 = kvp.Value.Any() ? Percentile(kvp.Value, 0.95) : 0.0,
                        p99 = kvp.Value.Any() ? Percentile(kvp.Value, 0.99) : 0.0
                    })
            };
        }
    }

    private static double Percentile(List<double> sequence, double percentile)
    {
        var sorted = sequence.OrderBy(x => x).ToArray();
        var n = sorted.Length;
        var position = percentile * (n - 1);
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        
        if (lower == upper)
            return sorted[lower];
        
        return sorted[lower] + (position - lower) * (sorted[upper] - sorted[lower]);
    }
}
