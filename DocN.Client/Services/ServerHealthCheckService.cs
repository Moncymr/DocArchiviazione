using System.Diagnostics;

namespace DocN.Client.Services;

/// <summary>
/// Service that checks if the Server API is available and ready
/// Used during Client startup to ensure Server is accessible before proceeding
/// </summary>
public interface IServerHealthCheckService
{
    /// <summary>
    /// Checks if the Server is reachable and responds to health checks
    /// </summary>
    Task<bool> IsServerHealthyAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Waits for the Server to become available with retry logic
    /// </summary>
    /// <param name="maxRetries">Maximum number of retry attempts</param>
    /// <param name="delayMs">Delay between retries in milliseconds</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if server becomes available, false if max retries exceeded</returns>
    Task<bool> WaitForServerAsync(int maxRetries = 30, int delayMs = 1000, CancellationToken cancellationToken = default);
}

public class ServerHealthCheckService : IServerHealthCheckService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ServerHealthCheckService> _logger;

    public ServerHealthCheckService(
        IHttpClientFactory httpClientFactory,
        ILogger<ServerHealthCheckService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Checks if the Server is reachable and responds to health checks
    /// </summary>
    public async Task<bool> IsServerHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("BackendAPI");
            
            // Set a short timeout for health check (5 seconds)
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));
            
            // Try to reach the server's health endpoint or root endpoint
            var response = await client.GetAsync("/health", cts.Token);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Server health check passed");
                return true;
            }
            
            _logger.LogWarning("Server returned non-success status: {StatusCode}", response.StatusCode);
            return false;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Server health check timed out");
            return false;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning("Server not reachable: {Message}", ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during server health check");
            return false;
        }
    }

    /// <summary>
    /// Waits for the Server to become available with exponential backoff retry logic
    /// </summary>
    public async Task<bool> WaitForServerAsync(int maxRetries = 30, int delayMs = 1000, CancellationToken cancellationToken = default)
    {
        try
        {
            var stopwatch = Stopwatch.StartNew();
            _logger.LogInformation("Waiting for Server to become available (max {MaxRetries} retries, {DelayMs}ms initial delay)...", maxRetries, delayMs);

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Server wait cancelled");
                    return false;
                }

                try
                {
                    var isHealthy = await IsServerHealthyAsync(cancellationToken);
                    if (isHealthy)
                    {
                        _logger.LogInformation("Server is available after {Attempts} attempts ({ElapsedMs}ms)", attempt, stopwatch.ElapsedMilliseconds);
                        return true;
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Server health check cancelled during attempt {Attempt}", attempt);
                    return false;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error checking server health on attempt {Attempt}", attempt);
                    // Continue to next attempt
                }

                if (attempt < maxRetries)
                {
                    try
                    {
                        // Exponential backoff with jitter: wait longer between retries, up to 5 seconds max
                        var delay = Math.Min(delayMs * Math.Pow(1.5, attempt - 1), 5000);
                        var jitter = Random.Shared.Next(0, 100); // Add randomness to avoid thundering herd
                        var totalDelay = (int)(delay + jitter);
                        
                        _logger.LogDebug("Server not available yet. Retry {Attempt}/{MaxRetries} in {DelayMs}ms...", attempt, maxRetries, totalDelay);
                        await Task.Delay(totalDelay, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogWarning("Server wait cancelled during delay after attempt {Attempt}", attempt);
                        return false;
                    }
                }
            }

            _logger.LogError("Server did not become available after {MaxRetries} attempts ({ElapsedMs}ms)", maxRetries, stopwatch.ElapsedMilliseconds);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during WaitForServerAsync");
            return false;
        }
    }
}
