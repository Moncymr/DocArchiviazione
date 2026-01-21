using Microsoft.Extensions.Logging;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Drive.v3.Data;

namespace DocN.Data.Services.Connectors;

/// <summary>
/// Configuration for Google Drive connector
/// </summary>
public class GoogleDriveConfiguration
{
    public string FolderId { get; set; } = "root";
    public bool Recursive { get; set; } = true;
}

/// <summary>
/// Credentials for Google Drive connector
/// </summary>
public class GoogleDriveCredentials
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
}

/// <summary>
/// Handler for Google Drive connectors using Google Drive API v3
/// </summary>
public class GoogleDriveConnectorHandler : BaseConnectorHandler
{
    public GoogleDriveConnectorHandler(ILogger logger) : base(logger)
    {
    }
    
    public override async Task<(bool success, string message)> TestConnectionAsync(string configuration, string? encryptedCredentials)
    {
        try
        {
            var config = ParseConfiguration<GoogleDriveConfiguration>(configuration);
            if (config == null)
            {
                return (false, "Invalid configuration: Unable to parse JSON.");
            }
            
            if (string.IsNullOrEmpty(encryptedCredentials))
            {
                return (false, "Credentials are required for Google Drive connector. Please provide ClientId, ClientSecret, and RefreshToken.");
            }
            
            var credentials = ParseConfiguration<GoogleDriveCredentials>(encryptedCredentials);
            if (credentials == null || string.IsNullOrEmpty(credentials.ClientId) || 
                string.IsNullOrEmpty(credentials.ClientSecret) || string.IsNullOrEmpty(credentials.RefreshToken))
            {
                return (false, "Invalid credentials: ClientId, ClientSecret, and RefreshToken are required.");
            }
            
            var service = CreateDriveService(credentials);
            
            // Test connection by getting folder information
            var request = service.Files.Get(config.FolderId);
            request.Fields = "id, name, mimeType";
            var folder = await request.ExecuteAsync();
            
            if (folder == null)
            {
                return (false, $"Unable to access folder with ID: {config.FolderId}");
            }
            
            return (true, $"Connection successful - Folder: {folder.Name}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing Google Drive connection");
            return (false, $"Connection test failed: {ex.Message}");
        }
    }
    
    public override async Task<List<ConnectorFileInfo>> ListFilesAsync(string configuration, string? encryptedCredentials, string? path = null)
    {
        var fileInfos = new List<ConnectorFileInfo>();
        
        try
        {
            var config = ParseConfiguration<GoogleDriveConfiguration>(configuration);
            if (config == null || string.IsNullOrEmpty(encryptedCredentials))
            {
                throw new InvalidOperationException("Invalid configuration or credentials");
            }
            
            var credentials = ParseConfiguration<GoogleDriveCredentials>(encryptedCredentials);
            if (credentials == null)
            {
                throw new InvalidOperationException("Invalid credentials");
            }
            
            var service = CreateDriveService(credentials);
            
            // Use specified folder ID or default from config
            var folderId = path ?? config.FolderId ?? "root";
            
            await ListFilesRecursiveAsync(service, folderId, fileInfos, config.Recursive);
            
            _logger.LogInformation("Listed {Count} files from Google Drive folder {FolderId}", fileInfos.Count, folderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing files from Google Drive");
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
            
            var credentials = ParseConfiguration<GoogleDriveCredentials>(encryptedCredentials);
            if (credentials == null)
            {
                throw new InvalidOperationException("Invalid credentials");
            }
            
            var service = CreateDriveService(credentials);
            
            // Download file content
            var request = service.Files.Get(filePath);
            var memoryStream = new MemoryStream();
            await request.DownloadAsync(memoryStream);
            memoryStream.Position = 0;
            
            return memoryStream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading file from Google Drive: {FilePath}", filePath);
            throw;
        }
    }
    
    private DriveService CreateDriveService(GoogleDriveCredentials credentials)
    {
        var tokenResponse = new Google.Apis.Auth.OAuth2.Responses.TokenResponse
        {
            RefreshToken = credentials.RefreshToken
        };
        
        var userCredential = new UserCredential(
            new Google.Apis.Auth.OAuth2.Flows.GoogleAuthorizationCodeFlow(
                new Google.Apis.Auth.OAuth2.Flows.GoogleAuthorizationCodeFlow.Initializer
                {
                    ClientSecrets = new ClientSecrets
                    {
                        ClientId = credentials.ClientId,
                        ClientSecret = credentials.ClientSecret
                    }
                }
            ),
            "user",
            tokenResponse
        );
        
        return new DriveService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = userCredential,
            ApplicationName = "DocN"
        });
    }
    
    private async Task ListFilesRecursiveAsync(DriveService service, string folderId, List<ConnectorFileInfo> fileInfos, bool recursive)
    {
        try
        {
            var request = service.Files.List();
            request.Q = $"'{folderId}' in parents and trashed=false";
            request.Fields = "files(id, name, mimeType, size, modifiedTime)";
            request.PageSize = 1000;
            
            string? pageToken = null;
            do
            {
                request.PageToken = pageToken;
                var result = await request.ExecuteAsync();
                
                if (result.Files == null) break;
                
                foreach (var file in result.Files)
                {
                    if (file.MimeType == "application/vnd.google-apps.folder")
                    {
                        // It's a folder
                        if (recursive)
                        {
                            await ListFilesRecursiveAsync(service, file.Id, fileInfos, recursive);
                        }
                    }
                    else
                    {
                        // It's a file - use UTC DateTime consistently
                        var modifiedDate = file.ModifiedTimeDateTimeOffset?.UtcDateTime;
                        
                        fileInfos.Add(new ConnectorFileInfo
                        {
                            Name = file.Name,
                            Path = file.Id, // Use file ID as path for Google Drive
                            Size = file.Size ?? 0,
                            ModifiedDate = modifiedDate,
                            IsFolder = false,
                            ContentType = file.MimeType ?? "application/octet-stream"
                        });
                    }
                }
                
                pageToken = result.NextPageToken;
            }
            while (pageToken != null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error listing files from folder {FolderId}", folderId);
        }
    }
}
