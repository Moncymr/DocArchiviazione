using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DocN.Server.Services.HealthChecks;

/// <summary>
/// Health check for file storage availability
/// </summary>
public class FileStorageHealthCheck : IHealthCheck
{
    private readonly ILogger<FileStorageHealthCheck> _logger;
    private readonly IConfiguration _configuration;

    public FileStorageHealthCheck(ILogger<FileStorageHealthCheck> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Get upload path from configuration
            var uploadPath = _configuration["FileStorage:UploadPath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
            
            // Check if directory exists
            if (!Directory.Exists(uploadPath))
            {
                try
                {
                    Directory.CreateDirectory(uploadPath);
                    _logger.LogInformation("Created upload directory: {UploadPath}", uploadPath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create upload directory: {UploadPath}", uploadPath);
                    return HealthCheckResult.Unhealthy(
                        "File storage directory cannot be created",
                        exception: ex,
                        data: new Dictionary<string, object> { { "path", uploadPath } });
                }
            }

            // Try to write a test file
            var testFilePath = Path.Combine(uploadPath, ".healthcheck");
            try
            {
                await File.WriteAllTextAsync(testFilePath, DateTime.UtcNow.ToString("O"), cancellationToken);
                
                // Try to read it back
                var content = await File.ReadAllTextAsync(testFilePath, cancellationToken);
                
                // Clean up
                File.Delete(testFilePath);
                
                // Get available disk space
                var driveInfo = new DriveInfo(Path.GetPathRoot(uploadPath) ?? "/");
                var availableSpaceGB = driveInfo.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                
                var data = new Dictionary<string, object>
                {
                    { "path", uploadPath },
                    { "availableSpaceGB", Math.Round(availableSpaceGB, 2) },
                    { "totalSpaceGB", Math.Round(driveInfo.TotalSize / (1024.0 * 1024.0 * 1024.0), 2) }
                };

                // Warn if less than 5GB available
                if (availableSpaceGB < 5)
                {
                    return HealthCheckResult.Degraded(
                        $"File storage available but disk space low: {availableSpaceGB:F2} GB remaining",
                        data: data);
                }

                return HealthCheckResult.Healthy(
                    "File storage is available and writable",
                    data: data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "File storage write test failed");
                return HealthCheckResult.Unhealthy(
                    "File storage is not writable",
                    exception: ex,
                    data: new Dictionary<string, object> { { "path", uploadPath } });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "File storage health check failed");
            return HealthCheckResult.Unhealthy(
                "File storage health check failed",
                exception: ex);
        }
    }
}
