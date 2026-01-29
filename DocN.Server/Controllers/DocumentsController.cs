using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DocN.Data;
using DocN.Data.Models;
using DocN.Data.Services;
using DocN.Data.Constants;
using DocN.Server.Middleware;

namespace DocN.Server.Controllers;

/// <summary>
/// Controller REST API per gestione completa documenti (CRUD, ricerca, elaborazione)
/// Espone endpoint per client frontend e integrazioni esterne
/// </summary>
/// <remarks>
/// Scopo: Fornire API RESTful per tutte le operazioni sui documenti
/// 
/// Operazioni supportate:
/// - GET /documents: Lista tutti documenti
/// - GET /documents/{id}: Dettagli documento specifico
/// - POST /documents: Crea nuovo documento
/// - PUT /documents/{id}: Aggiorna documento esistente
/// - DELETE /documents/{id}: Elimina documento
/// - GET /documents/search: Ricerca ibrida (vettoriale + full-text)
/// - POST /documents/{id}/process: Elabora documento (chunking + embeddings)
/// 
/// Dipendenze:
/// - DocArcContext: Accesso database documenti
/// - IChunkingService: Suddivisione documenti in chunk
/// - IBatchProcessingService: Elaborazione batch multipli documenti
/// - IEmbeddingService: Generazione embeddings vettoriali
/// 
/// Note:
/// - Tutti gli endpoint includono logging errori per diagnostica
/// - Response codes standard REST (200, 404, 500)
/// - Supporto paginazione per liste grandi (implementabile)
/// </remarks>
[ApiController]
[Route("[controller]")]
public class DocumentsController : ControllerBase
{
    private readonly DocArcContext _context;
    private readonly ILogger<DocumentsController> _logger;
    private readonly IChunkingService _chunkingService;
    private readonly IBatchProcessingService _batchProcessingService;
    private readonly IEmbeddingService _embeddingService;
    private readonly IDocumentService _documentService;
    private readonly IDocumentWorkflowService _workflowService;

    public DocumentsController(
        DocArcContext context, 
        ILogger<DocumentsController> logger,
        IChunkingService chunkingService,
        IBatchProcessingService batchProcessingService,
        IEmbeddingService embeddingService,
        IDocumentService documentService,
        IDocumentWorkflowService workflowService)
    {
        _context = context;
        _logger = logger;
        _chunkingService = chunkingService;
        _batchProcessingService = batchProcessingService;
        _embeddingService = embeddingService;
        _documentService = documentService;
        _workflowService = workflowService;
    }

