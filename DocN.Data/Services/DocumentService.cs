using DocN.Data.Models;
using DocN.Core.Interfaces;
using DocN.Data.Utilities;
using DocN.Data.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DocN.Data.Services;

/// <summary>
/// Informazioni sulle condivisioni di un documento
/// </summary>
public class DocumentShareInfo
{
    public List<UserShareInfo> UserShares { get; set; } = new();
    public List<GroupShareInfo> GroupShares { get; set; } = new();
}

/// <summary>
/// Informazioni su una condivisione con un utente
/// </summary>
public class UserShareInfo
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? UserEmail { get; set; }
    public DocumentPermission Permission { get; set; }
    public DateTime SharedAt { get; set; }
}

/// <summary>
/// Informazioni su una condivisione con un gruppo
/// </summary>
public class GroupShareInfo
{
    public int GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public int MemberCount { get; set; }
    public DocumentPermission Permission { get; set; }
    public DateTime SharedAt { get; set; }
}

/// <summary>
/// Interfaccia per il servizio di gestione documenti.
/// Fornisce operazioni CRUD, condivisione, controllo accessi e gestione visibilità.
/// </summary>
public interface IDocumentService
{
    /// <summary>
    /// Recupera un documento specifico se l'utente ha i permessi di accesso.
    /// </summary>
    /// <param name="documentId">ID del documento da recuperare</param>
    /// <param name="userId">ID dell'utente richiedente</param>
    /// <returns>Il documento se trovato e accessibile, altrimenti null</returns>
    Task<Document?> GetDocumentAsync(int documentId, string userId);
    
    /// <summary>
    /// Verifica se un utente ha i permessi per accedere a un documento.
    /// </summary>
    /// <param name="documentId">ID del documento</param>
    /// <param name="userId">ID dell'utente</param>
    /// <returns>True se l'utente può accedere al documento, altrimenti false</returns>
    Task<bool> CanUserAccessDocument(int documentId, string userId);
    
    /// <summary>
    /// Ottiene lista paginata di documenti accessibili all'utente.
    /// </summary>
    /// <param name="userId">ID dell'utente</param>
    /// <param name="page">Numero pagina (inizia da 1)</param>
    /// <param name="pageSize">Numero di documenti per pagina</param>
    /// <returns>Lista documenti accessibili all'utente</returns>
    Task<List<Document>> GetUserDocumentsAsync(string userId, int page = 1, int pageSize = 20);
    
    /// <summary>
    /// Conta il numero totale di documenti accessibili all'utente.
    /// </summary>
    /// <param name="userId">ID dell'utente</param>
    /// <returns>Numero totale di documenti accessibili</returns>
    Task<int> GetTotalDocumentCountAsync(string userId);
    
    /// <summary>
    /// Scarica il file di un documento se l'utente ha i permessi.
    /// </summary>
    /// <param name="documentId">ID del documento</param>
    /// <param name="userId">ID dell'utente</param>
    /// <returns>Array di byte del file se accessibile, altrimenti null</returns>
    Task<byte[]?> DownloadDocumentAsync(int documentId, string userId);
    
    /// <summary>
    /// Condivide un documento con un altro utente.
    /// </summary>
    /// <param name="documentId">ID del documento da condividere</param>
    /// <param name="shareWithUserId">ID dell'utente con cui condividere</param>
    /// <param name="permission">Livello di permesso da assegnare</param>
    /// <param name="currentUserId">ID dell'utente proprietario che condivide</param>
    /// <returns>True se condivisione riuscita, altrimenti false</returns>
    Task<bool> ShareDocumentAsync(int documentId, string shareWithUserId, DocumentPermission permission, string currentUserId);
    
    /// <summary>
    /// Condivide un documento con un gruppo di utenti.
    /// </summary>
    /// <param name="documentId">ID del documento da condividere</param>
    /// <param name="groupId">ID del gruppo con cui condividere</param>
    /// <param name="permission">Livello di permesso da assegnare</param>
    /// <param name="currentUserId">ID dell'utente proprietario che condivide</param>
    /// <returns>True se condivisione riuscita, altrimenti false</returns>
    Task<bool> ShareDocumentWithGroupAsync(int documentId, int groupId, DocumentPermission permission, string currentUserId);
    
    /// <summary>
    /// Rimuove la condivisione di un documento con un utente specifico.
    /// </summary>
    /// <param name="documentId">ID del documento</param>
    /// <param name="userId">ID dell'utente da rimuovere dalla condivisione</param>
    /// <param name="currentUserId">ID dell'utente proprietario</param>
    /// <returns>True se rimozione riuscita, altrimenti false</returns>
    Task<bool> RemoveUserShareAsync(int documentId, string userId, string currentUserId);
    
    /// <summary>
    /// Rimuove la condivisione di un documento con un gruppo.
    /// </summary>
    /// <param name="documentId">ID del documento</param>
    /// <param name="groupId">ID del gruppo da rimuovere dalla condivisione</param>
    /// <param name="currentUserId">ID dell'utente proprietario</param>
    /// <returns>True se rimozione riuscita, altrimenti false</returns>
    Task<bool> RemoveGroupShareAsync(int documentId, int groupId, string currentUserId);
    
    /// <summary>
    /// Ottiene tutte le condivisioni (utenti e gruppi) di un documento.
    /// </summary>
    /// <param name="documentId">ID del documento</param>
    /// <param name="userId">ID dell'utente richiedente (deve essere proprietario)</param>
    /// <returns>Oggetto con liste di condivisioni utenti e gruppi</returns>
    Task<DocumentShareInfo?> GetDocumentSharesAsync(int documentId, string userId);
    
    /// <summary>
    /// Aggiorna il livello di visibilità di un documento.
    /// </summary>
    /// <param name="documentId">ID del documento</param>
    /// <param name="visibility">Nuovo livello di visibilità</param>
    /// <param name="userId">ID dell'utente proprietario</param>
    /// <returns>True se aggiornamento riuscito, altrimenti false</returns>
    Task<bool> UpdateDocumentVisibilityAsync(int documentId, DocumentVisibility visibility, string userId);
    
