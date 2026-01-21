using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Azure.Identity;

namespace DocN.Data.Services.Connectors;

/// <summary>
/// Configuration for OneDrive connector
/// </summary>
public class OneDriveConfiguration
{
    public string FolderPath { get; set; } = "/";
    public bool Recursive { get; set; } = true;
}

/// <summary>
/// Credentials for OneDrive connector (OAuth)
/// </summary>
public class OneDriveCredentials
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
}

/// <summary>
/// Handler for OneDrive connectors using Microsoft Graph API
/// </summary>
public class OneDriveConnectorHandler : BaseConnectorHandler
{
    public OneDriveConnectorHandler(ILogger logger) : base(logger)
    {
    }
    
    public override async Task<(bool success, string message)> TestConnectionAsync(string configuration, string? encryptedCredentials)
    {
        try
        {
            var config = ParseConfiguration<OneDriveConfiguration>(configuration);
            if (config == null)
            {
                return (false, "Invalid configuration: Unable to parse JSON.");
            }
            
            if (string.IsNullOrEmpty(encryptedCredentials))
            {
                return (false, "Credentials are required for OneDrive connector. Please provide ClientId, ClientSecret, and TenantId.");
            }
            
            var credentials = ParseConfiguration<OneDriveCredentials>(encryptedCredentials);
            if (credentials == null || string.IsNullOrEmpty(credentials.ClientId) || 
                string.IsNullOrEmpty(credentials.ClientSecret) || string.IsNullOrEmpty(credentials.TenantId))
            {
                return (false, "Invalid credentials: ClientId, ClientSecret, and TenantId are required.");
            }
            
            var graphClient = CreateGraphClient(credentials);
            
            // Test connection by getting user's drive
            var drive = await graphClient.Me.Drive.GetAsync();
            if (drive == null)
            {
                return (false, "Unable to access OneDrive. Please check credentials.");
            }
            
            // Test folder access if specific path is provided
            if (!string.IsNullOrEmpty(config.FolderPath) && config.FolderPath != "/")
            {
                var folderPath = config.FolderPath.TrimStart('/');
                var folder = await graphClient.Drives[drive.Id].Items["root"].ItemWithPath(folderPath).GetAsync();
                if (folder == null)
                {
                    return (false, $"Folder not found: {config.FolderPath}");
                }
            }
            
            return (true, "Connection successful - OneDrive accessible");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing OneDrive connection");
            return (false, $"Connection test failed: {ex.Message}");
        }
    }
    
    public override async Task<List<ConnectorFileInfo>> ListFilesAsync(string configuration, string? encryptedCredentials, string? path = null)
    {
        var fileInfos = new List<ConnectorFileInfo>();
        
        try
        {
            var config = ParseConfiguration<OneDriveConfiguration>(configuration);
            if (config == null || string.IsNullOrEmpty(encryptedCredentials))
            {
                throw new InvalidOperationException("Invalid configuration or credentials");
            }
            
            var credentials = ParseConfiguration<OneDriveCredentials>(encryptedCredentials);
            if (credentials == null)
            {
                throw new InvalidOperationException("Invalid credentials");
            }
            
            var graphClient = CreateGraphClient(credentials);
            
            // Determine which folder to list
            var folderPath = path ?? config.FolderPath ?? "/";
            
            // Get drive ID first
            var drive = await graphClient.Me.Drive.GetAsync();
            if (drive?.Id == null)
            {
                throw new InvalidOperationException("Unable to access drive");
            }
            
            await ListFilesRecursiveAsync(graphClient, drive.Id, folderPath, fileInfos, config.Recursive);
            
            _logger.LogInformation("Listed {Count} files from OneDrive folder {Path}", fileInfos.Count, folderPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing files from OneDrive");
            throw;
        }
        
        return fileInfos;
    }
    
    public override async Task<Stream> DownloadFileAsync(string configuration, string? encryptedCredentials, string filePath)
    {
        try
        {
            if (string.IsNullOrEmpty(encryptedCredentials))
            {
                throw new InvalidOperationException("Credentials are required");
            }
            
            var credentials = ParseConfiguration<OneDriveCredentials>(encryptedCredentials);
            if (credentials == null)
            {
                throw new InvalidOperationException("Invalid credentials");
            }
            
            var graphClient = CreateGraphClient(credentials);
            
            // Get drive ID first
            var drive = await graphClient.Me.Drive.GetAsync();
            if (drive?.Id == null)
            {
                throw new InvalidOperationException("Unable to access drive");
            }
            
            // Note: filePath contains the full path, not the item ID
            // Remove leading slash if present to work with ItemWithPath
            var cleanPath = filePath.TrimStart('/');
            
            // Download file content
            var stream = await graphClient.Drives[drive.Id].Items["root"].ItemWithPath(cleanPath).Content.GetAsync();
            if (stream == null)
            {
                throw new FileNotFoundException($"File not found: {filePath}");
            }
            
            // Copy to memory stream to avoid disposal issues
            var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;
            
            return memoryStream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading file from OneDrive: {FilePath}", filePath);
            throw;
        }
    }
    
    private GraphServiceClient CreateGraphClient(OneDriveCredentials credentials)
    {
        var clientSecretCredential = new ClientSecretCredential(
            credentials.TenantId,
            credentials.ClientId,
            credentials.ClientSecret
        );
        
        return new GraphServiceClient(clientSecretCredential);
    }
    
    private async Task ListFilesRecursiveAsync(GraphServiceClient graphClient, string driveId, string folderPath, List<ConnectorFileInfo> fileInfos, bool recursive)
    {
        try
        {
            DriveItemCollectionResponse? items;
            
            if (string.IsNullOrEmpty(folderPath) || folderPath == "/")
            {
                items = await graphClient.Drives[driveId].Items["root"].Children.GetAsync();
            }
            else
            {
                var path = folderPath.TrimStart('/');
                items = await graphClient.Drives[driveId].Items["root"].ItemWithPath(path).Children.GetAsync();
            }
            
            if (items?.Value == null) return;
            
            foreach (var item in items.Value)
            {
                if (item.Folder != null)
                {
                    // It's a folder
                    if (recursive)
                    {
                        var subPath = string.IsNullOrEmpty(folderPath) || folderPath == "/" 
                            ? $"/{item.Name}" 
                            : $"{folderPath.TrimEnd('/')}/{item.Name}";
                        await ListFilesRecursiveAsync(graphClient, driveId, subPath, fileInfos, recursive);
                    }
                }
                else if (item.File != null)
                {
                    // It's a file
                    var fullPath = string.IsNullOrEmpty(folderPath) || folderPath == "/" 
                        ? $"/{item.Name}" 
                        : $"{folderPath.TrimEnd('/')}/{item.Name}";
                    
                    fileInfos.Add(new ConnectorFileInfo
                    {
                        Name = item.Name ?? "Unknown",
                        Path = fullPath,
                        Size = item.Size ?? 0,
                        ModifiedDate = item.LastModifiedDateTime?.UtcDateTime,
                        IsFolder = false,
                        ContentType = item.File.MimeType ?? "application/octet-stream"
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error listing files from folder {FolderPath}", folderPath);
        }
    }
}