    /// <summary>
    /// Ottiene lista paginata di documenti ordinati per data upload (più recenti primi)
    /// </summary>
    /// <param name="page">Numero pagina (default 1)</param>
    /// <param name="pageSize">Documenti per pagina (default 10, max 100)</param>
    /// <returns>Lista paginata documenti con metadati essenziali</returns>
    /// <response code="200">Ritorna la lista paginata dei documenti</response>
    /// <response code="500">Errore interno del server durante recupero</response>
    /// <remarks>
    /// Scopo: Fornire lista paginata documenti per visualizzazione in UI con caricamento asincrono
    /// 
    /// Comportamento:
    /// - Paginazione server-side per ottimizzare caricamento
    /// - Ordinamento decrescente per data upload
    /// - Include metadati: nome file, categoria, tag, dimensione, owner, testo estratto
    /// - ESCLUDE solo vettori embedding per ottimizzazione performance
    /// - Caricamento asincrono con 10 documenti per pagina di default
    /// 
    /// Performance:
    /// - Query ottimizzata con proiezione per escludere embeddings (768-1536 floats per documento)
    /// - Indice su UploadedAt per ordinamento veloce
    /// - Paginazione riduce drasticamente data transfer
    /// - Page size ridotto a 10 per caricamento più veloce
    /// 
    /// Note:
    /// - ExtractedText è incluso per supportare ricerca client-side
    /// - Per documenti con testi molto lunghi, considerare endpoint dedicato
    /// </remarks>
    [HttpGet]
    [RequirePermission(Permissions.DocumentRead)]
    [ProducesResponseType(typeof(IEnumerable<Document>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<Document>>> GetDocuments(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            // Validate pagination parameters
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100; // Max 100 items per page
            
            // PERFORMANCE FIX: Use projection to exclude embedding vectors
            // This significantly reduces data transfer and improves query speed
            // Embedding vectors (768 or 1536 floats per document) are only needed for semantic search
            // ExtractedText is included for client-side search functionality
            var documents = await _context.Documents
                .OrderByDescending(d => d.UploadedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(d => new Document
                {
                    Id = d.Id,
                    FileName = d.FileName,
                    FilePath = d.FilePath,
                    ContentType = d.ContentType,
                    FileSize = d.FileSize,
                    ExtractedText = d.ExtractedText, // Included for client-side search
                    SuggestedCategory = d.SuggestedCategory,
                    CategoryReasoning = d.CategoryReasoning,
                    ActualCategory = d.ActualCategory,
                    AITagsJson = d.AITagsJson,
                    AIAnalysisDate = d.AIAnalysisDate,
                    ExtractedMetadataJson = d.ExtractedMetadataJson,
                    PageCount = d.PageCount,
                    DetectedLanguage = d.DetectedLanguage,
                    ProcessingStatus = d.ProcessingStatus,
                    ProcessingError = d.ProcessingError,
                    ChunkEmbeddingStatus = d.ChunkEmbeddingStatus,
                    Notes = d.Notes,
                    Visibility = d.Visibility,
                    // EmbeddingVector768 and EmbeddingVector1536 excluded - not needed for list view
                    EmbeddingDimension = d.EmbeddingDimension,
                    UploadedAt = d.UploadedAt,
                    LastAccessedAt = d.LastAccessedAt,
                    AccessCount = d.AccessCount,
                    OwnerId = d.OwnerId,
                    TenantId = d.TenantId
                })
                .ToListAsync();

            _logger.LogInformation("Retrieved {Count} documents for page {Page} (pageSize: {PageSize})", 
                documents.Count, page, pageSize);
            return Ok(documents);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving documents");
            return StatusCode(500, "An error occurred while retrieving documents");
        }
    }

    /// <summary>
    /// Ottiene il numero totale di documenti per supportare la paginazione
    /// </summary>
    /// <returns>Numero totale di documenti</returns>
    /// <response code="200">Ritorna il conteggio totale</response>
    /// <response code="500">Errore interno del server</response>
    [HttpGet("count")]
    [RequirePermission(Permissions.DocumentRead)]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<int>> GetDocumentsCount()
    {
        try
        {
            var count = await _context.Documents.CountAsync();
            _logger.LogInformation("Total documents count: {Count}", count);
            return Ok(count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error counting documents");
            return StatusCode(500, "An error occurred while counting documents");
        }
    }

    /// <summary>
    /// Ottiene un singolo documento per ID con tutti i suoi metadati
    /// </summary>
    /// <param name="id">ID univoco del documento (chiave primaria)</param>
    /// <returns>Oggetto Document completo o 404 se non trovato</returns>
    /// <response code="200">Ritorna il documento richiesto</response>
    /// <response code="404">Documento con ID specificato non esiste</response>
    /// <response code="500">Errore interno del server</response>
    /// <remarks>
    /// Scopo: Recuperare dettagli completi di un documento specifico
    /// 
    /// Utilizzo tipico:
    /// - Visualizzazione dettagli documento in UI
    /// - Download documento
    /// - Modifica metadati (pre-popolamento form)
    /// 
    /// Output atteso:
    /// - Oggetto Document JSON con tutti i campi
    /// - Include: nome, path, categoria, tag, embeddings, owner, date, etc.
    /// - 404 se ID non esiste nel database
    /// 
    /// Performance:
    /// - Query ottimizzata con ricerca per chiave primaria (molto veloce)
    /// - Tipicamente meno di 10ms
    /// 
    /// Sicurezza:
    /// - Controllo autorizzazione: solo owner, admin, o utenti con permessi possono accedere
    /// - Verifica visibilità documento (private/shared/public)
    /// </remarks>
    [HttpGet("{id}")]
    [RequirePermission(Permissions.DocumentRead)]
    [ProducesResponseType(typeof(Document), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<Document>> GetDocument(int id)
    {
        try
        {
            var document = await _context.Documents
                .Include(d => d.Shares)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (document == null)
            {
                return NotFound();
            }

            // Get current user ID
            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            // Check authorization based on visibility
            if (!CanAccessDocument(document, currentUserId))
            {
                _logger.LogWarning(
                    "User {UserId} attempted to access document {DocumentId} with visibility {Visibility} owned by {OwnerId}",
                    currentUserId ?? "anonymous", id, document.Visibility, document.OwnerId);
                
                if (string.IsNullOrEmpty(currentUserId))
                {
                    return Unauthorized("Authentication required to access this document");
                }
                
                return StatusCode(StatusCodes.Status403Forbidden, "You do not have permission to access this document");
            }

            return Ok(document);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving document {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the document");
        }
    }

    /// <summary>
    /// Checks if the current user can access a document based on visibility settings
    /// </summary>
    /// <param name="document">The document to check access for</param>
    /// <param name="currentUserId">The current user ID (null if not authenticated)</param>
    /// <returns>True if user can access the document, false otherwise</returns>
    private bool CanAccessDocument(Document document, string? currentUserId)
    {
        // Public documents are accessible by everyone
        if (document.Visibility == DocumentVisibility.Public)
        {
            return true;
        }

        // For non-public documents, user must be authenticated
        if (string.IsNullOrEmpty(currentUserId))
        {
            return false;
        }

        // Owner always has access
        if (document.OwnerId == currentUserId)
        {
            return true;
        }

        // Check if user is admin (you may need to adjust this based on your role implementation)
        var isAdmin = User.IsInRole("Admin") || User.IsInRole("Administrator");
        if (isAdmin)
        {
            return true;
        }

        // For shared documents, check if user has explicit share permission
        if (document.Visibility == DocumentVisibility.Shared)
        {
            return document.Shares?.Any(s => s.SharedWithUserId == currentUserId) ?? false;
        }

        // For organization documents, check if user is in the same organization
        // TODO: Implement organization membership check when organization feature is added
        if (document.Visibility == DocumentVisibility.Organization)
        {
            // For now, deny access. Implement proper organization check when available
            return false;
        }

        // Default deny
        return false;
    }

    /// <summary>
    /// Crea un nuovo documento
    /// </summary>
    /// <param name="document">Dati del documento da creare</param>
    /// <returns>Il documento creato con ID assegnato</returns>
    /// <response code="201">Documento creato con successo</response>
    /// <response code="500">Errore interno del server</response>
    [HttpPost]
    [RequirePermission(Permissions.DocumentWrite)]
    [ProducesResponseType(typeof(Document), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<Document>> CreateDocument(Document document)
    {
        try
        {
            document.UploadedAt = DateTime.UtcNow;
            
            // Initialize workflow state
            document.WorkflowState = DocN.Data.Constants.DocumentProcessingState.Queued;
            document.StateEnteredAt = DateTime.UtcNow;
            document.MaxRetries = 3; // Default max retries
            
            // Vector can be null initially and calculated later
            
            _context.Documents.Add(document);
            await _context.SaveChangesAsync();

            // Transition to Extracting state if text is already available
            if (!string.IsNullOrEmpty(document.ExtractedText))
            {
                await _workflowService.TransitionToStateAsync(
                    document.Id,
                    DocN.Data.Constants.DocumentProcessingState.Extracting,
                    "Document uploaded with extracted text");

                // Create chunks for the document
                var chunks = _chunkingService.ChunkDocument(document);
                if (chunks.Any())
                {
                    // Transition to Chunking state
                    await _workflowService.TransitionToStateAsync(
                        document.Id,
                        DocN.Data.Constants.DocumentProcessingState.Chunking,
                        "Creating chunks");

                    // Generate embeddings for chunks using parallel processing
                    var embeddedCount = await GenerateChunkEmbeddingsAsync(chunks, document.Id);
                    
                    _context.DocumentChunks.AddRange(chunks);
                    await _context.SaveChangesAsync();
                    
                    _logger.LogInformation("Created {ChunkCount} chunks for document {Id}, {EmbeddedCount} with embeddings", 
                        chunks.Count, document.Id, embeddedCount);

                    // Transition to Embedding state
                    await _workflowService.TransitionToStateAsync(
                        document.Id,
                        DocN.Data.Constants.DocumentProcessingState.Embedding,
                        "Generating embeddings");
                }
            }

            _logger.LogInformation("Created document {Id} - {FileName} in state {State}", 
                document.Id, document.FileName, document.WorkflowState);
            return CreatedAtAction(nameof(GetDocument), new { id = document.Id }, document);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating document");
            
            // Try to record error in workflow if document was saved
            if (document.Id > 0)
            {
                try
                {
                    await _workflowService.RecordErrorAsync(
                        document.Id,
                        ex,
                        DocN.Data.Constants.DocumentErrorType.UnknownError);
                }
                catch (Exception workflowEx)
                {
                    _logger.LogError(workflowEx, "Error recording workflow error for document {Id}", document.Id);
                }
            }
            
            return StatusCode(500, "An error occurred while creating the document");
        }
    }

    /// <summary>
    /// Carica un nuovo documento con file
    /// </summary>
    /// <param name="file">File da caricare</param>
    /// <param name="category">Categoria del documento (opzionale)</param>
    /// <param name="visibility">Livello di visibilità (opzionale, default: Private)</param>
    /// <param name="notes">Note sul documento (opzionale)</param>
    /// <returns>Il documento creato</returns>
    /// <response code="201">Documento caricato con successo</response>
    /// <response code="400">Richiesta non valida (file mancante o non valido)</response>
    /// <response code="500">Errore interno del server</response>
    [HttpPost("upload")]
    [RequirePermission(Permissions.DocumentWrite)]
    [ProducesResponseType(typeof(Document), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [RequestSizeLimit(52428800)] // 50 MB limit
    public async Task<ActionResult<Document>> UploadDocument(
        [FromForm] IFormFile file,
        [FromForm] string? category = null,
        [FromForm] int visibility = 0,
        [FromForm] string? notes = null)
    {
        try
        {
            // Validate file
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { error = "No file provided" });
            }

            // Check file size (50 MB)
            if (file.Length > 52428800)
            {
                return BadRequest(new { error = "File size exceeds maximum limit of 50 MB" });
            }

            // Get user ID from claims
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            // Create upload directory if it doesn't exist
            var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
            Directory.CreateDirectory(uploadsPath);

            // Generate unique filename
            var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
            var filePath = Path.Combine(uploadsPath, uniqueFileName);

            // Save file to disk
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            _logger.LogInformation("File saved to {FilePath}", filePath);

            // Extract text from file (basic implementation)
            string extractedText = "";
            try
            {
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                
                // For text files, read content directly
                if (extension == ".txt")
                {
                    extractedText = await System.IO.File.ReadAllTextAsync(filePath);
                }
                // For PDFs and other formats, we'll add processing later
                // For now, just mark that text extraction is needed
                else
                {
                    extractedText = $"[Text extraction pending for {extension} file]";
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error extracting text from file");
                extractedText = "[Text extraction failed]";
            }

            // Create document entity
            var document = new Document
            {
                FileName = file.FileName,
                FilePath = filePath,
                ContentType = file.ContentType,
                FileSize = file.Length,
                ExtractedText = extractedText,
                ActualCategory = category ?? "Uncategorized",
                Visibility = (DocumentVisibility)visibility,
                Notes = notes,
                OwnerId = userId,
                UploadedAt = DateTime.UtcNow,
                WorkflowState = DocN.Data.Constants.DocumentProcessingState.Queued,
                StateEnteredAt = DateTime.UtcNow,
                MaxRetries = 3
            };

            // Save to database
            _context.Documents.Add(document);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Document {Id} - {FileName} uploaded successfully", document.Id, document.FileName);

            // If text was extracted, create chunks and embeddings asynchronously
            if (!string.IsNullOrEmpty(extractedText) && !extractedText.Contains("[Text extraction"))
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _workflowService.TransitionToStateAsync(
                            document.Id,
                            DocN.Data.Constants.DocumentProcessingState.Extracting,
                            "Document uploaded with extracted text");

                        var chunks = _chunkingService.ChunkDocument(document);
                        if (chunks.Any())
                        {
                            await _workflowService.TransitionToStateAsync(
                                document.Id,
                                DocN.Data.Constants.DocumentProcessingState.Chunking,
                                "Creating chunks");

                            var embeddedCount = await GenerateChunkEmbeddingsAsync(chunks, document.Id);
                            
                            _context.DocumentChunks.AddRange(chunks);
                            await _context.SaveChangesAsync();
                            
                            _logger.LogInformation("Created {ChunkCount} chunks for document {Id}, {EmbeddedCount} with embeddings", 
                                chunks.Count, document.Id, embeddedCount);

                            await _workflowService.TransitionToStateAsync(
                                document.Id,
                                DocN.Data.Constants.DocumentProcessingState.Completed,
                                "Document processing completed");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing document {Id} asynchronously", document.Id);
                        await _workflowService.RecordErrorAsync(
                            document.Id,
                            ex,
                            DocN.Data.Constants.DocumentErrorType.UnknownError);
                    }
                });
            }

            return CreatedAtAction(nameof(GetDocument), new { id = document.Id }, document);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading document");
            return StatusCode(500, new { error = "An error occurred while uploading the document" });
        }
    }

    /// <summary>
    /// Aggiorna un documento esistente
    /// </summary>
    /// <param name="id">ID del documento da aggiornare</param>
    /// <param name="document">Dati aggiornati del documento</param>
    /// <returns>Il documento aggiornato</returns>
    /// <response code="200">Documento aggiornato con successo</response>
    /// <response code="400">Richiesta non valida (ID mismatch)</response>
    /// <response code="404">Documento non trovato</response>
    /// <response code="500">Errore interno del server</response>
    [HttpPut("{id}")]
    [RequirePermission(Permissions.DocumentWrite)]
    [ProducesResponseType(typeof(Document), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<Document>> UpdateDocument(int id, Document document)
    {
        try
        {
            if (id != document.Id)
            {
                return BadRequest("Document ID mismatch");
            }

            var existingDocument = await _context.Documents.FindAsync(id);
            if (existingDocument == null)
            {
                return NotFound($"Document with ID {id} not found");
            }

            // Check if extracted text will change (before updating)
            bool extractedTextChanged = !string.IsNullOrEmpty(document.ExtractedText) && 
                                       document.ExtractedText != existingDocument.ExtractedText;

            // Update document properties
            existingDocument.FileName = document.FileName;
            existingDocument.ContentType = document.ContentType;
            existingDocument.FileSize = document.FileSize;
            existingDocument.ExtractedText = document.ExtractedText;
            existingDocument.SuggestedCategory = document.SuggestedCategory;
            existingDocument.CategoryReasoning = document.CategoryReasoning;
            existingDocument.ActualCategory = document.ActualCategory;
            existingDocument.Visibility = document.Visibility;
            existingDocument.Notes = document.Notes;
            existingDocument.PageCount = document.PageCount;
            existingDocument.DetectedLanguage = document.DetectedLanguage;
            existingDocument.ProcessingStatus = document.ProcessingStatus;
            existingDocument.ProcessingError = document.ProcessingError;
            existingDocument.AIAnalysisDate = document.AIAnalysisDate;
            existingDocument.AITagsJson = document.AITagsJson;
            existingDocument.ExtractedMetadataJson = document.ExtractedMetadataJson;

            // Update embedding if provided
            if (document.EmbeddingVector != null)
            {
                existingDocument.EmbeddingVector = document.EmbeddingVector;
                existingDocument.EmbeddingDimension = document.EmbeddingVector.Length;
            }

            // Update file path only if a new file was uploaded
            if (!string.IsNullOrEmpty(document.FilePath) && document.FilePath != existingDocument.FilePath)
            {
                existingDocument.FilePath = document.FilePath;
            }

            await _context.SaveChangesAsync();

            // Recreate chunks if extracted text changed
            if (extractedTextChanged)
            {
                // Delete existing chunks
                var existingChunks = await _context.DocumentChunks
                    .Where(c => c.DocumentId == id)
                    .ToListAsync();
                _context.DocumentChunks.RemoveRange(existingChunks);

                // Create new chunks with embeddings using parallel processing
                var newChunks = _chunkingService.ChunkDocument(existingDocument);
                if (newChunks.Any())
                {
                    var embeddedCount = await GenerateChunkEmbeddingsAsync(newChunks, id);
                    
                    _context.DocumentChunks.AddRange(newChunks);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Updated {ChunkCount} chunks for document {Id}, {EmbeddedCount} with embeddings", 
                        newChunks.Count, id, embeddedCount);
                }
            }

            _logger.LogInformation("Updated document {Id} - {FileName}", id, document.FileName);
            return Ok(existingDocument);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating document {Id}", id);
            return StatusCode(500, "An error occurred while updating the document");
        }
    }

    /// <summary>
    /// Scarica il file di un documento
    /// </summary>
    /// <param name="id">ID del documento da scaricare</param>
    /// <returns>Il file del documento</returns>
    /// <response code="200">Ritorna il file del documento</response>
    /// <response code="404">Documento o file non trovato</response>
    /// <response code="500">Errore interno del server</response>
    [HttpGet("{id}/download")]
    [RequirePermission(Permissions.DocumentRead)]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DownloadDocument(int id)
    {
        try
        {
            var document = await _context.Documents.FindAsync(id);

            if (document == null)
            {
                return NotFound("Documento non trovato");
            }

            if (!System.IO.File.Exists(document.FilePath))
            {
                return NotFound("File non trovato sul server");
            }

            var fileBytes = await System.IO.File.ReadAllBytesAsync(document.FilePath);
            
            // Update access statistics
            document.AccessCount++;
            document.LastAccessedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return File(fileBytes, document.ContentType, document.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading document {Id}", id);
            return StatusCode(500, "Errore durante il download del documento");
        }
    }

    /// <summary>
    /// Elimina un documento esistente con tutte le relazioni associate
    /// </summary>
    /// <param name="id">ID del documento da eliminare</param>
    /// <returns>Result dell'operazione</returns>
    /// <response code="204">Documento eliminato con successo</response>
    /// <response code="403">Utente non autorizzato (solo il proprietario può eliminare)</response>
    /// <response code="404">Documento non trovato</response>
    /// <response code="500">Errore interno del server</response>
    /// <remarks>
    /// Security: Only document owner can delete the document
    /// Note: Physical file deletion happens after DB commit - orphaned files may exist if deletion fails
    /// Consider: Implement cleanup job for orphaned files
    /// </remarks>
    [HttpDelete("{id}")]
    [RequirePermission(Permissions.DocumentDelete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteDocument(int id)
    {
        try
        {
            var document = await _context.Documents.FindAsync(id);

            if (document == null)
            {
                return NotFound($"Document with ID {id} not found");
            }

            // SECURITY CHECK: Only document owner can delete
            // Get current user ID from claims (assuming user is authenticated)
            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            
            if (!string.IsNullOrEmpty(document.OwnerId))
            {
                // Document has an owner - check authorization
                if (string.IsNullOrEmpty(currentUserId) || document.OwnerId != currentUserId)
                {
                    _logger.LogWarning("User {UserId} attempted to delete document {DocumentId} owned by {OwnerId}", 
                        currentUserId ?? "anonymous", id, document.OwnerId);
                    return StatusCode(403, "Only the document owner can delete this document");
                }
            }
            else
            {
                // Document has no owner - could be legacy data or system-created
                // Only allow deletion if user is authenticated (basic check)
                if (string.IsNullOrEmpty(currentUserId))
                {
                    _logger.LogWarning("Anonymous user attempted to delete document {DocumentId} with no owner", id);
                    return StatusCode(403, "Authentication required to delete documents");
                }
            }

            // Delete associated chunks first
            var chunks = await _context.DocumentChunks
                .Where(c => c.DocumentId == id)
                .ToListAsync();
            
            if (chunks.Any())
            {
                _context.DocumentChunks.RemoveRange(chunks);
            }

            // Delete associated similar documents relationships
            var similarDocuments = await _context.SimilarDocuments
                .Where(sd => sd.SourceDocumentId == id || sd.SimilarDocumentId == id)
                .ToListAsync();
            
            if (similarDocuments.Any())
            {
                _context.SimilarDocuments.RemoveRange(similarDocuments);
            }

            // Delete the document
            _context.Documents.Remove(document);
            await _context.SaveChangesAsync();

            // Try to delete the physical file (if it exists)
            if (!string.IsNullOrEmpty(document.FilePath) && System.IO.File.Exists(document.FilePath))
            {
                try
                {
                    System.IO.File.Delete(document.FilePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not delete physical file for document {Id}: {FilePath}", 
                        id, document.FilePath);
                    // Continue even if file deletion fails - document is already removed from DB
                }
            }

            _logger.LogInformation("User {UserId} deleted document {Id} - {FileName}", 
                currentUserId, id, document.FileName);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting document {Id}", id);
            return StatusCode(500, "An error occurred while deleting the document");
        }
    }

    /// <summary>
    /// Ottiene tutte le categorie uniche dei documenti
    /// </summary>
    /// <returns>Lista delle categorie disponibili</returns>
    /// <response code="200">Ritorna la lista delle categorie</response>
    /// <response code="500">Errore interno del server</response>
    [HttpGet("categories")]
    [RequirePermission(Permissions.DocumentRead)]
    [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<string>>> GetCategories()
    {
        try
        {
            // Get all unique categories from documents in a single query
            var categories = await _context.Documents
                .Select(d => new { d.ActualCategory, d.SuggestedCategory })
                .ToListAsync();

            // Process in memory to get unique categories
            var uniqueCategories = categories
                .SelectMany(d => new[] { d.ActualCategory, d.SuggestedCategory })
                .Where(c => !string.IsNullOrEmpty(c) && c != "Uncategorized")
                .Distinct()
                .OrderBy(c => c)
                .ToList();

            _logger.LogInformation("Retrieved {Count} unique categories", uniqueCategories.Count);
            return Ok(uniqueCategories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving categories");
            return StatusCode(500, "An error occurred while retrieving categories");
        }
    }

    /// <summary>
    /// Ricrea gli embeddings per tutti i documenti
    /// </summary>
    /// <remarks>
    /// Questa operazione rigenerera gli embeddings vettoriali per tutti i documenti che hanno testo estratto.
    /// Può richiedere molto tempo per database di grandi dimensioni.
    /// </remarks>
    /// <returns>Statistiche dell'operazione di ricreazione</returns>
    /// <response code="200">Operazione completata con successo</response>
    /// <response code="500">Errore interno del server</response>
    [HttpPost("recreate-embeddings")]
    [RequirePermission(Permissions.AdminSystem)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> RecreateAllEmbeddings()
    {
        try
        {
            _logger.LogInformation("Starting recreation of all embeddings");

            // Get all documents
            var documents = await _context.Documents
                .Where(d => !string.IsNullOrEmpty(d.ExtractedText))
                .ToListAsync();

            _logger.LogInformation("Found {Count} documents to process", documents.Count);

            int successCount = 0;
            int errorCount = 0;

            foreach (var document in documents)
            {
                try
                {
                    // Regenerate embedding for document
                    var embedding = await _embeddingService.GenerateEmbeddingAsync(document.ExtractedText);
                    if (embedding != null)
                    {
                        document.EmbeddingVector = embedding;
                        document.EmbeddingDimension = embedding.Length;
                        successCount++;
                    }

                    // Delete existing chunks and recreate them
                    var existingChunks = await _context.DocumentChunks
                        .Where(c => c.DocumentId == document.Id)
                        .ToListAsync();
                    
                    _context.DocumentChunks.RemoveRange(existingChunks);

                    // Create new chunks with embeddings using the same parallel processing helper
                    var newChunks = _chunkingService.ChunkDocument(document);
                    if (newChunks.Any())
                    {
                        var embeddedCount = await GenerateChunkEmbeddingsAsync(newChunks, document.Id);
                        _context.DocumentChunks.AddRange(newChunks);
                        _logger.LogInformation("Created {ChunkCount} chunks for document {Id}, {EmbeddedCount} with embeddings", 
                            newChunks.Count, document.Id, embeddedCount);
                    }

                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Recreated embeddings for document {Id}: {FileName}", 
                        document.Id, document.FileName);
                }
                catch (Exception ex)
                {
                    errorCount++;
                    _logger.LogError(ex, "Error recreating embeddings for document {Id}", document.Id);
                }
            }

            var message = $"Embeddings recreated for {successCount} documents. Errors: {errorCount}";
            _logger.LogInformation(message);
            return Ok(new { message, successCount, errorCount, totalDocuments = documents.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recreating all embeddings");
            return StatusCode(500, "An error occurred while recreating embeddings");
        }
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
        var successCount = 0;
        var totalBatches = (int)Math.Ceiling(chunks.Count / (double)batchSize);
        var currentBatch = 0;
        
        // Reuse semaphore across all batches for efficiency
        using var semaphore = new SemaphoreSlim(maxConcurrency);
        
        // Process chunks in batches to avoid memory pressure with large documents
        for (int i = 0; i < chunks.Count; i += batchSize)
        {
            currentBatch++;
            var batch = chunks.Skip(i).Take(batchSize).ToList();
            
            _logger.LogInformation("Processing batch {CurrentBatch}/{TotalBatches} ({ChunkCount} chunks) for document {DocumentId}", 
                currentBatch, totalBatches, batch.Count, documentId);
            
            var tasks = new List<Task<bool>>();
            
            foreach (var chunk in batch)
            {
                tasks.Add(GenerateSingleChunkEmbeddingAsync(chunk, documentId, semaphore));
            }
            
            var results = await Task.WhenAll(tasks);
            var batchSuccess = results.Count(r => r);
            successCount += batchSuccess;
            
            _logger.LogInformation("Batch {CurrentBatch}/{TotalBatches} completed: {Success}/{Total} chunks embedded successfully", 
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
    private async Task<bool> GenerateSingleChunkEmbeddingAsync(DocumentChunk chunk, int documentId, SemaphoreSlim semaphore)
    {
        await semaphore.WaitAsync();
        try
        {
            var chunkEmbedding = await _embeddingService.GenerateEmbeddingAsync(chunk.ChunkText);
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
            _logger.LogWarning(ex, "Failed to generate embedding for chunk {ChunkIndex} of document {DocumentId}", 
                chunk.ChunkIndex, documentId);
            return false;
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// Aggiorna la visibilità di un documento
    /// </summary>
    /// <param name="id">ID del documento</param>
    /// <param name="request">Richiesta con nuovo livello di visibilità</param>
    /// <returns>Risultato dell'operazione</returns>
    [HttpPatch("{id}/visibility")]
    [RequirePermission(Permissions.DocumentWrite)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateDocumentVisibility(int id, [FromBody] UpdateVisibilityRequest request)
    {
        try
        {
            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized("User must be authenticated");
            }

            var success = await _documentService.UpdateDocumentVisibilityAsync(id, request.Visibility, currentUserId);
            
            if (!success)
            {
                return NotFound("Document not found or you don't have permission to update it");
            }

            _logger.LogInformation("User {UserId} updated visibility of document {DocumentId} to {Visibility}", 
                currentUserId, id, request.Visibility);
            
            return Ok(new { message = "Visibility updated successfully", visibility = request.Visibility });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating visibility for document {DocumentId}", id);
            return StatusCode(500, "An error occurred while updating document visibility");
        }
    }

    /// <summary>
    /// Condivide un documento con un utente specifico
    /// </summary>
    /// <param name="id">ID del documento</param>
    /// <param name="request">Richiesta con ID utente e permesso</param>
    /// <returns>Risultato dell'operazione</returns>
    [HttpPost("{id}/shares/user")]
    [RequirePermission(Permissions.DocumentShare)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ShareWithUser(int id, [FromBody] ShareWithUserRequest request)
    {
        try
        {
            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized("User must be authenticated");
            }

            var success = await _documentService.ShareDocumentAsync(id, request.UserId, request.Permission, currentUserId);
            
            if (!success)
            {
                return NotFound("Document not found or you don't have permission to share it");
            }

            _logger.LogInformation("User {UserId} shared document {DocumentId} with user {SharedWithUserId}", 
                currentUserId, id, request.UserId);
            
            return Ok(new { message = "Document shared successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sharing document {DocumentId}", id);
            return StatusCode(500, "An error occurred while sharing document");
        }
    }

    /// <summary>
    /// Condivide un documento con un gruppo
    /// </summary>
    /// <param name="id">ID del documento</param>
    /// <param name="request">Richiesta con ID gruppo e permesso</param>
    /// <returns>Risultato dell'operazione</returns>
    [HttpPost("{id}/shares/group")]
    [RequirePermission(Permissions.DocumentShare)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ShareWithGroup(int id, [FromBody] ShareWithGroupRequest request)
    {
        try
        {
            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized("User must be authenticated");
            }

            var success = await _documentService.ShareDocumentWithGroupAsync(id, request.GroupId, request.Permission, currentUserId);
            
            if (!success)
            {
                return NotFound("Document or group not found, or you don't have permission to share");
            }

            _logger.LogInformation("User {UserId} shared document {DocumentId} with group {GroupId}", 
                currentUserId, id, request.GroupId);
            
            return Ok(new { message = "Document shared with group successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sharing document {DocumentId} with group", id);
            return StatusCode(500, "An error occurred while sharing document with group");
        }
    }

    /// <summary>
    /// Ottiene le condivisioni di un documento
    /// </summary>
    /// <param name="id">ID del documento</param>
    /// <returns>Lista delle condivisioni</returns>
    [HttpGet("{id}/shares")]
    [RequirePermission(Permissions.DocumentRead)]
    [ProducesResponseType(typeof(DocumentShareInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetDocumentShares(int id)
    {
        try
        {
            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized("User must be authenticated");
            }

            var shares = await _documentService.GetDocumentSharesAsync(id, currentUserId);
            
            if (shares == null)
            {
                return NotFound("Document not found or you don't have permission to view shares");
            }

            return Ok(shares);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving shares for document {DocumentId}", id);
            return StatusCode(500, "An error occurred while retrieving document shares");
        }
    }

    /// <summary>
    /// Rimuove la condivisione con un utente
    /// </summary>
    /// <param name="id">ID del documento</param>
    /// <param name="userId">ID dell'utente</param>
    /// <returns>Risultato dell'operazione</returns>
    [HttpDelete("{id}/shares/user/{userId}")]
    [RequirePermission(Permissions.DocumentShare)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> RemoveUserShare(int id, string userId)
    {
        try
        {
            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized("User must be authenticated");
            }

            var success = await _documentService.RemoveUserShareAsync(id, userId, currentUserId);
            
            if (!success)
            {
                return NotFound("Document or share not found, or you don't have permission");
            }

            _logger.LogInformation("User {UserId} removed share for user {SharedUserId} from document {DocumentId}", 
                currentUserId, userId, id);
            
            return Ok(new { message = "Share removed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing user share from document {DocumentId}", id);
            return StatusCode(500, "An error occurred while removing share");
        }
    }

    /// <summary>
    /// Rimuove la condivisione con un gruppo
    /// </summary>
    /// <param name="id">ID del documento</param>
    /// <param name="groupId">ID del gruppo</param>
    /// <returns>Risultato dell'operazione</returns>
    [HttpDelete("{id}/shares/group/{groupId}")]
    [RequirePermission(Permissions.DocumentShare)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> RemoveGroupShare(int id, int groupId)
    {
        try
        {
            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized("User must be authenticated");
            }

            var success = await _documentService.RemoveGroupShareAsync(id, groupId, currentUserId);
            
            if (!success)
            {
                return NotFound("Document or share not found, or you don't have permission");
            }

            _logger.LogInformation("User {UserId} removed share for group {GroupId} from document {DocumentId}", 
                currentUserId, groupId, id);
            
            return Ok(new { message = "Group share removed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing group share from document {DocumentId}", id);
            return StatusCode(500, "An error occurred while removing group share");
        }
    }

    // ========== Workflow Management Endpoints ==========

    /// <summary>
    /// Get detailed workflow status for a document
    /// </summary>
    /// <param name="id">Document ID</param>
    /// <returns>Workflow status with history and valid transitions</returns>
    [HttpGet("{id}/workflow/status")]
    [RequirePermission(Permissions.DocumentRead)]
    [ProducesResponseType(typeof(DocumentWorkflowStatus), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetWorkflowStatus(int id)
    {
        try
        {
            var status = await _workflowService.GetWorkflowStatusAsync(id);
            return Ok(status);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Document {DocumentId} not found", id);
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting workflow status for document {DocumentId}", id);
            return StatusCode(500, "An error occurred while retrieving workflow status");
        }
    }

    /// <summary>
    /// Trigger AI analysis for a document (transition to Analyzing state)
    /// </summary>
    /// <param name="id">Document ID</param>
    /// <returns>Transition result</returns>
    [HttpPost("{id}/workflow/analyze")]
    [RequirePermission(Permissions.DocumentWrite)]
    [ProducesResponseType(typeof(WorkflowTransitionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> TriggerAnalysis(int id)
    {
        try
        {
            var document = await _context.Documents.FindAsync(id);
            if (document == null)
            {
                return NotFound($"Document {id} not found");
            }

            // Transition to Analyzing state
            var result = await _workflowService.TransitionToStateAsync(
                id, 
                DocN.Data.Constants.DocumentProcessingState.Analyzing,
                "Manual analysis trigger");

            if (!result.Success)
            {
                return BadRequest(result.ErrorMessage);
            }

            _logger.LogInformation("Triggered analysis for document {DocumentId}", id);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error triggering analysis for document {DocumentId}", id);
            return StatusCode(500, "An error occurred while triggering analysis");
        }
    }

    /// <summary>
    /// Confirm metadata and proceed to chunking (AwaitingConfirmation → Chunking)
    /// </summary>
    /// <param name="id">Document ID</param>
    /// <param name="request">Confirmation details with optional metadata updates</param>
    /// <returns>Transition result</returns>
    [HttpPost("{id}/workflow/confirm")]
    [RequirePermission(Permissions.DocumentWrite)]
    [ProducesResponseType(typeof(WorkflowTransitionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ConfirmMetadata(int id, [FromBody] ConfirmMetadataRequest request)
    {
        try
        {
            var document = await _context.Documents.FindAsync(id);
            if (document == null)
            {
                return NotFound($"Document {id} not found");
            }

            // Update metadata if provided
            if (!string.IsNullOrEmpty(request.ActualCategory))
            {
                document.ActualCategory = request.ActualCategory;
            }
            if (!string.IsNullOrEmpty(request.Notes))
            {
                document.Notes = request.Notes;
            }

            await _context.SaveChangesAsync();

            // Transition to Chunking state
            var result = await _workflowService.TransitionToStateAsync(
                id,
                DocN.Data.Constants.DocumentProcessingState.Chunking,
                "User confirmed metadata");

            if (!result.Success)
            {
                return BadRequest(result.ErrorMessage);
            }

            _logger.LogInformation("Confirmed metadata for document {DocumentId}", id);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error confirming metadata for document {DocumentId}", id);
            return StatusCode(500, "An error occurred while confirming metadata");
        }
    }

    /// <summary>
    /// Manually retry a failed document
    /// </summary>
    /// <param name="id">Document ID</param>
    /// <returns>Transition result</returns>
    [HttpPost("{id}/workflow/retry")]
    [RequirePermission(Permissions.DocumentWrite)]
    [ProducesResponseType(typeof(WorkflowTransitionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> RetryProcessing(int id)
    {
        try
        {
            var document = await _context.Documents.FindAsync(id);
            if (document == null)
            {
                return NotFound($"Document {id} not found");
            }

            // Check if document is in a retryable state
            if (document.WorkflowState != DocN.Data.Constants.DocumentProcessingState.Failed &&
                document.WorkflowState != DocN.Data.Constants.DocumentProcessingState.PermanentFailure)
            {
                return BadRequest($"Document is not in a failed state. Current state: {document.WorkflowState}");
            }

            // Reset workflow to restart processing
            var result = await _workflowService.ResetWorkflowAsync(id);

            if (!result.Success)
            {
                return BadRequest(result.ErrorMessage);
            }

            _logger.LogInformation("Manually triggered retry for document {DocumentId}", id);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrying document {DocumentId}", id);
            return StatusCode(500, "An error occurred while retrying document processing");
        }
    }

    /// <summary>
    /// Get list of documents ready for retry
    /// </summary>
    /// <param name="maxCount">Maximum number of documents to return</param>
    /// <returns>List of documents ready for retry</returns>
    [HttpGet("workflow/pending-retries")]
    [RequirePermission(Permissions.DocumentRead)]
    [ProducesResponseType(typeof(IEnumerable<Document>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetPendingRetries([FromQuery] int maxCount = 10)
    {
        try
        {
            var documents = await _workflowService.GetDocumentsReadyForRetryAsync(maxCount);
            _logger.LogInformation("Found {Count} documents ready for retry", documents.Count);
            return Ok(documents);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving documents ready for retry");
            return StatusCode(500, "An error occurred while retrieving pending retries");
        }
    }
}

// Request DTOs
public class UpdateVisibilityRequest
{
    public DocumentVisibility Visibility { get; set; }
}

public class ShareWithUserRequest
{
    public string UserId { get; set; } = string.Empty;
    public DocumentPermission Permission { get; set; } = DocumentPermission.Read;
}

public class ShareWithGroupRequest
{
    public int GroupId { get; set; }
    public DocumentPermission Permission { get; set; } = DocumentPermission.Read;
}

public class ConfirmMetadataRequest
{
    public string? ActualCategory { get; set; }
    public string? Notes { get; set; }
}