    /// <summary>
    /// Crea un nuovo documento nel database con validazione embedding.
    /// </summary>
    /// <param name="document">Documento da creare</param>
    /// <param name="generateChunkEmbeddingsImmediately">Se true, genera embeddings chunks immediatamente; se false (default), li genera in background</param>
    /// <returns>Documento creato con ID assegnato</returns>
    Task<Document> CreateDocumentAsync(Document document, bool generateChunkEmbeddingsImmediately = false);
    
    /// <summary>
    /// Aggiorna un documento esistente se l'utente è il proprietario.
    /// </summary>
    /// <param name="document">Documento con dati aggiornati</param>
    /// <param name="userId">ID dell'utente richiedente</param>
    /// <returns>Documento aggiornato</returns>
    Task<Document> UpdateDocumentAsync(Document document, string userId);
    
    /// <summary>
    /// Salva relazioni di similarità tra documenti per raccomandazioni.
    /// </summary>
    /// <param name="sourceDocumentId">ID del documento sorgente</param>
    /// <param name="similarDocuments">Lista documenti simili con score</param>
    /// <returns>Task completato</returns>
    Task SaveSimilarDocumentsAsync(int sourceDocumentId, List<RelevantDocumentResult> similarDocuments);
    
    /// <summary>
    /// Genera embeddings per chunks di un documento in background (chiamato da BatchEmbeddingProcessor).
    /// </summary>
    /// <param name="documentId">ID del documento</param>
    /// <returns>Numero di chunks con embeddings generati con successo</returns>
    Task<int> GenerateChunkEmbeddingsForDocumentAsync(int documentId);
    
    /// <summary>
    /// Elimina un documento se l'utente è il proprietario.
    /// </summary>
    /// <param name="documentId">ID del documento da eliminare</param>
    /// <param name="userId">ID dell'utente richiedente (deve essere il proprietario)</param>
    /// <returns>True se eliminazione riuscita, false altrimenti</returns>
    Task<bool> DeleteDocumentAsync(int documentId, string userId);
    
    /// <summary>
    /// Elimina tutti i documenti dell'utente.
    /// </summary>
    /// <param name="userId">ID dell'utente richiedente</param>
    /// <returns>Numero di documenti eliminati</returns>
    Task<int> DeleteAllDocumentsAsync(string userId);
}

/// <summary>
/// Implementazione del servizio di gestione documenti.
/// Gestisce operazioni CRUD, controllo accessi multi-tenant, condivisione e visibilità.
/// </summary>
/// <remarks>
/// Scopo: Fornire layer business logic per operazioni su documenti con controlli di sicurezza.
/// Output: Documenti filtrati per permessi utente, operazioni CRUD sicure.
/// </remarks>
public class DocumentService : IDocumentService
{
    private readonly ApplicationDbContext _context;
    private readonly IChunkingService? _chunkingService;
    private readonly IMultiProviderAIService? _aiService;
    private readonly ILogger<DocumentService>? _logger;

    public DocumentService(
        ApplicationDbContext context, 
        IChunkingService? chunkingService = null,
        IMultiProviderAIService? aiService = null,
        ILogger<DocumentService>? logger = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _chunkingService = chunkingService;
        _aiService = aiService;
        _logger = logger;
    }

    /// <summary>
    /// Recupera un documento con tutte le relazioni (Owner, Shares, Tags) verificando i permessi di accesso.
    /// </summary>
    /// <param name="documentId">ID del documento da recuperare</param>
    /// <param name="userId">ID dell'utente richiedente per controllo accessi</param>
    /// <returns>Documento completo se accessibile, null se non trovato o non autorizzato</returns>
    /// <remarks>
    /// Scopo: Recuperare documento con controllo sicurezza integrato.
    /// Output: Document con navigazioni caricate, o null se inaccessibile.
    /// </remarks>
    public async Task<Document?> GetDocumentAsync(int documentId, string userId)
    {
        // OTTIMIZZAZIONE: AsNoTracking per query read-only
        var document = await _context.Documents
            .AsNoTracking()
            .Include(d => d.Owner)
            .Include(d => d.Shares)
            .Include(d => d.Tags)
            .FirstOrDefaultAsync(d => d.Id == documentId);

        if (document == null)
            return null;

        // Check access permissions
        if (!await CanUserAccessDocument(documentId, userId))
            return null;

        return document;
    }

