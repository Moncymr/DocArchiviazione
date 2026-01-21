using DocN.Data;
using DocN.Data.Models;
using DocN.Data.Services.Connectors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DocN.Data.Services;

/// <summary>
/// Servizio per gestione connettori documenti esterni (Google Drive, SharePoint, Dropbox, etc.)
/// </summary>
/// <remarks>
/// <para><strong>Scopo:</strong> Fornire integrazione con sistemi documentali esterni per import automatico documenti</para>
/// 
/// <para><strong>Funzionalità chiave:</strong></para>
/// <list type="bullet">
/// <item><description>Gestione configurazione connettori (tipo, credenziali criptate, opzioni)</description></item>
/// <item><description>Test connessione per validazione credenziali</description></item>
/// <item><description>Sincronizzazione documenti automatica/manuale</description></item>
/// <item><description>Multi-tenancy con isolamento per OwnerId/TenantId</description></item>
/// <item><description>Factory pattern per handler specifici per tipo connettore</description></item>
/// </list>
/// 
/// <para><strong>Connettori supportati:</strong></para>
/// <list type="bullet">
/// <item><description>GoogleDrive - Google Drive API v3</description></item>
/// <item><description>SharePoint - Microsoft Graph API</description></item>
/// <item><description>Dropbox - Dropbox API v2</description></item>
/// <item><description>OneDrive - Microsoft Graph API</description></item>
/// <item><description>FTP/SFTP - File system remoti</description></item>
/// <item><description>WebDAV - Storage WebDAV generico</description></item>
/// </list>
/// 
/// <para><strong>Sicurezza:</strong></para>
/// <list type="bullet">
/// <item><description>Credenziali sempre criptate in database (EncryptedCredentials)</description></item>
/// <item><description>Isolamento tenant (filtro su OwnerId/TenantId)</description></item>
/// <item><description>OAuth 2.0 per Google Drive, SharePoint, OneDrive</description></item>
/// </list>
/// 
/// <para><strong>Ottimizzazioni:</strong></para>
/// <list type="bullet">
/// <item><description>AsNoTracking() su tutte le query read-only (performance)</description></item>
/// <item><description>Select projection per evitare caricare dati non necessari</description></item>
/// <item><description>ConnectorHandlerFactory per dependency injection handlers specifici</description></item>
/// </list>
/// </remarks>
public class ConnectorService : IConnectorService
{
    private readonly DocArcContext _context;
    private readonly ILogger<ConnectorService> _logger;
    private readonly ConnectorHandlerFactory _handlerFactory;

    /// <summary>
    /// Inizializza una nuova istanza del servizio connettori
    /// </summary>
    /// <param name="context">Database context per accesso connettori</param>
    /// <param name="logger">Logger per diagnostica</param>
    public ConnectorService(DocArcContext context, ILogger<ConnectorService> logger)
    {
        _context = context;
        _logger = logger;
        _handlerFactory = new ConnectorHandlerFactory(logger);
    }

