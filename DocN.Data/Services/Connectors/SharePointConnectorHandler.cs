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
/// Handler per connettori SharePoint Online utilizzando il PnP Framework.
/// </summary>
/// <remarks>
/// Implementa l'integrazione con SharePoint Online utilizzando:
/// - PnP Framework per l'autenticazione e le operazioni CSOM (Client-Side Object Model)
/// - Autenticazione tramite App-Only con ClientId e ClientSecret
/// - Supporto per l'accesso ricorsivo alle librerie documenti
/// - Gestione automatica delle cartelle di sistema (cartelle che iniziano con "_" e "Forms")
/// </remarks>
public class SharePointConnectorHandler : BaseConnectorHandler
{
    /// <summary>
    /// Inizializza una nuova istanza della classe <see cref="SharePointConnectorHandler"/>.
    /// </summary>
    /// <param name="logger">Logger per la registrazione di eventi e errori.</param>
    public SharePointConnectorHandler(ILogger logger) : base(logger)
    {
    }
    
    /// <summary>
    /// Testa la connessione a SharePoint Online verificando l'autenticazione e l'accesso alla libreria specificata.
    /// </summary>
    /// <param name="configuration">Configurazione JSON contenente SiteUrl e FolderPath.</param>
    /// <param name="encryptedCredentials">Credenziali JSON crittografate con ClientId e ClientSecret per l'autenticazione App-Only.</param>
    /// <returns>
    /// Tupla con:
    /// - success: true se la connessione è valida
    /// - message: dettagli sul test (nome del sito o descrizione errore)
    /// </returns>
    /// <remarks>
    /// Il test verifica:
    /// 1. Validità della configurazione (URL sito e percorso cartella richiesti)
    /// 2. Presenza e validità delle credenziali App-Only (ClientId/ClientSecret)
    /// 3. Autenticazione con SharePoint tramite PnP AuthenticationManager
    /// 4. Accesso al sito (caricamento proprietà Web)
    /// 5. Accesso alla cartella specificata (verifica esistenza)
    /// 
    /// Utilizza pattern using per garantire la corretta disposizione del ClientContext.
    /// </remarks>
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
    
    /// <summary>
    /// Elenca tutti i file disponibili nella libreria SharePoint specificata.
    /// </summary>
    /// <param name="configuration">Configurazione JSON con SiteUrl, FolderPath e opzione Recursive.</param>
    /// <param name="encryptedCredentials">Credenziali App-Only crittografate (ClientId/ClientSecret).</param>
    /// <param name="path">Percorso specifico da cui iniziare la scansione (opzionale, usa config.FolderPath se null).</param>
    /// <returns>Lista di <see cref="ConnectorFileInfo"/> con metadati dei file trovati.</returns>
    /// <remarks>
    /// Operazioni eseguite:
    /// 1. Parsing e validazione della configurazione e credenziali
    /// 2. Creazione del ClientContext con autenticazione PnP
    /// 3. Scansione ricorsiva (se abilitata) delle cartelle e file
    /// 4. Estrazione metadati: nome, percorso relativo al server, dimensione, data modifica, tipo contenuto
    /// 
    /// La ricorsione ignora automaticamente le cartelle di sistema SharePoint (nomi che iniziano con "_" e "Forms").
    /// Utilizza pattern using per la corretta disposizione delle risorse di connessione.
    /// Gli errori su cartelle specifiche vengono loggati come warning senza interrompere l'elaborazione.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Lanciata se configurazione o credenziali sono invalide.</exception>
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
    
    /// <summary>
    /// Scarica un file specifico da SharePoint utilizzando il suo percorso relativo al server.
    /// </summary>
    /// <param name="configuration">Configurazione JSON con SiteUrl.</param>
    /// <param name="encryptedCredentials">Credenziali App-Only crittografate.</param>
    /// <param name="filePath">Percorso relativo al server del file (es. "/sites/sitename/Shared Documents/file.pdf").</param>
    /// <returns>MemoryStream contenente il contenuto del file scaricato.</returns>
    /// <remarks>
    /// Processo di download:
    /// 1. Validazione configurazione e credenziali
    /// 2. Autenticazione e creazione ClientContext con PnP
    /// 3. Apertura dello stream binario del file tramite CSOM
    /// 4. Copia del contenuto in un MemoryStream per garantire la disponibilità dopo la chiusura del context
    /// 5. Reset della posizione dello stream a 0 per la lettura
    /// 
    /// Il file viene copiato in memoria per evitare problemi di concorrenza e ciclo di vita della connessione.
    /// Il chiamante è responsabile della disposizione dello stream restituito.
    /// Utilizza pattern using per garantire la corretta disposizione del ClientContext.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Lanciata se configurazione o credenziali sono invalide.</exception>
    /// <exception cref="FileNotFoundException">Lanciata implicitamente da SharePoint se il file non esiste.</exception>
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
    
