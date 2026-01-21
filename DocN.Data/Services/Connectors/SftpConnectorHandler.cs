using Microsoft.Extensions.Logging;

namespace DocN.Data.Services.Connectors;

/// <summary>
/// Configuration for SFTP connector
/// </summary>
public class SftpConfiguration
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 22;
    public string Username { get; set; } = string.Empty;
    public string RemotePath { get; set; } = "/";
    public bool Recursive { get; set; } = true;
}

/// <summary>
/// Handler for SFTP connectors
/// NOTE: This is a placeholder implementation. Full SFTP integration requires:
/// - SSH.NET (Renci.SshNet) NuGet package
/// - Private key or password authentication
/// - Proper SSH key management
/// </summary>
public class SftpConnectorHandler : BaseConnectorHandler
{
    public SftpConnectorHandler(ILogger logger) : base(logger)
    {
    }
    
    public override async Task<(bool success, string message)> TestConnectionAsync(string configuration, string? encryptedCredentials)
    {
        _logger.LogWarning("SFTP connector not fully implemented - returning placeholder response");
        return await Task.FromResult((false, "SFTP connector not yet implemented. Please use LocalFolder connector or implement SFTP integration."));
    }
    
    public override async Task<List<ConnectorFileInfo>> ListFilesAsync(string configuration, string? encryptedCredentials, string? path = null)
    {
        _logger.LogWarning("SFTP file listing not implemented");
        return await Task.FromResult(new List<ConnectorFileInfo>());
    }
    
    public override async Task<Stream> DownloadFileAsync(string configuration, string? encryptedCredentials, string filePath)
    {
        _logger.LogWarning("SFTP file download not implemented");
        throw new NotImplementedException("SFTP file download not yet implemented");
    }
}
