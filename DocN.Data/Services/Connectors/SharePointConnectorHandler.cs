using Microsoft.Extensions.Logging;
using PnP.Framework;
using Microsoft.SharePoint.Client;

namespace DocN.Data.Services.Connectors;

/// <summary>
/// Configuration for SharePoint connector
/// </summary>
public class SharePointConfiguration
{
    public string SiteUrl { get; set; } = string.Empty;
    public string FolderPath { get; set; } = "/Shared Documents";
    public bool Recursive { get; set; } = true;
}

/// <summary>
/// Credentials for SharePoint connector
/// </summary>
public class SharePointCredentials
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
}

/// <summary>
/// Handler for SharePoint connectors using PnP Framework
/// </summary>
public class SharePointConnectorHandler : BaseConnectorHandler
{
    public SharePointConnectorHandler(ILogger logger) : base(logger)
    {
    }
    
    public override async Task<(bool success, string message)> TestConnectionAsync(string configuration, string? encryptedCredentials)
    {
        try
        {
            var config = ParseConfiguration<SharePointConfiguration>(configuration);
            if (config == null || string.IsNullOrEmpty(config.SiteUrl))
            {
                return (false, "Invalid configuration: SiteUrl is required");
            }
            
            if (string.IsNullOrEmpty(encryptedCredentials))
            {
                return (false, "Credentials are required for SharePoint connector. Please provide ClientId and ClientSecret.");
            }
            
            var credentials = ParseConfiguration<SharePointCredentials>(encryptedCredentials);
            if (credentials == null || string.IsNullOrEmpty(credentials.ClientId) || string.IsNullOrEmpty(credentials.ClientSecret))
            {
                return (false, "Invalid credentials: ClientId and ClientSecret are required.");
            }
            
            using (var context = await CreateClientContextAsync(config.SiteUrl, credentials))
            {
                // Test connection by loading web properties
                var web = context.Web;
                context.Load(web, w => w.Title);
                await context.ExecuteQueryAsync();
                
                // Test folder access
                var folder = context.Web.GetFolderByServerRelativeUrl(config.FolderPath);
                context.Load(folder, f => f.Name);
                await context.ExecuteQueryAsync();
                
                return (true, $"Connection successful - Site: {web.Title}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing SharePoint connection");
            return (false, $"Connection test failed: {ex.Message}");
        }
    }
    
    public override async Task<List<ConnectorFileInfo>> ListFilesAsync(string configuration, string? encryptedCredentials, string? path = null)
    {
        var fileInfos = new List<ConnectorFileInfo>();
        
        try
        {
            var config = ParseConfiguration<SharePointConfiguration>(configuration);
            if (config == null || string.IsNullOrEmpty(config.SiteUrl) || string.IsNullOrEmpty(encryptedCredentials))
            {
                throw new InvalidOperationException("Invalid configuration or credentials");
            }
            
            var credentials = ParseConfiguration<SharePointCredentials>(encryptedCredentials);
            if (credentials == null)
            {
                throw new InvalidOperationException("Invalid credentials");
            }
            
            using (var context = await CreateClientContextAsync(config.SiteUrl, credentials))
            {
                var folderPath = path ?? config.FolderPath;
                await ListFilesRecursiveAsync(context, folderPath, fileInfos, config.Recursive);
            }
            
            _logger.LogInformation("Listed {Count} files from SharePoint folder {Path}", fileInfos.Count, path ?? config.FolderPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing files from SharePoint");
            throw;
        }
        
        return fileInfos;
    }
    
    public override async Task<Stream> DownloadFileAsync(string configuration, string? encryptedCredentials, string filePath)
    {
        try
        {
            var config = ParseConfiguration<SharePointConfiguration>(configuration);
            if (config == null || string.IsNullOrEmpty(config.SiteUrl) || string.IsNullOrEmpty(encryptedCredentials))
            {
                throw new InvalidOperationException("Invalid configuration or credentials");
            }
            
            var credentials = ParseConfiguration<SharePointCredentials>(encryptedCredentials);
            if (credentials == null)
            {
                throw new InvalidOperationException("Invalid credentials");
            }
            
            using (var context = await CreateClientContextAsync(config.SiteUrl, credentials))
            {
                var file = context.Web.GetFileByServerRelativeUrl(filePath);
                var fileStream = file.OpenBinaryStream();
                await context.ExecuteQueryAsync();
                
                // Copy to memory stream
                var memoryStream = new MemoryStream();
                await fileStream.Value.CopyToAsync(memoryStream);
                memoryStream.Position = 0;
                
                return memoryStream;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading file from SharePoint: {FilePath}", filePath);
            throw;
        }
    }
    
    private async Task<ClientContext> CreateClientContextAsync(string siteUrl, SharePointCredentials credentials)
    {
        // Extract tenant from site URL (e.g., "https://tenant.sharepoint.com" -> "tenant.sharepoint.com")
        var uri = new Uri(siteUrl);
        var tenantUrl = $"{uri.Scheme}://{uri.Host}";
        
        var authManager = new PnP.Framework.AuthenticationManager(credentials.ClientId, credentials.ClientSecret, tenantUrl);
        return await authManager.GetContextAsync(siteUrl);
    }
    
    private async Task ListFilesRecursiveAsync(ClientContext context, string folderPath, List<ConnectorFileInfo> fileInfos, bool recursive)
    {
        try
        {
            var folder = context.Web.GetFolderByServerRelativeUrl(folderPath);
            context.Load(folder, f => f.ServerRelativeUrl);
            context.Load(folder.Files, files => files.Include(
                f => f.Name, 
                f => f.ServerRelativeUrl, 
                f => f.Length, 
                f => f.TimeLastModified
            ));
            
            if (recursive)
            {
                context.Load(folder.Folders, folders => folders.Include(f => f.Name, f => f.ServerRelativeUrl));
            }
            
            await context.ExecuteQueryAsync();
            
            // Add files
            foreach (var file in folder.Files)
            {
                fileInfos.Add(new ConnectorFileInfo
                {
                    Name = file.Name,
                    Path = file.ServerRelativeUrl,
                    Size = file.Length,
                    ModifiedDate = file.TimeLastModified,
                    IsFolder = false,
                    ContentType = GetContentTypeFromFileName(file.Name)
                });
            }
            
            // Recursively process subfolders
            if (recursive)
            {
                foreach (var subfolder in folder.Folders)
                {
                    // Skip system folders
                    if (subfolder.Name.StartsWith("_") || subfolder.Name == "Forms")
                        continue;
                        
                    await ListFilesRecursiveAsync(context, subfolder.ServerRelativeUrl, fileInfos, recursive);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error listing files from folder {FolderPath}", folderPath);
        }
    }
    
    private string GetContentTypeFromFileName(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".ppt" => "application/vnd.ms-powerpoint",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".txt" => "text/plain",
            _ => "application/octet-stream"
        };
    }
}
