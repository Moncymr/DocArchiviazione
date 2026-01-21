using Microsoft.Extensions.Logging;

namespace DocN.Data.Services.Connectors;

/// <summary>
/// Configuration for FTP connector
/// </summary>
public class FtpConfiguration
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 21;
    public string Username { get; set; } = string.Empty;
    public string RemotePath { get; set; } = "/";
    public bool UseSSL { get; set; } = false;
    public bool Recursive { get; set; } = true;
}

/// <summary>
/// Handler for FTP connectors
/// NOTE: This is a placeholder implementation. Full FTP integration requires:
/// - FluentFTP NuGet package recommended (or System.Net.FtpWebRequest)
/// - Proper SSL/TLS handling
/// - Passive mode support
/// </summary>
public class FtpConnectorHandler : BaseConnectorHandler
{
    public FtpConnectorHandler(ILogger logger) : base(logger)
    {
    }
    
    public override async Task<(bool success, string message)> TestConnectionAsync(string configuration, string? encryptedCredentials)
    {
        _logger.LogWarning("FTP connector not fully implemented - returning placeholder response");
        return await Task.FromResult((false, "FTP connector not yet implemented. Please use LocalFolder connector or implement FTP integration."));
    }
    
    public override async Task<List<ConnectorFileInfo>> ListFilesAsync(string configuration, string? encryptedCredentials, string? path = null)
    {
        _logger.LogWarning("FTP file listing not implemented");
        return await Task.FromResult(new List<ConnectorFileInfo>());
    }
    
    public override async Task<Stream> DownloadFileAsync(string configuration, string? encryptedCredentials, string filePath)
    {
        _logger.LogWarning("FTP file download not implemented");
        throw new NotImplementedException("FTP file download not yet implemented");
    }
}
