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
/// Handler per connettori OneDrive utilizzando Microsoft Graph API.
/// </summary>
/// <remarks>
/// Implementa l'integrazione con OneDrive for Business/Personal tramite:
/// - Microsoft Graph API per operazioni su file e cartelle
/// - Azure.Identity per l'autenticazione OAuth 2.0 con flusso Client Credentials
/// - Supporto per navigazione ricorsiva delle cartelle
/// - Gestione degli item ID e percorsi relativi
/// 
/// Autenticazione richiesta:
/// - ClientId: Application (client) ID dall'Azure AD app registration
/// - ClientSecret: Client secret generato per l'app
/// - TenantId: ID del tenant Azure AD
/// - Permessi API: Files.Read.All o Files.ReadWrite.All
/// </remarks>
public class OneDriveConnectorHandler : BaseConnectorHandler
{
    /// <summary>
    /// Inizializza una nuova istanza della classe <see cref="OneDriveConnectorHandler"/>.
    /// </summary>
    /// <param name="logger">Logger per la registrazione di eventi e errori.</param>
    public OneDriveConnectorHandler(ILogger logger) : base(logger)
    {
    }
    
    /// <summary>
    /// Testa la connessione a OneDrive verificando l'autenticazione e l'accesso al drive dell'utente.
    /// </summary>
    /// <param name="configuration">Configurazione JSON contenente FolderPath (opzionale, default "/").</param>
    /// <param name="encryptedCredentials">Credenziali JSON crittografate con ClientId, ClientSecret e TenantId.</param>
    /// <returns>
    /// Tupla con:
    /// - success: true se la connessione è valida
    /// - message: conferma accesso o descrizione errore
    /// </returns>
    /// <remarks>
    /// Il test verifica:
    /// 1. Validità del formato della configurazione JSON
    /// 2. Presenza e completezza delle credenziali (ClientId, ClientSecret, TenantId)
    /// 3. Autenticazione OAuth 2.0 tramite ClientSecretCredential
    /// 4. Accesso al drive dell'utente tramite Microsoft Graph API (endpoint /me/drive)
    /// 5. Accessibilità della cartella specifica (se FolderPath diverso da "/")
    /// 
    /// Utilizza il pattern async/await per operazioni non bloccanti con Microsoft Graph.
    /// </remarks>
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
    
    /// <summary>
    /// Elenca tutti i file disponibili in OneDrive a partire dal percorso specificato.
    /// </summary>
    /// <param name="configuration">Configurazione JSON con FolderPath e opzione Recursive.</param>
    /// <param name="encryptedCredentials">Credenziali OAuth 2.0 crittografate (ClientId, ClientSecret, TenantId).</param>
    /// <param name="path">Percorso specifico da cui iniziare la scansione (opzionale, usa config.FolderPath se null).</param>
    /// <returns>Lista di <see cref="ConnectorFileInfo"/> con metadati dei file trovati.</returns>
    /// <remarks>
    /// Operazioni eseguite:
    /// 1. Parsing e validazione della configurazione e credenziali
    /// 2. Creazione del GraphServiceClient autenticato
    /// 3. Recupero dell'ID del drive tramite endpoint /me/drive
    /// 4. Scansione ricorsiva (se abilitata) a partire dal percorso specificato
    /// 5. Estrazione metadati: nome, percorso completo, dimensione, data modifica UTC, tipo MIME
    /// 
    /// Percorsi supportati:
    /// - "/" per la root del drive
    /// - Percorsi relativi senza slash iniziale per l'API ItemWithPath
    /// 
    /// La ricorsione processa automaticamente tutte le sottocartelle quando abilitata.
    /// Gli errori su cartelle specifiche vengono loggati come warning senza interrompere l'elaborazione.
    /// Il percorso completo viene ricostruito durante la ricorsione per fornire path assoluti.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Lanciata se configurazione, credenziali o accesso al drive sono invalidi.</exception>
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
    
