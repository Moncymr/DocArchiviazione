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
/// Handler per connettori Google Drive utilizzando Google Drive API v3.
/// </summary>
/// <remarks>
/// Implementa l'integrazione con Google Drive tramite:
/// - Google Drive API v3 per operazioni su file e cartelle
/// - Google.Apis.Auth per autenticazione OAuth 2.0
/// - Supporto per navigazione ricorsiva delle cartelle
/// - Paginazione automatica per gestire grandi quantità di file (max 1000 per pagina)
/// - Utilizzo di file ID come identificatori invece di percorsi
/// 
/// Autenticazione richiesta:
/// - ClientId: Client ID dalla Google Cloud Console
/// - ClientSecret: Client Secret dall'app OAuth
/// - RefreshToken: Token di refresh OAuth 2.0 ottenuto tramite flusso di autorizzazione
/// 
/// Setup Google Cloud Console necessario:
/// 1. Creare progetto in Google Cloud Console
/// 2. Abilitare Google Drive API
/// 3. Configurare schermata consenso OAuth
/// 4. Creare credenziali OAuth 2.0 (Web application)
/// 5. Ottenere refresh token tramite flusso OAuth (authorization code grant)
/// 6. Scope richiesti: https://www.googleapis.com/auth/drive.readonly (minimo)
/// </remarks>
public class GoogleDriveConnectorHandler : BaseConnectorHandler
{
    /// <summary>
    /// Inizializza una nuova istanza della classe <see cref="GoogleDriveConnectorHandler"/>.
    /// </summary>
    /// <param name="logger">Logger per la registrazione di eventi e errori.</param>
    public GoogleDriveConnectorHandler(ILogger logger) : base(logger)
    {
    }
    
    /// <summary>
    /// Testa la connessione a Google Drive verificando l'autenticazione e l'accesso alla cartella specificata.
    /// </summary>
    /// <param name="configuration">Configurazione JSON contenente FolderId (default "root") e opzione Recursive.</param>
    /// <param name="encryptedCredentials">Credenziali JSON crittografate con ClientId, ClientSecret e RefreshToken.</param>
    /// <returns>
    /// Tupla con:
    /// - success: true se la connessione è valida
    /// - message: nome della cartella accessibile o descrizione errore
    /// </returns>
    /// <remarks>
    /// Il test verifica:
    /// 1. Validità del formato della configurazione JSON
    /// 2. Presenza e completezza delle credenziali OAuth (ClientId, ClientSecret, RefreshToken)
    /// 3. Autenticazione OAuth 2.0 tramite refresh token
    /// 4. Accesso alla cartella specificata tramite Files.Get API
    /// 5. Verifica che l'item esista e sia accessibile
    /// 
    /// Identificatori cartella supportati:
    /// - "root": cartella principale del drive (My Drive)
    /// - ID specifico: stringa alfanumerica univoca della cartella Google Drive
    /// 
    /// La verifica accede ai metadati della cartella (id, name, mimeType) senza scaricare contenuti.
    /// </remarks>
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
    
