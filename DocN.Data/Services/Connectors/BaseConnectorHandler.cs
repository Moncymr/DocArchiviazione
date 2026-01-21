using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DocN.Data.Constants;
using Microsoft.Extensions.Logging;

namespace DocN.Data.Services.Connectors;

/// <summary>
/// Classe base astratta per l'implementazione dei connettori ai repository esterni.
/// Fornisce funzionalità comuni per la gestione delle connessioni, autenticazione e operazioni sui file.
/// </summary>
/// <remarks>
/// Tutti i connettori specifici (SharePoint, OneDrive, Google Drive, FTP, SFTP, Local Folder)
/// devono derivare da questa classe e implementare i metodi astratti per la gestione
/// delle operazioni specifiche del repository.
/// </remarks>
public abstract class BaseConnectorHandler
{
    protected readonly ILogger _logger;
    
    /// <summary>
    /// Inizializza una nuova istanza della classe <see cref="BaseConnectorHandler"/>.
    /// </summary>
    /// <param name="logger">Logger per la registrazione di eventi e errori.</param>
    protected BaseConnectorHandler(ILogger logger)
    {
        _logger = logger;
    }
    
    /// <summary>
    /// Testa la connessione al repository esterno verificando la validità delle credenziali e della configurazione.
    /// </summary>
    /// <param name="configuration">Configurazione JSON specifica del connettore (URL, percorsi, opzioni).</param>
    /// <param name="encryptedCredentials">Credenziali crittografate in formato JSON (opzionale per connettori senza autenticazione).</param>
    /// <returns>
    /// Una tupla contenente:
    /// - success: true se la connessione è riuscita, false altrimenti
    /// - message: messaggio descrittivo del risultato del test
    /// </returns>
    /// <remarks>
    /// Questo metodo verifica la connettività al repository senza scaricare file.
    /// Implementazioni specifiche devono validare autenticazione, autorizzazioni e accessibilità delle risorse.
    /// </remarks>
    public abstract Task<(bool success, string message)> TestConnectionAsync(string configuration, string? encryptedCredentials);
    
    /// <summary>
    /// Elenca tutti i file disponibili nel repository esterno secondo la configurazione specificata.
    /// </summary>
    /// <param name="configuration">Configurazione JSON del connettore con parametri di ricerca.</param>
    /// <param name="encryptedCredentials">Credenziali crittografate per l'accesso al repository.</param>
    /// <param name="path">Percorso specifico da cui elencare i file (opzionale, usa la configurazione di default se null).</param>
    /// <returns>Lista di <see cref="ConnectorFileInfo"/> con metadati dei file trovati.</returns>
    /// <remarks>
    /// Il metodo può supportare ricerca ricorsiva nelle sottocartelle in base alla configurazione.
    /// Gli errori di accesso a specifiche cartelle vengono loggati ma non interrompono l'elaborazione.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Lanciata se la configurazione o le credenziali sono invalide.</exception>
    public abstract Task<List<ConnectorFileInfo>> ListFilesAsync(string configuration, string? encryptedCredentials, string? path = null);
    
    /// <summary>
    /// Scarica un file specifico dal repository esterno.
    /// </summary>
    /// <param name="configuration">Configurazione JSON del connettore.</param>
    /// <param name="encryptedCredentials">Credenziali crittografate per l'accesso al repository.</param>
    /// <param name="filePath">Percorso completo o identificatore del file da scaricare (formato dipende dal tipo di connettore).</param>
    /// <returns>Stream contenente il contenuto del file. Il chiamante è responsabile della sua disposizione.</returns>
    /// <remarks>
    /// Il file viene generalmente caricato in memoria (MemoryStream) per evitare problemi di concorrenza
    /// e gestione del ciclo di vita della connessione. Lo stream deve essere disposto dal chiamante.
    /// </remarks>
    /// <exception cref="FileNotFoundException">Lanciata se il file specificato non esiste.</exception>
    /// <exception cref="InvalidOperationException">Lanciata se la configurazione o le credenziali sono invalide.</exception>
    public abstract Task<Stream> DownloadFileAsync(string configuration, string? encryptedCredentials, string filePath);
    