    /// <summary>
    /// Ottiene tutti i connettori dell'utente
    /// </summary>
    /// <param name="userId">ID utente proprietario connettori</param>
    /// <returns>Lista connettori dell'utente ordinati per data creazione (più recenti prima)</returns>
    /// <remarks>
    /// <para><strong>Query optimization:</strong></para>
    /// <list type="bullet">
    /// <item><description>AsNoTracking() - Read-only query, no change tracking overhead</description></item>
    /// <item><description>Select projection - Carica solo campi necessari per DTO</description></item>
    /// <item><description>Filtro OwnerId - Isolamento tenant sicuro</description></item>
    /// </list>
    /// </remarks>
    public async Task<List<DocumentConnector>> GetUserConnectorsAsync(string userId)
    {
        try
        {
            return await _context.DocumentConnectors
                .AsNoTracking()
                .Where(c => c.OwnerId == userId)
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => new DocumentConnector
                {
                    Id = c.Id,
                    Name = c.Name,
                    ConnectorType = c.ConnectorType,
                    Configuration = c.Configuration,
                    EncryptedCredentials = c.EncryptedCredentials,
                    IsActive = c.IsActive,
                    LastConnectionTest = c.LastConnectionTest,
                    LastConnectionTestResult = c.LastConnectionTestResult,
                    LastSyncedAt = c.LastSyncedAt,
                    OwnerId = c.OwnerId,
                    TenantId = c.TenantId,
                    CreatedAt = c.CreatedAt,
                    UpdatedAt = c.UpdatedAt,
                    Description = c.Description
                })
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving connectors for user {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Ottiene un connettore specifico per ID
    /// </summary>
    /// <param name="connectorId">ID connettore da recuperare</param>
    /// <param name="userId">ID utente proprietario (per security check)</param>
    /// <returns>Connettore trovato o null se non esiste/non autorizzato</returns>
    /// <remarks>
    /// <para><strong>Security:</strong> Verifica che l'utente sia proprietario del connettore (OwnerId check)</para>
    /// <para><strong>Optimization:</strong> AsNoTracking() + Select projection per performance</para>
    /// </remarks>
    public async Task<DocumentConnector?> GetConnectorAsync(int connectorId, string userId)
    {
        try
        {
            return await _context.DocumentConnectors
                .AsNoTracking()
                .Select(c => new DocumentConnector
                {
                    Id = c.Id,
                    Name = c.Name,
                    ConnectorType = c.ConnectorType,
                    Configuration = c.Configuration,
                    EncryptedCredentials = c.EncryptedCredentials,
                    IsActive = c.IsActive,
                    LastConnectionTest = c.LastConnectionTest,
                    LastConnectionTestResult = c.LastConnectionTestResult,
                    LastSyncedAt = c.LastSyncedAt,
                    OwnerId = c.OwnerId,
                    TenantId = c.TenantId,
                    CreatedAt = c.CreatedAt,
                    UpdatedAt = c.UpdatedAt,
                    Description = c.Description
                })
                .FirstOrDefaultAsync(c => c.Id == connectorId && c.OwnerId == userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving connector {ConnectorId} for user {UserId}", connectorId, userId);
            throw;
        }
    }

    /// <summary>
    /// Crea un nuovo connettore per l'utente
    /// </summary>
    /// <param name="connector">Connettore da creare con configurazione e credenziali</param>
    /// <returns>Connettore creato con ID assegnato</returns>
    /// <exception cref="InvalidOperationException">Se la configurazione è invalida (es. contiene intero DocumentConnector invece che solo config specifiche)</exception>
    /// <remarks>
    /// <para><strong>Validazione configurazione:</strong> Verifica che Configuration contenga solo impostazioni specifiche
    /// del connettore e non l'intero oggetto DocumentConnector (errore comune)</para>
    /// 
    /// <para><strong>Sicurezza credenziali:</strong> Le credenziali devono essere già criptate prima di chiamare questo metodo.
    /// Il servizio non cripta automaticamente.</para>
    /// </remarks>
    public async Task<DocumentConnector> CreateConnectorAsync(DocumentConnector connector)
    {
        try
        {
            // Validate that configuration is not the entire DocumentConnector JSON
            if (!string.IsNullOrEmpty(connector.Configuration))
            {
                if (connector.Configuration.Contains("\"connectorType\"") || 
                    connector.Configuration.Contains("\"ConnectorType\"") ||
                    connector.Configuration.Contains("\"name\""))
                {
                    throw new ArgumentException("Invalid configuration: The 'configuration' field should contain only the connector-specific configuration JSON (e.g., {\"folderPath\":\"C:\\\\Path\"}), not the entire DocumentConnector object.");
                }
            }
            
            // Create a new entity with properly initialized navigation properties
            var newConnector = new DocumentConnector
            {
                Name = connector.Name,
                ConnectorType = connector.ConnectorType,
                Configuration = connector.Configuration,
                EncryptedCredentials = connector.EncryptedCredentials,
                IsActive = connector.IsActive,
                LastConnectionTest = connector.LastConnectionTest,
                LastConnectionTestResult = connector.LastConnectionTestResult,
                LastSyncedAt = connector.LastSyncedAt,
                OwnerId = connector.OwnerId,
                TenantId = connector.TenantId,
                Description = connector.Description,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                // Explicitly initialize the collection to avoid null reference issues
                IngestionSchedules = new List<IngestionSchedule>()
            };
            
            _context.DocumentConnectors.Add(newConnector);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Created connector {ConnectorId} for user {UserId}", newConnector.Id, newConnector.OwnerId);
            
            // Return a clean copy without navigation properties
            return new DocumentConnector
            {
                Id = newConnector.Id,
                Name = newConnector.Name,
                ConnectorType = newConnector.ConnectorType,
                Configuration = newConnector.Configuration,
                EncryptedCredentials = newConnector.EncryptedCredentials,
                IsActive = newConnector.IsActive,
                LastConnectionTest = newConnector.LastConnectionTest,
                LastConnectionTestResult = newConnector.LastConnectionTestResult,
                LastSyncedAt = newConnector.LastSyncedAt,
                OwnerId = newConnector.OwnerId,
                TenantId = newConnector.TenantId,
                CreatedAt = newConnector.CreatedAt,
                UpdatedAt = newConnector.UpdatedAt,
                Description = newConnector.Description
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating connector for user {UserId}", connector.OwnerId);
            throw;
        }
    }

    public async Task<DocumentConnector> UpdateConnectorAsync(DocumentConnector connector, string userId)
    {
        try
        {
            var existing = await _context.DocumentConnectors
                .FirstOrDefaultAsync(c => c.Id == connector.Id && c.OwnerId == userId);
            
            if (existing == null)
            {
                throw new UnauthorizedAccessException("Connector not found or access denied");
            }
            
            existing.Name = connector.Name;
            existing.ConnectorType = connector.ConnectorType;
            existing.Configuration = connector.Configuration;
            existing.EncryptedCredentials = connector.EncryptedCredentials;
            existing.IsActive = connector.IsActive;
            existing.Description = connector.Description;
            existing.UpdatedAt = DateTime.UtcNow;
            
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Updated connector {ConnectorId}", connector.Id);
            return existing;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating connector {ConnectorId}", connector.Id);
            throw;
        }
    }

    public async Task<bool> DeleteConnectorAsync(int connectorId, string userId)
    {
        try
        {
            var connector = await _context.DocumentConnectors
                .FirstOrDefaultAsync(c => c.Id == connectorId && c.OwnerId == userId);
            
            if (connector == null)
            {
                return false;
            }
            
            _context.DocumentConnectors.Remove(connector);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Deleted connector {ConnectorId}", connectorId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting connector {ConnectorId}", connectorId);
            throw;
        }
    }

    public async Task<(bool success, string message)> TestConnectionAsync(int connectorId, string userId)
    {
        try
        {
            var connector = await GetConnectorAsync(connectorId, userId);
            if (connector == null)
            {
                return (false, "Connector not found");
            }
            
            // Use appropriate handler based on connector type
            var handler = _handlerFactory.CreateHandler(connector.ConnectorType);
            var result = await handler.TestConnectionAsync(connector.Configuration, connector.EncryptedCredentials);
            
            // Update connector with test results
            var existingConnector = await _context.DocumentConnectors
                .FirstOrDefaultAsync(c => c.Id == connectorId && c.OwnerId == userId);
            
            if (existingConnector != null)
            {
                existingConnector.LastConnectionTest = DateTime.UtcNow;
                existingConnector.LastConnectionTestResult = result.message;
                await _context.SaveChangesAsync();
            }
            
            _logger.LogInformation("Connection test {Status} for connector {ConnectorId}: {Message}", 
                result.success ? "successful" : "failed", connectorId, result.message);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing connection for connector {ConnectorId}", connectorId);
            return (false, $"Connection test failed: {ex.Message}");
        }
    }

    public async Task<List<ConnectorFileInfo>> ListFilesAsync(int connectorId, string userId, string? path = null)
    {
        try
        {
            var connector = await GetConnectorAsync(connectorId, userId);
            if (connector == null)
            {
                throw new UnauthorizedAccessException("Connector not found or access denied");
            }
            
            // Use appropriate handler based on connector type
            var handler = _handlerFactory.CreateHandler(connector.ConnectorType);
            var files = await handler.ListFilesAsync(connector.Configuration, connector.EncryptedCredentials, path);
            
            _logger.LogInformation("Listed {Count} files from connector {ConnectorId}", files.Count, connectorId);
            return files;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing files from connector {ConnectorId}", connectorId);
            throw;
        }
    }
}
