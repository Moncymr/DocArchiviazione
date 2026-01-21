using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DocN.Data.Constants;
using Microsoft.Extensions.Logging;

namespace DocN.Data.Services.Connectors;

/// <summary>
/// Base class for connector implementations
/// </summary>
public abstract class BaseConnectorHandler
{
    protected readonly ILogger _logger;
    
    protected BaseConnectorHandler(ILogger logger)
    {
        _logger = logger;
    }
    
    /// <summary>
    /// Tests the connection to the external repository
    /// </summary>
    public abstract Task<(bool success, string message)> TestConnectionAsync(string configuration, string? encryptedCredentials);
    
    /// <summary>
    /// Lists files from the connector
    /// </summary>
    public abstract Task<List<ConnectorFileInfo>> ListFilesAsync(string configuration, string? encryptedCredentials, string? path = null);
    
    /// <summary>
    /// Downloads a file from the connector
    /// </summary>
    public abstract Task<Stream> DownloadFileAsync(string configuration, string? encryptedCredentials, string filePath);
    
    /// <summary>
    /// Computes SHA-256 hash of a stream
    /// </summary>
    protected async Task<string> ComputeFileHashAsync(Stream stream)
    {
        var position = stream.Position;
        stream.Position = 0;
        
        using var sha256 = SHA256.Create();
        var hashBytes = await sha256.ComputeHashAsync(stream);
        var hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        
        stream.Position = position;
        return hash;
    }
    
    /// <summary>
    /// Parses JSON configuration with case-insensitive property names
    /// </summary>
    protected T? ParseConfiguration<T>(string configuration) where T : class
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            return JsonSerializer.Deserialize<T>(configuration, options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse configuration: {Configuration}", configuration);
            return null;
        }
    }
}

/// <summary>
/// Factory for creating connector handlers
/// </summary>
public class ConnectorHandlerFactory
{
    private readonly ILogger _logger;
    
    public ConnectorHandlerFactory(ILogger logger)
    {
        _logger = logger;
    }
    
    public BaseConnectorHandler CreateHandler(string connectorType)
    {
        return connectorType switch
        {
            ConnectorTypes.LocalFolder => new LocalFolderConnectorHandler(_logger),
            ConnectorTypes.SharePoint => new SharePointConnectorHandler(_logger),
            ConnectorTypes.OneDrive => new OneDriveConnectorHandler(_logger),
            ConnectorTypes.GoogleDrive => new GoogleDriveConnectorHandler(_logger),
            ConnectorTypes.FTP => new FtpConnectorHandler(_logger),
            ConnectorTypes.SFTP => new SftpConnectorHandler(_logger),
            _ => throw new NotSupportedException($"Connector type '{connectorType}' is not supported")
        };
    }
}

/// <summary>
/// Represents file information from a connector
/// </summary>
public class ConnectorFileInfo
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public bool IsFolder { get; set; }
    public string? ContentType { get; set; }
}
