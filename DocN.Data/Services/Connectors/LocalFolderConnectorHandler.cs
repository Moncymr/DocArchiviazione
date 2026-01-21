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
/// Handler per connettori che accedono a cartelle del file system locale.
/// </summary>
/// <remarks>
/// Implementa l'accesso diretto al file system locale tramite:
/// - System.IO per operazioni su file e directory
/// - Supporto per pattern di ricerca file (es. "*.pdf,*.docx,*.xlsx")
/// - Scansione ricorsiva o limitata al livello superiore
/// - Nessuna autenticazione richiesta (usa permessi del processo)
/// 
/// Questo connettore è utile per:
/// - Importazione batch da cartelle di rete mappate
/// - Elaborazione di archivi locali
/// - Testing e sviluppo senza dipendenze esterne
/// - Scenari on-premise con accesso diretto al file system
/// </remarks>
public class LocalFolderConnectorHandler : BaseConnectorHandler
{
    /// <summary>
    /// Inizializza una nuova istanza della classe <see cref="LocalFolderConnectorHandler"/>.
    /// </summary>
    /// <param name="logger">Logger per la registrazione di eventi e errori.</param>
    public LocalFolderConnectorHandler(ILogger logger) : base(logger)
    {
    }
    
    /// <summary>
    /// Testa l'accesso a una cartella locale verificando esistenza e permessi di lettura.
    /// </summary>
    /// <param name="configuration">Configurazione JSON contenente FolderPath, Recursive e FilePattern (opzionale).</param>
    /// <param name="encryptedCredentials">Non utilizzato per cartelle locali (può essere null).</param>
    /// <returns>
    /// Tupla con:
    /// - success: true se la cartella esiste ed è accessibile
    /// - message: numero di file trovati o descrizione errore dettagliata
    /// </returns>
    /// <remarks>
    /// Validazioni eseguite:
    /// 1. Verifica che non sia stato passato l'intero JSON DocumentConnector invece della sola configurazione
    /// 2. Parsing e validazione della struttura JSON della configurazione
    /// 3. Verifica che FolderPath sia specificato e non vuoto
    /// 4. Controllo esistenza fisica della directory nel file system
    /// 5. Test permessi di lettura tramite enumerazione file
    /// 
    /// Messaggi di errore dettagliati:
    /// - Guida l'utente a fornire il formato JSON corretto
    /// - Indica il valore ricevuto in caso di errore di parsing
    /// - Distingue tra cartella inesistente e accesso negato
    /// 
    /// La verifica dei permessi viene eseguita tramite Directory.GetFiles che lancia
    /// UnauthorizedAccessException se i permessi sono insufficienti.
    /// </remarks>
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
    
    /// <summary>
    /// Elenca tutti i file disponibili nella cartella locale specificata.
    /// </summary>
    /// <param name="configuration">Configurazione JSON con FolderPath, Recursive e FilePattern.</param>
    /// <param name="encryptedCredentials">Non utilizzato per cartelle locali.</param>
    /// <param name="path">Non utilizzato, il percorso è specificato nella configurazione.</param>
    /// <returns>Lista di <see cref="ConnectorFileInfo"/> con metadati dei file trovati.</returns>
    /// <remarks>
    /// Operazioni eseguite:
    /// 1. Parsing e validazione della configurazione (FolderPath obbligatorio)
    /// 2. Verifica esistenza fisica della directory
    /// 3. Parsing dei pattern di ricerca file (es. "*.pdf,*.docx,*.xlsx")
    /// 4. Enumerazione file per ogni pattern specificato
    /// 5. Estrazione metadati per ogni file: nome, percorso assoluto, dimensione, data modifica UTC
    /// 
    /// Pattern di ricerca:
    /// - Se FilePattern è null o vuoto, usa "*.*" (tutti i file)
    /// - Supporta pattern multipli separati da virgola (es. "*.pdf,*.docx")
    /// - Pattern standard: wildcards * e ? supportati da Directory.GetFiles
    /// 
    /// Opzioni di ricerca:
    /// - Recursive=true → SearchOption.AllDirectories (include sottocartelle)
    /// - Recursive=false → SearchOption.TopDirectoryOnly (solo cartella principale)
    /// 
    /// Gestione errori:
    /// - Errori su file specifici (es. accesso negato) → loggati come warning, non bloccano l'elaborazione
    /// - Directory inesistente → lancia DirectoryNotFoundException
    /// - Configurazione invalida → lancia InvalidOperationException
    /// 
    /// Il metodo determina automaticamente il tipo MIME basandosi sull'estensione del file.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Lanciata se la configurazione è invalida o FolderPath mancante.</exception>
    /// <exception cref="DirectoryNotFoundException">Lanciata se la cartella specificata non esiste.</exception>
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
    