    /// <summary>
    /// Verifica permessi accesso documento considerando: proprietà, visibilità, condivisioni (utente e gruppo) e multi-tenancy.
    /// </summary>
    /// <param name="documentId">ID del documento</param>
    /// <param name="userId">ID dell'utente da verificare</param>
    /// <returns>True se l'utente ha accesso, false altrimenti</returns>
    /// <remarks>
    /// Scopo: Controllo centralizzato permessi con logica multi-tenant.
    /// Logica: Owner → Public → Organization → Shared (Direct or via Group) → Denied
    /// Output: Boolean indicante permesso accesso.
    /// </remarks>
    public async Task<bool> CanUserAccessDocument(int documentId, string userId)
    {
        // OTTIMIZZAZIONE: AsNoTracking per query read-only di verifica permessi
        var document = await _context.Documents
            .AsNoTracking()
            .Include(d => d.Shares)
            .Include(d => d.GroupShares)
                .ThenInclude(gs => gs.Group)
                    .ThenInclude(g => g.Members)
            .FirstOrDefaultAsync(d => d.Id == documentId);

        if (document == null)
            return false;

        // Owner always has access (if document has an owner)
        if (!string.IsNullOrEmpty(document.OwnerId) && document.OwnerId == userId)
            return true;

        // Documents without owner are accessible based on visibility
        if (string.IsNullOrEmpty(document.OwnerId))
        {
            return document.Visibility == DocumentVisibility.Public || 
                   document.Visibility == DocumentVisibility.Organization;
        }

        // Check visibility settings
        if (document.Visibility == DocumentVisibility.Public)
            return true;

        if (document.Visibility == DocumentVisibility.Organization)
        {
            // Check if user is in the same organization/tenant as the document
            if (string.IsNullOrEmpty(userId))
                return false;
                
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return false;
                
            // Check if both document and user have a tenant and they match
            if (document.TenantId.HasValue && user.TenantId.HasValue)
            {
                return document.TenantId.Value == user.TenantId.Value;
            }
            
            // If document has no tenant, organization visibility is equivalent to public
            return !document.TenantId.HasValue;
        }

        // Check if document is shared with user directly
        if (document.Visibility == DocumentVisibility.Shared)
        {
            // Direct share with user
            if (document.Shares.Any(s => s.SharedWithUserId == userId))
                return true;
            
            // Share via group membership
            if (document.GroupShares.Any(gs => 
                gs.Group.Members.Any(m => m.UserId == userId)))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Recupera lista paginata documenti accessibili considerando ownership, condivisioni e tenant.
    /// </summary>
    /// <param name="userId">ID utente (può essere null per documenti pubblici)</param>
    /// <param name="page">Numero pagina (default 1, base 1)</param>
    /// <param name="pageSize">Elementi per pagina (default 20)</param>
    /// <returns>Lista documenti ordinata per data upload (più recenti prima)</returns>
    /// <remarks>
    /// Scopo: Lista documenti con filtro permessi automatico e paginazione.
    /// Filtri applicati: Owner OR Shared OR SameTenant OR Public
    /// Output: Lista Document con Include Owner e Tags.
    /// </remarks>
    public async Task<List<Document>> GetUserDocumentsAsync(string userId, int page = 1, int pageSize = 20)
    {
        // Get documents owned by user, shared with user, or in same tenant
        var query = _context.Documents.AsQueryable();
        
        if (string.IsNullOrEmpty(userId))
        {
            // If no user ID, return all public documents and documents without owner
            query = query.Where(d => d.Visibility == DocumentVisibility.Public || d.OwnerId == null);
        }
        else
        {
            // Get user's tenant
            var user = await _context.Users.FindAsync(userId);
            var userTenantId = user?.TenantId;
            
            // Get documents owned by user, shared with user, OR in the same tenant (if user has a tenant)
            query = _context.Documents.Where(d => 
                d.OwnerId == userId ||  // Owned by user
                d.OwnerId == null ||    // No owner (accessible to all in tenant)
                d.Shares.Any(s => s.SharedWithUserId == userId) ||  // Shared with user
                (userTenantId != null && d.TenantId == userTenantId) // Same tenant
            );
        }

        var allDocs = await query
            .OrderByDescending(d => d.UploadedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(d => d.Owner)
            .Include(d => d.Tags)
            .ToListAsync();

        return allDocs;
    }

    /// <summary>
    /// Conta documenti totali accessibili all'utente (per paginazione UI).
    /// </summary>
    /// <param name="userId">ID utente</param>
    /// <returns>Numero totale documenti accessibili</returns>
    /// <remarks>
    /// Scopo: Calcolare total count per paginazione mantenendo stessi filtri di GetUserDocumentsAsync.
    /// Output: Intero con count documenti.
    /// </remarks>
    public async Task<int> GetTotalDocumentCountAsync(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            // If no user ID, count all public documents and documents without owner
            return await _context.Documents.CountAsync(d => d.Visibility == DocumentVisibility.Public || d.OwnerId == null);
        }
        
        // Get user's tenant
        var user = await _context.Users.FindAsync(userId);
        var userTenantId = user?.TenantId;
        
        // Count documents owned by user, shared with user, OR in the same tenant
        return await _context.Documents.CountAsync(d => 
            d.OwnerId == userId ||  // Owned by user
            d.OwnerId == null ||    // No owner (accessible to all in tenant)
            d.Shares.Any(s => s.SharedWithUserId == userId) ||  // Shared with user
            (userTenantId != null && d.TenantId == userTenantId) // Same tenant
        );
    }

    /// <summary>
    /// Scarica file documento dal filesystem dopo verifica permessi.
    /// </summary>
    /// <param name="documentId">ID documento</param>
    /// <param name="userId">ID utente richiedente</param>
    /// <returns>Byte array del file se accessibile, null se negato o file non esiste</returns>
    /// <remarks>
    /// Scopo: Download sicuro file con controllo accessi.
    /// Verifica: Permessi utente + esistenza file su disco
    /// Output: Byte[] del file o null.
    /// </remarks>
    public async Task<byte[]?> DownloadDocumentAsync(int documentId, string userId)
    {
        try
        {
            // Check if user has access
            if (!await CanUserAccessDocument(documentId, userId))
            {
                _logger?.LogWarning("User {UserId} attempted to download document {DocumentId} without permission", 
                    userId ?? "anonymous", documentId);
                return null;
            }

            var document = await _context.Documents.FindAsync(documentId);
            if (document == null)
            {
                _logger?.LogWarning("Document {DocumentId} not found for download", documentId);
                return null;
            }

            if (string.IsNullOrEmpty(document.FilePath) || !File.Exists(document.FilePath))
            {
                _logger?.LogWarning("File not found on disk for document {DocumentId}: {FilePath}", 
                    documentId, document.FilePath ?? "null");
                return null;
            }

            var fileBytes = await File.ReadAllBytesAsync(document.FilePath);
            _logger?.LogInformation("User {UserId} downloaded document {DocumentId} ({FileName}), size: {Size} bytes", 
                userId, documentId, document.FileName, fileBytes.Length);
            
            return fileBytes;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error downloading document {DocumentId} for user {UserId}", documentId, userId);
            return null;
        }
    }

    /// <summary>
    /// Condivide documento con altro utente creando o aggiornando DocumentShare.
    /// </summary>
    /// <param name="documentId">ID documento da condividere</param>
    /// <param name="shareWithUserId">ID utente destinatario</param>
    /// <param name="permission">Permesso da assegnare (Read, Write, Admin)</param>
    /// <param name="currentUserId">ID proprietario che condivide</param>
    /// <returns>True se condivisione riuscita, false se negata (solo owner può condividere)</returns>
    /// <remarks>
    /// Scopo: Gestire condivisione granulare documenti.
    /// Logica: Solo owner può condividere, aggiorna visibilità a Shared se era Private.
    /// Output: Boolean successo operazione.
    /// </remarks>
    public async Task<bool> ShareDocumentAsync(int documentId, string shareWithUserId, DocumentPermission permission, string currentUserId)
    {
        var document = await _context.Documents.FindAsync(documentId);
        
        if (document == null)
            return false;

        // Only owner can share (documents without owner cannot be shared)
        if (string.IsNullOrEmpty(document.OwnerId) || document.OwnerId != currentUserId)
            return false;

        // Check if already shared
        var existingShare = await _context.DocumentShares
            .FirstOrDefaultAsync(s => s.DocumentId == documentId && s.SharedWithUserId == shareWithUserId);

        if (existingShare != null)
        {
            existingShare.Permission = permission;
        }
        else
        {
            var share = new DocumentShare
            {
                DocumentId = documentId,
                SharedWithUserId = shareWithUserId,
                Permission = permission,
                SharedByUserId = currentUserId,
                SharedAt = DateTime.UtcNow
            };
            _context.DocumentShares.Add(share);
        }

        // Update document visibility to Shared if it's Private
        if (document.Visibility == DocumentVisibility.Private)
        {
            document.Visibility = DocumentVisibility.Shared;
        }

        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Condivide documento con un gruppo di utenti creando o aggiornando DocumentGroupShare.
    /// </summary>
    /// <param name="documentId">ID documento da condividere</param>
    /// <param name="groupId">ID gruppo destinatario</param>
    /// <param name="permission">Permesso da assegnare (Read, Write, Delete)</param>
    /// <param name="currentUserId">ID proprietario che condivide</param>
    /// <returns>True se condivisione riuscita, false se negata (solo owner può condividere)</returns>
    public async Task<bool> ShareDocumentWithGroupAsync(int documentId, int groupId, DocumentPermission permission, string currentUserId)
    {
        var document = await _context.Documents.FindAsync(documentId);
        
        if (document == null)
            return false;

        // Only owner can share (documents without owner cannot be shared)
        if (string.IsNullOrEmpty(document.OwnerId) || document.OwnerId != currentUserId)
            return false;

        // Verify group exists
        var group = await _context.UserGroups.FindAsync(groupId);
        if (group == null)
            return false;

        // Check if already shared
        var existingShare = await _context.DocumentGroupShares
            .FirstOrDefaultAsync(s => s.DocumentId == documentId && s.GroupId == groupId);

        if (existingShare != null)
        {
            existingShare.Permission = permission;
        }
        else
        {
            var share = new DocumentGroupShare
            {
                DocumentId = documentId,
                GroupId = groupId,
                Permission = permission,
                SharedByUserId = currentUserId,
                SharedAt = DateTime.UtcNow
            };
            _context.DocumentGroupShares.Add(share);
        }

        // Update document visibility to Shared if it's Private
        if (document.Visibility == DocumentVisibility.Private)
        {
            document.Visibility = DocumentVisibility.Shared;
        }

        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Rimuove la condivisione di un documento con un utente specifico.
    /// </summary>
    public async Task<bool> RemoveUserShareAsync(int documentId, string userId, string currentUserId)
    {
        var document = await _context.Documents.FindAsync(documentId);
        
        if (document == null)
            return false;

        // Only owner can remove shares
        if (string.IsNullOrEmpty(document.OwnerId) || document.OwnerId != currentUserId)
            return false;

        var share = await _context.DocumentShares
            .FirstOrDefaultAsync(s => s.DocumentId == documentId && s.SharedWithUserId == userId);

        if (share == null)
            return false;

        _context.DocumentShares.Remove(share);
        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Rimuove la condivisione di un documento con un gruppo.
    /// </summary>
    public async Task<bool> RemoveGroupShareAsync(int documentId, int groupId, string currentUserId)
    {
        var document = await _context.Documents.FindAsync(documentId);
        
        if (document == null)
            return false;

        // Only owner can remove shares
        if (string.IsNullOrEmpty(document.OwnerId) || document.OwnerId != currentUserId)
            return false;

        var share = await _context.DocumentGroupShares
            .FirstOrDefaultAsync(s => s.DocumentId == documentId && s.GroupId == groupId);

        if (share == null)
            return false;

        _context.DocumentGroupShares.Remove(share);
        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Ottiene tutte le condivisioni (utenti e gruppi) di un documento.
    /// </summary>
    public async Task<DocumentShareInfo?> GetDocumentSharesAsync(int documentId, string userId)
    {
        var document = await _context.Documents.FindAsync(documentId);
        
        if (document == null)
            return null;

        // Only owner can view shares
        if (string.IsNullOrEmpty(document.OwnerId) || document.OwnerId != userId)
            return null;

        // OTTIMIZZAZIONE: AsNoTracking per query read-only con projection
        var userShares = await _context.DocumentShares
            .AsNoTracking()
            .Where(s => s.DocumentId == documentId)
            .Include(s => s.SharedWithUser)
            .Select(s => new UserShareInfo
            {
                UserId = s.SharedWithUserId,
                UserName = s.SharedWithUser.UserName ?? string.Empty,
                UserEmail = s.SharedWithUser.Email,
                Permission = s.Permission,
                SharedAt = s.SharedAt
            })
            .ToListAsync();

        // OTTIMIZZAZIONE: AsNoTracking per query read-only con projection
        var groupShares = await _context.DocumentGroupShares
            .AsNoTracking()
            .Where(s => s.DocumentId == documentId)
            .Include(s => s.Group)
                .ThenInclude(g => g.Members)
            .Select(s => new GroupShareInfo
            {
                GroupId = s.GroupId,
                GroupName = s.Group.Name,
                MemberCount = s.Group.Members.Count,
                Permission = s.Permission,
                SharedAt = s.SharedAt
            })
            .ToListAsync();

        return new DocumentShareInfo
        {
            UserShares = userShares,
            GroupShares = groupShares
        };
    }

    /// <summary>
    /// Modifica livello visibilità documento (Private, Shared, Organization, Public).
    /// </summary>
    /// <param name="documentId">ID documento</param>
    /// <param name="visibility">Nuovo livello visibilità</param>
    /// <param name="userId">ID utente (deve essere owner)</param>
    /// <returns>True se aggiornamento riuscito, false se negato</returns>
    /// <remarks>
    /// Scopo: Permettere a owner di cambiare visibilità documento.
    /// Restrizione: Solo owner può modificare.
    /// Output: Boolean successo operazione.
    /// </remarks>
    public async Task<bool> UpdateDocumentVisibilityAsync(int documentId, DocumentVisibility visibility, string userId)
    {
        var document = await _context.Documents.FindAsync(documentId);
        
        if (document == null)
            return false;

        // Only owner can change visibility (documents without owner cannot have visibility changed)
        if (string.IsNullOrEmpty(document.OwnerId) || document.OwnerId != userId)
            return false;

        document.Visibility = visibility;
        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Crea nuovo documento con validazione dimensioni embedding vettoriale e gestione chunks.
    /// </summary>
    /// <param name="document">Documento da creare con metadata ed embedding</param>
    /// <param name="generateChunkEmbeddingsImmediately">Se true, genera embeddings chunks immediatamente; se false (default), li genera in background</param>
    /// <returns>Documento creato con ID assegnato</returns>
    /// <exception cref="InvalidOperationException">Se embedding ha dimensioni errate o salvataggio DB fallisce</exception>
    /// <remarks>
    /// Scopo: Inserire documento validando embedding per evitare errori VECTOR SQL Server.
    /// Validazione: Verifica 768 o 1536 dimensioni prima di save.
    /// Output: Document con Id popolato, exception se errore validazione o DB.
    /// </remarks>
    public async Task<Document> CreateDocumentAsync(Document document, bool generateChunkEmbeddingsImmediately = false)
    {
        // Use execution strategy to handle all operations as a retriable unit
        var strategy = _context.Database.CreateExecutionStrategy();
        
        return await strategy.ExecuteAsync(async () =>
        {
            try
            {
                // Detach any existing tracked entities to avoid conflicts on retry
                var trackedEntries = _context.ChangeTracker.Entries<Document>()
                    .Where(e => e.State != EntityState.Detached)
                    .ToList();
                foreach (var entry in trackedEntries)
                {
                    entry.State = EntityState.Detached;
                }
                
                // Determine which embedding field is populated and validate
                float[]? embeddingToValidate = null;
                
                if (document.EmbeddingVector768 != null && document.EmbeddingVector768.Length > 0)
                {
                    embeddingToValidate = document.EmbeddingVector768;
                    document.EmbeddingDimension = 768;
                }
                else if (document.EmbeddingVector1536 != null && document.EmbeddingVector1536.Length > 0)
                {
                    embeddingToValidate = document.EmbeddingVector1536;
                    document.EmbeddingDimension = 1536;
                }
                
                // Validate embedding dimensions before saving to avoid database errors
                EmbeddingValidationHelper.ValidateEmbeddingDimensions(embeddingToValidate);
                
                _context.Documents.Add(document);
                await _context.SaveChangesAsync();
                
                // Create chunks with or without embeddings based on parameter
                if (_chunkingService != null && !string.IsNullOrEmpty(document.ExtractedText))
                {
                    if (generateChunkEmbeddingsImmediately && _aiService != null)
                    {
                        // SLOW PATH: Create chunks with embeddings immediately (complete but slower)
                        var chunks = _chunkingService.ChunkDocument(document);
                        if (chunks.Any())
                        {
                            _logger?.LogInformation("Creating {ChunkCount} chunks WITH embeddings for document {Id} (immediate generation)", chunks.Count, document.Id);
                            document.ChunkEmbeddingStatus = ChunkEmbeddingStatus.Processing;
                            await _context.SaveChangesAsync();
                            
                            var embeddedCount = await GenerateChunkEmbeddingsAsync(chunks, document.Id);
                            _logger?.LogInformation("Created {ChunkCount} chunks for document {Id}, {EmbeddedCount} with embeddings", 
                                chunks.Count, document.Id, embeddedCount);
                            
                            _context.DocumentChunks.AddRange(chunks);
                            document.ChunkEmbeddingStatus = ChunkEmbeddingStatus.Completed;
                            await _context.SaveChangesAsync();
                            
                            _logger?.LogInformation("Saved {ChunkCount} chunks with embeddings for document {Id}", chunks.Count, document.Id);
                        }
                        else
                        {
                            document.ChunkEmbeddingStatus = ChunkEmbeddingStatus.NotRequired;
                            await _context.SaveChangesAsync();
                        }
                    }
                    else
                    {
                        // FAST PATH: Just mark as pending - background processor will create chunks AND embeddings later
                        _logger?.LogInformation("Marking document {Id} for background chunk generation (no chunks created during upload for speed)", document.Id);
                        document.ChunkEmbeddingStatus = ChunkEmbeddingStatus.Pending;
                        await _context.SaveChangesAsync();
                    }
                }
                else
                {
                    document.ChunkEmbeddingStatus = ChunkEmbeddingStatus.NotRequired;
                    await _context.SaveChangesAsync();
                }
                
                return document;
            }
            catch (DbUpdateException ex)
            {
                // Extract the inner exception details for better error reporting
                var innerMessage = ex.InnerException?.Message ?? ex.Message;
                
                // Check for vector dimension mismatch error
                if (EmbeddingValidationHelper.IsVectorDimensionMismatchError(innerMessage))
                {
                    var embeddingDim = document.EmbeddingDimension ?? 0;
                    throw new InvalidOperationException(
                        EmbeddingValidationHelper.CreateDimensionMismatchErrorMessage(embeddingDim, innerMessage),
                        ex);
                }
                
                throw new InvalidOperationException($"Database save failed: {innerMessage}", ex);
            }
        });
    }

    /// <summary>
    /// Aggiorna documento esistente con controllo ownership e validazione embedding.
    /// </summary>
    /// <param name="document">Documento con dati aggiornati</param>
    /// <param name="userId">ID utente richiedente (deve essere owner)</param>
    /// <returns>Documento aggiornato</returns>
    /// <exception cref="InvalidOperationException">Se documento non trovato</exception>
    /// <exception cref="UnauthorizedAccessException">Se utente non è owner</exception>
    /// <remarks>
    /// Scopo: Update sicuro con validazione owner e embedding dimensions.
    /// Logica: Carica documento esistente, verifica owner, aggiorna proprietà, valida embedding.
    /// Output: Document aggiornato con tutte proprietà sincronizzate.
    /// </remarks>
    public async Task<Document> UpdateDocumentAsync(Document document, string userId)
    {
        var existingDocument = await _context.Documents
            .Include(d => d.Tags)
            .FirstOrDefaultAsync(d => d.Id == document.Id);

        if (existingDocument == null)
            throw new InvalidOperationException($"Document with ID {document.Id} not found");

        // Only owner can update (documents without owner cannot be updated)
        if (string.IsNullOrEmpty(existingDocument.OwnerId) || existingDocument.OwnerId != userId)
            throw new UnauthorizedAccessException("Only the document owner can update this document");

        // Determine which embedding field is populated and validate
        float[]? embeddingToValidate = null;
        
        if (document.EmbeddingVector768 != null && document.EmbeddingVector768.Length > 0)
        {
            embeddingToValidate = document.EmbeddingVector768;
            document.EmbeddingDimension = 768;
        }
        else if (document.EmbeddingVector1536 != null && document.EmbeddingVector1536.Length > 0)
        {
            embeddingToValidate = document.EmbeddingVector1536;
            document.EmbeddingDimension = 1536;
        }
        
        // Validate embedding dimensions before updating
        EmbeddingValidationHelper.ValidateEmbeddingDimensions(embeddingToValidate);

        // Update document properties
        existingDocument.FileName = document.FileName;
        existingDocument.FilePath = document.FilePath;
        existingDocument.ContentType = document.ContentType;
        existingDocument.FileSize = document.FileSize;
        existingDocument.ExtractedText = document.ExtractedText;
        existingDocument.SuggestedCategory = document.SuggestedCategory;
        existingDocument.CategoryReasoning = document.CategoryReasoning;
        existingDocument.ActualCategory = document.ActualCategory;
        existingDocument.Visibility = document.Visibility;
        existingDocument.EmbeddingVector768 = document.EmbeddingVector768;
        existingDocument.EmbeddingVector1536 = document.EmbeddingVector1536;
        existingDocument.EmbeddingDimension = document.EmbeddingDimension;
        existingDocument.Notes = document.Notes;
        existingDocument.PageCount = document.PageCount;
        existingDocument.DetectedLanguage = document.DetectedLanguage;
        existingDocument.ProcessingStatus = document.ProcessingStatus;
        existingDocument.ProcessingError = document.ProcessingError;
        existingDocument.AIAnalysisDate = document.AIAnalysisDate;
        existingDocument.AITagsJson = document.AITagsJson;
        existingDocument.ExtractedMetadataJson = document.ExtractedMetadataJson;

        // Update tags
        if (existingDocument.Tags != null)
        {
            existingDocument.Tags.Clear();
        
            if (document.Tags != null)
            {
                foreach (var tag in document.Tags)
                {
                    if (!string.IsNullOrWhiteSpace(tag?.Name))
                    {
                        existingDocument.Tags.Add(new DocumentTag
                        {
                            Name = tag.Name,
                            Document = existingDocument
                        });
                    }
                }
            }
        }

        try
        {
            await _context.SaveChangesAsync();
            return existingDocument;
        }
        catch (DbUpdateException ex)
        {
            // Extract the inner exception details for better error reporting
            var innerMessage = ex.InnerException?.Message ?? ex.Message;
            
            // Check for vector dimension mismatch error
            if (EmbeddingValidationHelper.IsVectorDimensionMismatchError(innerMessage))
            {
                var embeddingDim = document.EmbeddingVector?.Length ?? 0;
                throw new InvalidOperationException(
                    EmbeddingValidationHelper.CreateDimensionMismatchErrorMessage(embeddingDim, innerMessage),
                    ex);
            }
            
            throw new InvalidOperationException($"Database save failed: {innerMessage}", ex);
        }
    }

    /// <summary>
    /// Salva relazioni di similarità per documento (max 5 documenti simili).
    /// </summary>
    /// <param name="sourceDocumentId">ID documento sorgente</param>
    /// <param name="similarDocuments">Lista documenti simili ordinati per score</param>
    /// <returns>Task completato</returns>
    /// <remarks>
    /// Scopo: Memorizzare documenti semanticamente simili per raccomandazioni.
    /// Logica: Rimuove vecchie relazioni, salva top 5 nuove con ranking.
    /// Output: Tabella SimilarDocuments popolata per suggerimenti "Documenti correlati".
    /// </remarks>
    public async Task SaveSimilarDocumentsAsync(int sourceDocumentId, List<RelevantDocumentResult> similarDocuments)
    {
        if (similarDocuments == null || similarDocuments.Count == 0)
            return;

        // Remove any existing similar document relationships for this source document
        var existingRelationships = await _context.SimilarDocuments
            .Where(sd => sd.SourceDocumentId == sourceDocumentId)
            .ToListAsync();
        
        if (existingRelationships.Any())
        {
            _context.SimilarDocuments.RemoveRange(existingRelationships);
        }

        // Add new similar document relationships
        const int MaxSimilarDocuments = 5;
        int rank = 1;
        foreach (var similar in similarDocuments.Take(MaxSimilarDocuments))
        {
            var similarDoc = new SimilarDocument
            {
                SourceDocumentId = sourceDocumentId,
                SimilarDocumentId = similar.DocumentId,
                SimilarityScore = similar.SimilarityScore,
                RelevantChunk = similar.RelevantChunk,
                ChunkIndex = similar.ChunkIndex,
                AnalyzedAt = DateTime.UtcNow,
                Rank = rank++
            };
            
            _context.SimilarDocuments.Add(similarDoc);
        }

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Helper method to generate embeddings for a list of document chunks
    /// Uses sequential processing with batching to avoid API rate limits and timeouts
    /// </summary>
    /// <param name="chunks">List of chunks to generate embeddings for</param>
    /// <param name="documentId">Document ID for logging purposes</param>
    /// <param name="maxConcurrency">Maximum number of concurrent embedding requests (default: 1 for reliability)</param>
    /// <param name="batchSize">Number of chunks to process in each batch (default: 10 for better progress feedback)</param>
    /// <returns>Number of chunks that successfully got embeddings</returns>
    private async Task<int> GenerateChunkEmbeddingsAsync(List<DocumentChunk> chunks, int documentId, int maxConcurrency = 1, int batchSize = 10)
    {
        if (_aiService == null)
            return 0;
            
        var successCount = 0;
        var totalBatches = (int)Math.Ceiling(chunks.Count / (double)batchSize);
        var currentBatch = 0;
        
        // Reuse semaphore across all batches for efficiency
        using var semaphore = new System.Threading.SemaphoreSlim(maxConcurrency);
        
        // Process chunks in batches to avoid memory pressure with large documents
        for (int i = 0; i < chunks.Count; i += batchSize)
        {
            currentBatch++;
            var batch = chunks.Skip(i).Take(batchSize).ToList();
            
            _logger?.LogInformation("Processing batch {CurrentBatch}/{TotalBatches} ({ChunkCount} chunks) for document {DocumentId}", 
                currentBatch, totalBatches, batch.Count, documentId);
            
            var tasks = new List<Task<bool>>();
            
            foreach (var chunk in batch)
            {
                tasks.Add(GenerateSingleChunkEmbeddingAsync(chunk, documentId, semaphore));
            }
            
            var results = await Task.WhenAll(tasks);
            var batchSuccess = results.Count(r => r);
            successCount += batchSuccess;
            
            _logger?.LogInformation("Batch {CurrentBatch}/{TotalBatches} completed: {Success}/{Total} chunks embedded successfully", 
                currentBatch, totalBatches, batchSuccess, batch.Count);
        }
        
        return successCount;
    }

    /// <summary>
    /// Generate embedding for a single chunk with semaphore-controlled concurrency
    /// </summary>
    /// <param name="chunk">The document chunk to generate embedding for</param>
    /// <param name="documentId">Document ID for logging purposes</param>
    /// <param name="semaphore">Semaphore to control concurrency across multiple chunk operations</param>
    /// <returns>True if embedding was successfully generated, false otherwise</returns>
    private async Task<bool> GenerateSingleChunkEmbeddingAsync(DocumentChunk chunk, int documentId, System.Threading.SemaphoreSlim semaphore)
    {
        if (_aiService == null)
            return false;
            
        await semaphore.WaitAsync();
        try
        {
            var chunkEmbedding = await _aiService.GenerateEmbeddingAsync(chunk.ChunkText);
            if (chunkEmbedding != null)
            {
                // ChunkEmbedding setter automatically sets EmbeddingDimension
                chunk.ChunkEmbedding = chunkEmbedding;
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to generate embedding for chunk {ChunkIndex} of document {DocumentId}", 
                chunk.ChunkIndex, documentId);
            return false;
        }
        finally
        {
            semaphore.Release();
        }
    }
    
    /// <summary>
    /// Genera embeddings per tutti i chunks di un documento che non hanno ancora embeddings.
    /// Metodo pubblico utilizzato da BatchEmbeddingProcessor per processing in background.
    /// </summary>
    /// <param name="documentId">ID del documento</param>
    /// <returns>Numero di chunks con embeddings generati con successo</returns>
    public async Task<int> GenerateChunkEmbeddingsForDocumentAsync(int documentId)
    {
        if (_aiService == null)
        {
            _logger?.LogWarning("Cannot generate chunk embeddings for document {DocumentId}: AI service not available", documentId);
            return 0;
        }
        
        // Get document to update status
        var document = await _context.Documents.FindAsync(documentId);
        if (document == null)
        {
            _logger?.LogWarning("Document {DocumentId} not found", documentId);
            return 0;
        }
        
        // Get all chunks for this document that don't have embeddings
        var chunksWithoutEmbeddings = await _context.DocumentChunks
            .Where(c => c.DocumentId == documentId && 
                        c.ChunkEmbedding768 == null && 
                        c.ChunkEmbedding1536 == null)
            .ToListAsync();
        
        if (!chunksWithoutEmbeddings.Any())
        {
            _logger?.LogInformation("No chunks without embeddings found for document {DocumentId}", documentId);
            document.ChunkEmbeddingStatus = ChunkEmbeddingStatus.Completed;
            await _context.SaveChangesAsync();
            return 0;
        }
        
        _logger?.LogInformation("Generating embeddings for {ChunkCount} chunks of document {DocumentId}", 
            chunksWithoutEmbeddings.Count, documentId);
        
        // Update status to Processing
        document.ChunkEmbeddingStatus = ChunkEmbeddingStatus.Processing;
        await _context.SaveChangesAsync();
        
        // Generate embeddings with batching and controlled concurrency
        var successCount = await GenerateChunkEmbeddingsAsync(chunksWithoutEmbeddings, documentId);
        
        // Ensure chunks are marked as modified so their embeddings are saved
        foreach (var chunk in chunksWithoutEmbeddings.Where(c => c.ChunkEmbedding != null))
        {
            _context.Entry(chunk).State = EntityState.Modified;
        }
        
        // Save all chunks with their new embeddings and update document status
        document.ChunkEmbeddingStatus = ChunkEmbeddingStatus.Completed;
        await _context.SaveChangesAsync();
        
        _logger?.LogInformation("Generated embeddings for {SuccessCount}/{TotalCount} chunks of document {DocumentId} - Status: Completed", 
            successCount, chunksWithoutEmbeddings.Count, documentId);
        
        return successCount;
    }
    
    /// <summary>
    /// Elimina un documento se l'utente è il proprietario.
    /// </summary>
    /// <param name="documentId">ID del documento da eliminare</param>
    /// <param name="userId">ID dell'utente richiedente</param>
    /// <returns>True se eliminazione riuscita, false se negato o errore</returns>
    /// <remarks>
    /// Scopo: Eliminare documento con controllo ownership.
    /// Logica: Solo owner può eliminare, rimuove document + chunks + similar documents + file fisico.
    /// Output: Boolean successo operazione.
    /// </remarks>
    public async Task<bool> DeleteDocumentAsync(int documentId, string userId)
    {
        try
        {
            var document = await _context.Documents.FindAsync(documentId);
            
            if (document == null)
                return false;

            // SECURITY CHECK: Only document owner can delete
            if (!string.IsNullOrEmpty(document.OwnerId))
            {
                // Document has an owner - check authorization
                if (string.IsNullOrEmpty(userId) || document.OwnerId != userId)
                {
                    _logger?.LogWarning("User {UserId} attempted to delete document {DocumentId} owned by {OwnerId}", 
                        userId ?? "anonymous", documentId, document.OwnerId);
                    return false;
                }
            }
            else
            {
                // Document has no owner - could be legacy data or system-created
                // Only allow deletion if user is authenticated (basic check)
                if (string.IsNullOrEmpty(userId))
                {
                    _logger?.LogWarning("Anonymous user attempted to delete document {DocumentId} with no owner", documentId);
                    return false;
                }
            }

            // Delete associated chunks first
            var chunks = await _context.DocumentChunks
                .Where(c => c.DocumentId == documentId)
                .ToListAsync();
            
            if (chunks.Any())
            {
                _context.DocumentChunks.RemoveRange(chunks);
            }

            // Delete associated similar documents relationships
            var similarDocuments = await _context.SimilarDocuments
                .Where(sd => sd.SourceDocumentId == documentId || sd.SimilarDocumentId == documentId)
                .ToListAsync();
            
            if (similarDocuments.Any())
            {
                _context.SimilarDocuments.RemoveRange(similarDocuments);
            }

            // Delete the document
            _context.Documents.Remove(document);
            await _context.SaveChangesAsync();

            // Try to delete the physical file (if it exists)
            if (!string.IsNullOrEmpty(document.FilePath) && File.Exists(document.FilePath))
            {
                try
                {
                    File.Delete(document.FilePath);
                    _logger?.LogInformation("Deleted physical file for document {DocumentId}: {FilePath}", 
                        documentId, document.FilePath);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Could not delete physical file for document {DocumentId}: {FilePath}", 
                        documentId, document.FilePath);
                    // Continue even if file deletion fails - document is already removed from DB
                }
            }

            _logger?.LogInformation("User {UserId} deleted document {DocumentId} - {FileName}", 
                userId, documentId, document.FileName);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error deleting document {DocumentId}", documentId);
            return false;
        }
    }

    public async Task<int> DeleteAllDocumentsAsync(string userId)
    {
        try
        {
            if (string.IsNullOrEmpty(userId))
            {
                _logger?.LogWarning("Anonymous user attempted to delete all documents");
                return 0;
            }

            // Get all documents owned by the current user
            var userDocuments = await _context.Documents
                .Where(d => d.OwnerId == userId)
                .ToListAsync();

            if (!userDocuments.Any())
            {
                _logger?.LogInformation("User {UserId} attempted to delete all documents but has none", userId);
                return 0;
            }

            var documentIds = userDocuments.Select(d => d.Id).ToList();
            var deletedCount = userDocuments.Count;

            // Delete associated chunks for all documents
            var chunks = await _context.DocumentChunks
                .Where(c => documentIds.Contains(c.DocumentId))
                .ToListAsync();
            
            if (chunks.Any())
            {
                _context.DocumentChunks.RemoveRange(chunks);
            }

            // Delete associated similar documents relationships
            var similarDocuments = await _context.SimilarDocuments
                .Where(sd => documentIds.Contains(sd.SourceDocumentId) || documentIds.Contains(sd.SimilarDocumentId))
                .ToListAsync();
            
            if (similarDocuments.Any())
            {
                _context.SimilarDocuments.RemoveRange(similarDocuments);
            }

            // Delete all user documents
            _context.Documents.RemoveRange(userDocuments);
            await _context.SaveChangesAsync();

            // Try to delete physical files
            foreach (var document in userDocuments)
            {
                if (!string.IsNullOrEmpty(document.FilePath) && File.Exists(document.FilePath))
                {
                    try
                    {
                        File.Delete(document.FilePath);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Could not delete physical file for document {DocumentId}: {FilePath}", 
                            document.Id, document.FilePath);
                    }
                }
            }

            _logger?.LogInformation("User {UserId} deleted all {Count} documents", userId, deletedCount);
            return deletedCount;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error deleting all documents for user {UserId}", userId);
            return 0;
        }
    }
}