    /// <summary>
    /// Elenca tutti i file disponibili in Google Drive a partire dalla cartella specificata.
    /// </summary>
    /// <param name="configuration">Configurazione JSON con FolderId (default "root") e opzione Recursive.</param>
    /// <param name="encryptedCredentials">Credenziali OAuth 2.0 crittografate (ClientId, ClientSecret, RefreshToken).</param>
    /// <param name="path">ID cartella specifica da cui iniziare (opzionale, usa config.FolderId se null).</param>
    /// <returns>Lista di <see cref="ConnectorFileInfo"/> con metadati dei file trovati.</returns>
    /// <remarks>
    /// Operazioni eseguite tramite Google Drive API v3:
    /// 1. Parsing e validazione della configurazione e credenziali
    /// 2. Creazione del DriveService autenticato
    /// 3. Determinazione del folder ID (path parameter, config.FolderId, o "root")
    /// 4. Scansione ricorsiva (se abilitata) usando query API specifiche
    /// 5. Estrazione metadati: nome, ID (usato come Path), dimensione, data modifica UTC, MIME type
    /// 
    /// Query Google Drive API:
    /// - Filtro: "'{folderId}' in parents and trashed=false"
    /// - Fields: "files(id, name, mimeType, size, modifiedTime)"
    /// - PageSize: 1000 (massimo permesso dall'API)
    /// - Paginazione: gestita tramite pageToken per risultati oltre 1000 file
    /// 
    /// Distinzione file/cartelle:
    /// - Cartella: mimeType = "application/vnd.google-apps.folder"
    /// - File: qualsiasi altro mimeType
    /// - Solo i file vengono aggiunti alla lista risultante
    /// - Le cartelle vengono elaborate ricorsivamente se recursive=true
    /// 
    /// Note sui percorsi:
    /// - Google Drive usa ID univoci invece di percorsi gerarchici
    /// - Il campo Path contiene l'ID del file (necessario per il download)
    /// - I file possono avere più parent (non strettamente gerarchico)
    /// 
    /// Gestione date:
    /// - ModifiedTime restituito come DateTimeOffset
    /// - Convertito in DateTime UTC per consistenza
    /// 
    /// Gli errori su cartelle specifiche vengono loggati come warning
    /// ma non interrompono l'elaborazione delle altre cartelle.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Lanciata se configurazione o credenziali sono invalide.</exception>
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
    
    /// <summary>
    /// Scarica un file specifico da Google Drive utilizzando il suo ID univoco.
    /// </summary>
    /// <param name="configuration">Configurazione JSON (non utilizzata nel download).</param>
    /// <param name="encryptedCredentials">Credenziali OAuth 2.0 crittografate.</param>
    /// <param name="filePath">ID univoco del file da scaricare (non un percorso, ma l'ID alfanumerico Google Drive).</param>
    /// <returns>MemoryStream contenente il contenuto del file scaricato.</returns>
    /// <remarks>
    /// Processo di download tramite Google Drive API v3:
    /// 1. Validazione presenza credenziali
    /// 2. Autenticazione e creazione del DriveService
    /// 3. Download del contenuto tramite Files.Get(fileId).Download()
    /// 4. Copia dello stream in MemoryStream per garantire disponibilità
    /// 5. Reset posizione stream a 0 per la lettura
    /// 
    /// Note importanti sui file Google:
    /// - Google Docs, Sheets, Slides: richiedono export in formato specifico
    /// - File non-Google (PDF, Office, immagini): download diretto del contenuto binario
    /// - Il parametro filePath contiene l'ID del file, non un percorso gerarchico
    /// - L'ID è ottenuto dal campo Path di ConnectorFileInfo restituito da ListFilesAsync
    /// 
    /// Il file viene copiato in memoria per:
    /// - Evitare dipendenze dal ciclo di vita della connessione HTTP
    /// - Permettere multiple letture del contenuto
    /// - Garantire disponibilità anche dopo chiusura del DriveService
    /// 
    /// Il chiamante è responsabile della disposizione del MemoryStream restituito.
    /// Per file molto grandi, considerare implementazioni alternative con streaming diretto.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Lanciata se credenziali sono invalide.</exception>
    /// <exception cref="Google.GoogleApiException">Lanciata se il file non esiste o non è accessibile.</exception>
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
    