    /// <summary>
    /// Scarica un file dal file system locale copiandolo in memoria.
    /// </summary>
    /// <param name="configuration">Configurazione JSON (non utilizzata nel download).</param>
    /// <param name="encryptedCredentials">Non utilizzato per cartelle locali.</param>
    /// <param name="filePath">Percorso assoluto del file da leggere.</param>
    /// <returns>MemoryStream contenente il contenuto del file.</returns>
    /// <remarks>
    /// Processo di lettura file:
    /// 1. Verifica esistenza del file nel file system
    /// 2. Apertura del file in modalità lettura tramite File.OpenRead
    /// 3. Copia completa del contenuto in un MemoryStream
    /// 4. Chiusura automatica del FileStream (pattern using)
    /// 5. Reset posizione MemoryStream a 0 per la lettura
    /// 
    /// Gestione del locking:
    /// - Il file viene copiato completamente in memoria per evitare lock prolungati sul file system
    /// - Il FileStream viene chiuso immediatamente dopo la copia
    /// - Il MemoryStream restituito è completamente indipendente dal file originale
    /// 
    /// Questa strategia permette:
    /// - Accesso concorrente al file da parte di altri processi
    /// - Elaborazione del contenuto anche se il file viene modificato/cancellato successivamente
    /// - Nessun lock mantenuto sul file durante l'elaborazione
    /// 
    /// Il chiamante è responsabile della disposizione del MemoryStream restituito.
    /// </remarks>
    /// <exception cref="FileNotFoundException">Lanciata se il file specificato non esiste.</exception>
    /// <exception cref="UnauthorizedAccessException">Lanciata se mancano i permessi di lettura sul file.</exception>
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
    
    /// <summary>
    /// Analizza la stringa dei pattern di ricerca file in una lista di pattern individuali.
    /// </summary>
    /// <param name="filePattern">Stringa contenente pattern separati da virgola (es. "*.pdf,*.docx,*.xlsx").</param>
    /// <returns>Lista di pattern individuali, o ["*.*"] se filePattern è null/vuoto.</returns>
    /// <remarks>
    /// Elaborazione eseguita:
    /// 1. Se filePattern è null o vuoto → restituisce lista con singolo pattern "*.*" (tutti i file)
    /// 2. Split della stringa per virgola (StringSplitOptions.RemoveEmptyEntries ignora entry vuote)
    /// 3. Trim degli spazi bianchi da ogni pattern
    /// 4. Restituzione lista di pattern puliti
    /// 
    /// Esempi di input validi:
    /// - "*.pdf" → ["*.pdf"]
    /// - "*.pdf,*.docx" → ["*.pdf", "*.docx"]
    /// - "*.pdf, *.docx, *.xlsx" → ["*.pdf", "*.docx", "*.xlsx"] (spazi rimossi)
    /// - null o "" → ["*.*"]
    /// 
    /// I pattern supportano wildcards standard:
    /// - * : qualsiasi sequenza di caratteri
    /// - ? : singolo carattere qualsiasi
    /// </remarks>
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
    
    /// <summary>
    /// Determina il tipo MIME del contenuto basandosi sull'estensione del file.
    /// </summary>
    /// <param name="extension">Estensione del file (con punto, es. ".pdf").</param>
    /// <returns>Stringa contenente il tipo MIME appropriato (es. "application/pdf").</returns>
    /// <remarks>
    /// Mappa le estensioni comuni ai rispettivi tipi MIME:
    /// 
    /// Documenti Office:
    /// - .doc, .docx (Word)
    /// - .xls, .xlsx (Excel)
    /// - .ppt, .pptx (PowerPoint)
    /// 
    /// Documenti e dati:
    /// - .pdf (Adobe PDF)
    /// - .txt (testo semplice)
    /// - .csv (dati tabulari)
    /// 
    /// Formati strutturati:
    /// - .json (JSON)
    /// - .xml (XML)
    /// - .html, .htm (HTML)
    /// 
    /// Default: "application/octet-stream" per estensioni non riconosciute (dati binari generici)
    /// 
    /// La mappatura case-insensitive (ToLowerInvariant) garantisce il funzionamento
    /// con estensioni in maiuscolo/minuscolo/misto.
    /// </remarks>
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