    /// <summary>
    /// Scarica un file specifico da OneDrive utilizzando il suo percorso.
    /// </summary>
    /// <param name="configuration">Configurazione JSON (non utilizzata nel download).</param>
    /// <param name="encryptedCredentials">Credenziali OAuth 2.0 crittografate.</param>
    /// <param name="filePath">Percorso del file da scaricare (relativo alla root, con o senza slash iniziale).</param>
    /// <returns>MemoryStream contenente il contenuto del file scaricato.</returns>
    /// <remarks>
    /// Processo di download tramite Microsoft Graph API:
    /// 1. Validazione presenza credenziali
    /// 2. Autenticazione e creazione del GraphServiceClient
    /// 3. Recupero dell'ID del drive
    /// 4. Download del contenuto tramite endpoint /drives/{driveId}/root:/{path}:/content
    /// 5. Copia dello stream in MemoryStream per garantire disponibilità dopo chiusura connessione
    /// 6. Reset posizione stream a 0 per la lettura
    /// 
    /// Note sui percorsi:
    /// - Il slash iniziale viene rimosso automaticamente per compatibilità con ItemWithPath API
    /// - I percorsi possono contenere sottocartelle (es. "Documents/Subfolder/file.pdf")
    /// 
    /// Il file viene copiato in memoria per evitare problemi di ciclo di vita della connessione HTTP.
    /// Il chiamante è responsabile della disposizione dello stream restituito.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Lanciata se credenziali sono invalide o accesso al drive fallisce.</exception>
    /// <exception cref="FileNotFoundException">Lanciata se il file specificato non esiste in OneDrive.</exception>
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
    
    /// <summary>
    /// Crea un client Microsoft Graph autenticato utilizzando le credenziali OAuth 2.0.
    /// </summary>
    /// <param name="credentials">Credenziali contenenti ClientId, ClientSecret e TenantId.</param>
    /// <returns>GraphServiceClient autenticato e pronto per chiamate API.</returns>
    /// <remarks>
    /// Autenticazione tramite Azure Identity:
    /// 1. Crea un ClientSecretCredential con TenantId, ClientId e ClientSecret
    /// 2. Utilizza il flusso OAuth 2.0 Client Credentials Grant
    /// 3. Istanzia GraphServiceClient con la credential configurata
    /// 
    /// Requisiti Azure AD:
    /// - App registration in Azure AD
    /// - Client secret generato per l'app
    /// - Permessi API Microsoft Graph: Files.Read.All o Files.ReadWrite.All (Application permissions)
    /// - Admin consent concesso per i permessi dell'applicazione
    /// 
    /// Il client gestisce automaticamente il rinnovo del token OAuth e le retry policy.
    /// Utilizza gli endpoint standard di Microsoft Graph (https://graph.microsoft.com).
    /// </remarks>
    private GraphServiceClient CreateGraphClient(OneDriveCredentials credentials)
    {
        var clientSecretCredential = new ClientSecretCredential(
            credentials.TenantId,
            credentials.ClientId,
            credentials.ClientSecret
        );
        
        return new GraphServiceClient(clientSecretCredential);
    }
    
    /// <summary>
    /// Elenca ricorsivamente i file da una cartella OneDrive e dalle sue sottocartelle.
    /// </summary>
    /// <param name="graphClient">GraphServiceClient autenticato.</param>
    /// <param name="driveId">ID univoco del drive OneDrive.</param>
    /// <param name="folderPath">Percorso relativo della cartella da elaborare ("/" per root).</param>
    /// <param name="fileInfos">Lista di accumulo dove aggiungere i file trovati.</param>
    /// <param name="recursive">Se true, elabora ricorsivamente tutte le sottocartelle.</param>
    /// <remarks>
    /// Operazioni eseguite tramite Microsoft Graph API:
    /// 1. Determina l'endpoint API in base al percorso:
    ///    - Root ("/") → /drives/{driveId}/root/children
    ///    - Percorso specifico → /drives/{driveId}/root:/{path}:/children
    /// 2. Recupera gli item (file e cartelle) nella location corrente
    /// 3. Distingue file da cartelle tramite proprietà Folder e File
    /// 4. Per ogni file: estrae metadati (nome, dimensione, data modifica UTC, MIME type)
    /// 5. Per ogni cartella (se ricorsione abilitata): chiamata ricorsiva
    /// 
    /// Costruzione percorsi:
    /// - Rimuove slash iniziali/finali per normalizzazione
    /// - Costruisce percorsi completi concatenando parent path e nome item
    /// - Mantiene slash iniziale nei percorsi risultanti per coerenza
    /// 
    /// Gestione date:
    /// - Utilizza LastModifiedDateTime in formato UTC
    /// - Converte DateTimeOffset in DateTime per compatibilità
    /// 
    /// Gli errori su cartelle specifiche vengono loggati come warning
    /// ma non interrompono l'elaborazione delle altre cartelle.
    /// </remarks>
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
