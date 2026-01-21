using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace DocN.Data.Services.Connectors;

/// <summary>
/// Configuration for LocalFolder connector
/// </summary>
public class LocalFolderConfiguration
{
    public string FolderPath { get; set; } = string.Empty;
    public bool Recursive { get; set; } = true;
    public string? FilePattern { get; set; } // e.g., "*.pdf,*.docx,*.xlsx"
}

/// <summary>
/// Handler for local folder connectors
/// </summary>
public class LocalFolderConnectorHandler : BaseConnectorHandler
{
    public LocalFolderConnectorHandler(ILogger logger) : base(logger)
    {
    }
    
    public override async Task<(bool success, string message)> TestConnectionAsync(string configuration, string? encryptedCredentials)
    {
        try
        {
            // Check if user passed the entire DocumentConnector JSON instead of just the configuration
            if (configuration.Contains("\"connectorType\"") || configuration.Contains("\"ConnectorType\""))
            {
                return (false, "Invalid configuration: You passed the entire DocumentConnector JSON. Please use only the 'configuration' field value. Example: {\"folderPath\":\"C:\\\\Path\",\"recursive\":true,\"filePattern\":\"*.pdf\"}");
            }
            
            var config = ParseConfiguration<LocalFolderConfiguration>(configuration);
            if (config == null)
            {
                return (false, $"Invalid configuration: Unable to parse JSON. Expected format: {{\"folderPath\":\"C:\\\\Path\",\"recursive\":true,\"filePattern\":\"*.pdf\"}}. Received: {configuration}");
            }
            
            if (string.IsNullOrEmpty(config.FolderPath))
            {
                return (false, $"Invalid configuration: FolderPath is required and cannot be empty. Expected format: {{\"folderPath\":\"C:\\\\Path\",\"recursive\":true}}. Received configuration with FolderPath='{config.FolderPath}'");
            }
            
            if (!Directory.Exists(config.FolderPath))
            {
                return (false, $"Folder does not exist: {config.FolderPath}");
            }
            
            // Test read access by listing files
            var files = Directory.GetFiles(config.FolderPath, "*.*", 
                config.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
            
            return await Task.FromResult((true, $"Connection successful - {files.Length} files found"));
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Access denied to folder");
            return (false, "Access denied - check folder permissions");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing local folder connection");
            return (false, $"Connection test failed: {ex.Message}");
        }
    }
    
    public override async Task<List<ConnectorFileInfo>> ListFilesAsync(string configuration, string? encryptedCredentials, string? path = null)
    {
        var fileInfos = new List<ConnectorFileInfo>();
        
        try
        {
            var config = ParseConfiguration<LocalFolderConfiguration>(configuration);
            if (config == null || string.IsNullOrEmpty(config.FolderPath))
            {
                throw new InvalidOperationException("Invalid configuration: FolderPath is required");
            }
            
            var basePath = config.FolderPath;
            if (!Directory.Exists(basePath))
            {
                throw new DirectoryNotFoundException($"Folder does not exist: {basePath}");
            }
            
            // Parse file patterns
            var patterns = ParseFilePatterns(config.FilePattern);
            
            // Get all files
            var searchOption = config.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            
            foreach (var pattern in patterns)
            {
                var files = Directory.GetFiles(basePath, pattern, searchOption);
                foreach (var file in files)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        fileInfos.Add(new ConnectorFileInfo
                        {
                            Name = fileInfo.Name,
                            Path = file,
                            Size = fileInfo.Length,
                            ModifiedDate = fileInfo.LastWriteTimeUtc,
                            IsFolder = false,
                            ContentType = GetContentType(fileInfo.Extension)
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error reading file info for {File}", file);
                    }
                }
            }
            
            _logger.LogInformation("Listed {Count} files from local folder {Path}", fileInfos.Count, basePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing files from local folder");
            throw;
        }
        
        return await Task.FromResult(fileInfos);
    }
    
    public override async Task<Stream> DownloadFileAsync(string configuration, string? encryptedCredentials, string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"File not found: {filePath}");
            }
            
            // Read file into memory stream to avoid locking the file
            var memoryStream = new MemoryStream();
            using (var fileStream = File.OpenRead(filePath))
            {
                await fileStream.CopyToAsync(memoryStream);
            }
            memoryStream.Position = 0;
            
            return memoryStream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading file from local folder: {FilePath}", filePath);
            throw;
        }
    }
    
    private List<string> ParseFilePatterns(string? filePattern)
    {
        if (string.IsNullOrEmpty(filePattern))
        {
            return new List<string> { "*.*" };
        }
        
        return filePattern.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .ToList();
    }
    
    private string GetContentType(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".ppt" => "application/vnd.ms-powerpoint",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".txt" => "text/plain",
            ".csv" => "text/csv",
            ".json" => "application/json",
            ".xml" => "application/xml",
            ".html" => "text/html",
            ".htm" => "text/html",
            _ => "application/octet-stream"
        };
    }
}