    /// <summary>
    /// Calcola l'hash SHA-256 del contenuto di uno stream per l'identificazione univoca dei file.
    /// </summary>
    /// <param name="stream">Stream del file di cui calcolare l'hash.</param>
    /// <returns>Stringa contenente l'hash SHA-256 in formato esadecimale lowercase.</returns>
    /// <remarks>
    /// Il metodo preserva la posizione originale dello stream.
    /// Prima riporta lo stream all'inizio, calcola l'hash, poi ripristina la posizione originale.
    /// Utilizza un pattern using per la corretta disposizione delle risorse crittografiche.
    /// L'hash è utilizzato per il rilevamento di duplicati e verifica dell'integrità dei file.
    /// </remarks>
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
    /// Deserializza una stringa JSON in un oggetto di configurazione tipizzato.
    /// </summary>
    /// <typeparam name="T">Tipo di configurazione da deserializzare (deve essere una classe).</typeparam>
    /// <param name="configuration">Stringa JSON contenente la configurazione.</param>
    /// <returns>Oggetto di tipo T deserializzato, o null se la deserializzazione fallisce.</returns>
    /// <remarks>
    /// Utilizza opzioni di deserializzazione case-insensitive per maggiore flessibilità
    /// nell'interpretazione dei nomi delle proprietà JSON (es. "folderPath" o "FolderPath").
    /// Gli errori di parsing vengono loggati e il metodo restituisce null invece di lanciare eccezioni,
    /// permettendo una gestione degli errori più robusta nel codice chiamante.
    /// </remarks>
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
/// Factory per la creazione dinamica di handler di connettori specifici in base al tipo richiesto.
/// </summary>
/// <remarks>
/// Implementa il pattern Factory per centralizzare la logica di istanziazione dei connettori.
/// Supporta tutti i tipi di connettori definiti in <see cref="ConnectorTypes"/>.
/// </remarks>
public class ConnectorHandlerFactory
{
    private readonly ILogger _logger;
    
    /// <summary>
    /// Inizializza una nuova istanza della classe <see cref="ConnectorHandlerFactory"/>.
    /// </summary>
    /// <param name="logger">Logger condiviso da tutti gli handler creati dalla factory.</param>
    public ConnectorHandlerFactory(ILogger logger)
    {
        _logger = logger;
    }
    
    /// <summary>
    /// Crea un'istanza dell'handler appropriato in base al tipo di connettore specificato.
    /// </summary>
    /// <param name="connectorType">Tipo di connettore da creare (valore definito in <see cref="ConnectorTypes"/>).</param>
    /// <returns>Istanza di <see cref="BaseConnectorHandler"/> specifica per il tipo richiesto.</returns>
    /// <remarks>
    /// Utilizza un'espressione switch per mappare i tipi di connettore alle rispettive implementazioni:
    /// - LocalFolder: accesso al file system locale
    /// - SharePoint: integrazione con SharePoint Online tramite PnP Framework
    /// - OneDrive: integrazione con OneDrive tramite Microsoft Graph API
    /// - GoogleDrive: integrazione con Google Drive tramite Google Drive API v3
    /// - FTP: connettore FTP standard (implementazione placeholder)
    /// - SFTP: connettore SFTP sicuro (implementazione placeholder)
    /// </remarks>
    /// <exception cref="NotSupportedException">Lanciata se il tipo di connettore non è riconosciuto o supportato.</exception>
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
/// Rappresenta le informazioni e i metadati di un file proveniente da un connettore esterno.
/// </summary>
/// <remarks>
/// Questa classe fornisce una struttura dati unificata per rappresentare i file
/// indipendentemente dal tipo di connettore (SharePoint, OneDrive, Google Drive, etc.).
/// I metadati raccolti includono nome, percorso, dimensione, data di modifica e tipo di contenuto.
/// </remarks>
public class ConnectorFileInfo
{
    /// <summary>
    /// Nome del file (senza percorso).
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Percorso completo o identificatore univoco del file nel repository.
    /// Il formato varia in base al tipo di connettore:
    /// - LocalFolder: percorso assoluto del file system
    /// - SharePoint/OneDrive: URL relativo al server
    /// - Google Drive: ID univoco del file
    /// </summary>
    public string Path { get; set; } = string.Empty;
    
    /// <summary>
    /// Dimensione del file in bytes.
    /// </summary>
    public long Size { get; set; }
    
    /// <summary>
    /// Data e ora dell'ultima modifica del file in formato UTC.
    /// Può essere null se il connettore non fornisce questa informazione.
    /// </summary>
    public DateTime? ModifiedDate { get; set; }
    
    /// <summary>
    /// Indica se l'elemento è una cartella (true) o un file (false).
    /// </summary>
    public bool IsFolder { get; set; }
    
    /// <summary>
    /// Tipo MIME del contenuto del file (es. "application/pdf", "text/plain").
    /// Può essere null se non determinabile dal connettore.
    /// </summary>
    public string? ContentType { get; set; }
}