    /// <summary>
    /// Crea e autentica un ClientContext SharePoint utilizzando le credenziali App-Only.
    /// </summary>
    /// <param name="siteUrl">URL completo del sito SharePoint (es. "https://tenant.sharepoint.com/sites/sitename").</param>
    /// <param name="credentials">Credenziali contenenti ClientId e ClientSecret per l'autenticazione App-Only.</param>
    /// <returns>ClientContext autenticato e pronto all'uso.</returns>
    /// <remarks>
    /// Autenticazione tramite PnP Framework:
    /// 1. Estrae il tenant URL dall'URL del sito (es. "https://tenant.sharepoint.com")
    /// 2. Crea un AuthenticationManager PnP con ClientId, ClientSecret e tenant URL
    /// 3. Ottiene un ClientContext autenticato specifico per il sito richiesto
    /// 
    /// L'autenticazione App-Only richiede:
    /// - Registrazione di un'app in Azure AD con permessi SharePoint
    /// - ClientId (Application ID) dall'app Azure AD
    /// - ClientSecret generato per l'app
    /// - Permessi API SharePoint appropriati (Sites.Read.All o Sites.FullControl.All)
    /// 
    /// Il ClientContext restituito deve essere disposto dal chiamante (pattern using consigliato).
    /// </remarks>
    private async Task<ClientContext> CreateClientContextAsync(string siteUrl, SharePointCredentials credentials)
    {
        // Extract tenant from site URL (e.g., "https://tenant.sharepoint.com" -> "tenant.sharepoint.com")
        var uri = new Uri(siteUrl);
        var tenantUrl = $"{uri.Scheme}://{uri.Host}";
        
        var authManager = new PnP.Framework.AuthenticationManager(credentials.ClientId, credentials.ClientSecret, tenantUrl);
        return await authManager.GetContextAsync(siteUrl);
    }
    
    /// <summary>
    /// Elenca ricorsivamente i file da una cartella SharePoint e dalle sue sottocartelle.
    /// </summary>
    /// <param name="context">ClientContext SharePoint autenticato.</param>
    /// <param name="folderPath">Percorso relativo al server della cartella da elaborare.</param>
    /// <param name="fileInfos">Lista di accumulo dove aggiungere i file trovati.</param>
    /// <param name="recursive">Se true, elabora ricorsivamente tutte le sottocartelle.</param>
    /// <remarks>
    /// Operazioni eseguite:
    /// 1. Caricamento della cartella tramite URL relativo al server
    /// 2. Caricamento batch dei file con proprietà: Name, ServerRelativeUrl, Length, TimeLastModified
    /// 3. Caricamento batch delle sottocartelle (se ricorsione abilitata)
    /// 4. Esecuzione query CSOM per recuperare i dati da SharePoint
    /// 5. Iterazione sui file e aggiunta alla lista con mappatura tipo contenuto
    /// 6. Chiamata ricorsiva per ogni sottocartella (escludendo cartelle di sistema)
    /// 
    /// Ottimizzazioni:
    /// - Utilizza Include() per caricare solo i campi necessari (riduce traffico di rete)
    /// - Carica file e cartelle in una singola query batch
    /// - Ignora cartelle di sistema (nome inizia con "_" o nome uguale a "Forms")
    /// 
    /// Gli errori su cartelle specifiche (es. permessi negati) vengono loggati come warning
    /// ma non interrompono l'elaborazione delle altre cartelle.
    /// </remarks>
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
    
    /// <summary>
    /// Determina il tipo MIME del contenuto basandosi sull'estensione del file.
    /// </summary>
    /// <param name="fileName">Nome del file (con estensione).</param>
    /// <returns>Stringa contenente il tipo MIME appropriato (es. "application/pdf", "text/plain").</returns>
    /// <remarks>
    /// Mappa le estensioni comuni dei file Office e documenti ai rispettivi tipi MIME:
    /// - Documenti PDF
    /// - Documenti Word (.doc, .docx)
    /// - Fogli Excel (.xls, .xlsx)
    /// - Presentazioni PowerPoint (.ppt, .pptx)
    /// - File di testo (.txt)
    /// - Default: "application/octet-stream" per estensioni non riconosciute
    /// 
    /// La mappatura case-insensitive garantisce il funzionamento con estensioni maiuscole/minuscole.
    /// </remarks>
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