    /// <summary>
    /// Crea un client Google Drive Service autenticato utilizzando le credenziali OAuth 2.0.
    /// </summary>
    /// <param name="credentials">Credenziali contenenti ClientId, ClientSecret e RefreshToken.</param>
    /// <returns>DriveService autenticato e pronto per chiamate API Google Drive v3.</returns>
    /// <remarks>
    /// Processo di autenticazione OAuth 2.0:
    /// 1. Crea un TokenResponse con il refresh token fornito
    /// 2. Configura GoogleAuthorizationCodeFlow con ClientSecrets
    /// 3. Crea UserCredential combinando flow e token response
    /// 4. Istanzia DriveService con la credential e ApplicationName
    /// 
    /// Flusso OAuth utilizzato:
    /// - Tipo: Authorization Code Flow con Refresh Token
    /// - Grant Type: refresh_token (per ottenere nuovi access token)
    /// - Il refresh token è long-lived e non scade (salvo revoca)
    /// - Gli access token vengono rinnovati automaticamente dal client
    /// 
    /// Ottenimento del Refresh Token (one-time setup):
    /// 1. Implementare Authorization Code Flow con prompt utente
    /// 2. L'utente autorizza l'app tramite Google OAuth consent screen
    /// 3. Ottenere authorization code dalla callback URL
    /// 4. Scambiare authorization code per access token + refresh token
    /// 5. Salvare il refresh token in modo sicuro (crittografato)
    /// 6. Utilizzare il refresh token per autenticazioni successive
    /// 
    /// ApplicationName:
    /// - Identificatore dell'applicazione nei log Google
    /// - Visibile all'utente nella schermata consenso OAuth
    /// - Utilizzato per analytics e rate limiting
    /// 
    /// Il DriveService gestisce automaticamente:
    /// - Rinnovo automatico degli access token scaduti
    /// - Retry con backoff esponenziale su errori transienti
    /// - Rispetto dei rate limits di Google Drive API
    /// </remarks>
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
    
    /// <summary>
    /// Elenca ricorsivamente i file da una cartella Google Drive e dalle sue sottocartelle.
    /// </summary>
    /// <param name="service">DriveService autenticato.</param>
    /// <param name="folderId">ID univoco della cartella Google Drive da elaborare ("root" per cartella principale).</param>
    /// <param name="fileInfos">Lista di accumulo dove aggiungere i file trovati.</param>
    /// <param name="recursive">Se true, elabora ricorsivamente tutte le sottocartelle.</param>
    /// <remarks>
    /// Operazioni eseguite tramite Google Drive API v3:
    /// 
    /// 1. Costruzione query di ricerca:
    ///    - Filtro: "'{folderId}' in parents and trashed=false"
    ///    - Esclude file nel cestino
    ///    - Trova solo figli diretti della cartella specificata
    /// 
    /// 2. Configurazione richiesta API:
    ///    - Fields: "files(id, name, mimeType, size, modifiedTime)" (solo campi necessari)
    ///    - PageSize: 1000 (massimo consentito, ottimizza numero di chiamate API)
    ///    - Supporta paginazione con NextPageToken
    /// 
    /// 3. Iterazione paginata:
    ///    - Loop do-while per gestire risultati multipagina
    ///    - Utilizza pageToken per richiedere pagine successive
    ///    - Continua fino a quando NextPageToken è null
    /// 
    /// 4. Elaborazione item:
    ///    - Cartelle (mimeType = "application/vnd.google-apps.folder"):
    ///      * Se recursive=true → chiamata ricorsiva con folder.Id
    ///      * Le cartelle non vengono aggiunte alla lista risultati
    ///    - File (altri mimeType):
    ///      * Aggiunta a fileInfos con metadati completi
    ///      * Path = file.Id (ID univoco necessario per download)
    ///      * Size = file.Size ?? 0 (nullable per Google Docs)
    ///      * ModifiedDate = convertito in UTC DateTime
    ///      * ContentType = file.MimeType o "application/octet-stream"
    /// 
    /// 5. Gestione file Google Workspace:
    ///    - Google Docs/Sheets/Slides hanno Size=null (non file binari nativi)
    ///    - Dimensione impostata a 0 per questi file
    ///    - MimeType specifici: "application/vnd.google-apps.document", etc.
    ///    - Richiedono export in formato standard per il download
    /// 
    /// Ottimizzazioni performance:
    /// - Richiesta solo campi necessari (riduce payload risposta)
    /// - PageSize massimo per minimizzare numero di chiamate API
    /// - Paginazione efficiente per grandi quantità di file
    /// 
    /// Gli errori su cartelle specifiche (es. permessi negati) vengono loggati come warning
    /// ma non interrompono l'elaborazione delle altre cartelle.
    /// </remarks>
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
