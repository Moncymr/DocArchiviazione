using DocN.Data.Constants;
using DocN.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace DocN.Data.Services;

public interface IDocumentStatisticsService
{
    Task<DocumentStatistics> GetStatisticsAsync(string userId);
    Task UpdateDocumentAccessAsync(int documentId);
}

public class DocumentStatisticsService : IDocumentStatisticsService
{
    private readonly ApplicationDbContext _context;

    public DocumentStatisticsService(ApplicationDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<DocumentStatistics> GetStatisticsAsync(string userId)
    {
        // Use the same logic as DocumentService.GetUserDocumentsAsync to get accessible documents
        IQueryable<Document> userDocs;
        
        if (string.IsNullOrEmpty(userId))
        {
            // If no user ID, get only public documents
            userDocs = _context.Documents.Where(d => d.Visibility == DocumentVisibility.Public);
        }
        else
        {
            // Get user's tenant once upfront to avoid repeated queries
            // Use projection to fetch only TenantId rather than the entire user entity
            var userTenantId = await _context.Users
                .Where(u => u.Id == userId)
                .Select(u => u.TenantId)
                .FirstOrDefaultAsync();
            
            // Get documents owned by user, shared with user, OR in the same tenant (if user has a tenant)
            // Use Include to avoid N+1 query issues with Shares
            userDocs = _context.Documents
                .Include(d => d.Shares)
                .Where(d => 
                    d.OwnerId == userId ||  // Owned by user
                    d.Shares.Any(s => s.SharedWithUserId == userId) ||  // Shared with user
                    (userTenantId != null && d.TenantId == userTenantId) || // Same tenant - see all docs in tenant
                    (d.OwnerId == null && d.Visibility == DocumentVisibility.Public) // Public documents without owner
                );
        }
        
        var totalDocs = await userDocs.CountAsync();
        var totalStorage = await userDocs.SumAsync(d => (long?)d.FileSize) ?? 0;
        
        var today = DateTime.UtcNow.Date;
        var weekAgo = today.AddDays(-7);
        var monthAgo = today.AddMonths(-1);
        
        var docsToday = await userDocs.CountAsync(d => d.UploadedAt >= today);
        var docsThisWeek = await userDocs.CountAsync(d => d.UploadedAt >= weekAgo);
        var docsThisMonth = await userDocs.CountAsync(d => d.UploadedAt >= monthAgo);
        
        // Documents by category
        var docsByCategory = await userDocs
            .Where(d => !string.IsNullOrEmpty(d.ActualCategory))
            .GroupBy(d => d.ActualCategory!)
            .Select(g => new { Category = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Category, x => x.Count);
        
        // Documents by type (file extension)
        var docsByType = await userDocs
            .GroupBy(d => d.ContentType)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Type, x => x.Count);
        
        // Most accessed documents
        var topDocs = await userDocs
            .OrderByDescending(d => d.AccessCount)
            .Take(10)
            .Select(d => new TopDocument
            {
                Id = d.Id,
                FileName = d.FileName,
                AccessCount = d.AccessCount,
                LastAccessedAt = d.LastAccessedAt ?? d.UploadedAt
            })
            .ToListAsync();
        
        // Optimization suggestions
        var optimizations = GenerateOptimizationSuggestions(docsByCategory, totalDocs);
        
        // Embedding Queue Statistics - count all documents regardless of user
        var pendingCount = await _context.Documents
            .CountAsync(d => d.ChunkEmbeddingStatus == ChunkEmbeddingStatus.Pending);
        
        var processingCount = await _context.Documents
            .CountAsync(d => d.ChunkEmbeddingStatus == ChunkEmbeddingStatus.Processing);
        
        var completedCount = await _context.Documents
            .CountAsync(d => d.ChunkEmbeddingStatus == ChunkEmbeddingStatus.Completed);
        
        // Count chunks without embeddings - ONLY for documents that are Pending or Processing
        // This ensures we only count chunks that are actively in the processing queue
        var pendingChunksCount = await _context.DocumentChunks
            .Include(c => c.Document)
            .Where(c => (c.ChunkEmbedding768 == null && c.ChunkEmbedding1536 == null) &&
                       (c.Document!.ChunkEmbeddingStatus == ChunkEmbeddingStatus.Pending || 
                        c.Document!.ChunkEmbeddingStatus == ChunkEmbeddingStatus.Processing))
            .CountAsync();
        
        // For documents in Pending status that don't have any chunks yet, estimate their chunks
        // Average document has ~15 chunks based on typical PDF documents
        const int ESTIMATED_CHUNKS_PER_DOCUMENT = 15; // Can be adjusted based on actual statistics
        
        // Note: This query uses a nested ANY which EF Core translates to NOT EXISTS in SQL
        // Performance is acceptable for current scale. If needed, can be optimized with a LEFT JOIN approach.
        var documentsWithoutChunks = await _context.Documents
            .Where(d => d.ChunkEmbeddingStatus == ChunkEmbeddingStatus.Pending &&
                       !_context.DocumentChunks.Any(c => c.DocumentId == d.Id))
            .CountAsync();
        
        // Add estimated chunks for documents that haven't been chunked yet
        var estimatedPendingChunks = documentsWithoutChunks * ESTIMATED_CHUNKS_PER_DOCUMENT;
        var totalPendingChunks = pendingChunksCount + estimatedPendingChunks;
        
        // Calculate estimated processing time
        // Based on observations: ~2-4 seconds per chunk with Gemini
        // BatchEmbeddingProcessor processes 5 documents at a time every 30 seconds
        var estimatedTimeMinutes = CalculateEstimatedProcessingTime(pendingCount, totalPendingChunks);
        
        return new DocumentStatistics
        {
            TotalDocuments = totalDocs,
            TotalStorageBytes = totalStorage,
            DocumentsUploadedToday = docsToday,
            DocumentsUploadedThisWeek = docsThisWeek,
            DocumentsUploadedThisMonth = docsThisMonth,
            DocumentsByCategory = docsByCategory,
            DocumentsByType = docsByType,
            MostAccessedDocuments = topDocs,
            OptimizationSuggestions = optimizations,
            PendingEmbeddingsCount = pendingCount,
            ProcessingEmbeddingsCount = processingCount,
            CompletedEmbeddingsCount = completedCount,
            PendingChunksCount = totalPendingChunks,
            EstimatedProcessingTimeMinutes = estimatedTimeMinutes,
            LastBatchProcessingTime = DateTime.UtcNow // Will be updated by background processor in future
        };
    }

    public async Task UpdateDocumentAccessAsync(int documentId)
    {
        var document = await _context.Documents.FindAsync(documentId);
        if (document != null)
        {
            document.AccessCount++;
            document.LastAccessedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    private List<CategoryOptimization> GenerateOptimizationSuggestions(
        Dictionary<string, int> docsByCategory, int totalDocs)
    {
        var suggestions = new List<CategoryOptimization>();
        
        // Find overcrowded categories
        foreach (var category in docsByCategory)
        {
            var percentage = (category.Value * 100.0) / totalDocs;
            
            if (percentage > 40)
            {
                suggestions.Add(new CategoryOptimization
                {
                    Category = category.Key,
                    DocumentCount = category.Value,
                    Suggestion = "Consider creating sub-categories",
                    Reason = $"This category contains {percentage:F1}% of all documents and may benefit from further organization"
                });
            }
            else if (category.Value < 3 && totalDocs > 20)
            {
                suggestions.Add(new CategoryOptimization
                {
                    Category = category.Key,
                    DocumentCount = category.Value,
                    Suggestion = "Consider merging with similar category",
                    Reason = "Low document count - may be too specific"
                });
            }
        }
        
        return suggestions;
    }
    
    /// <summary>
    /// Calculate estimated processing time based on pending documents and chunks
    /// </summary>
    /// <param name="pendingDocs">Number of documents waiting for processing</param>
    /// <param name="pendingChunks">Number of chunks without embeddings</param>
    /// <returns>Estimated time in minutes</returns>
    private double CalculateEstimatedProcessingTime(int pendingDocs, int pendingChunks)
    {
        // Based on real-world observations:
        // - Average simple PDF: 10-20 chunks
        // - Gemini embedding generation: ~2-4 seconds per chunk
        // - BatchEmbeddingProcessor: processes 5 documents at a time, runs every 30 seconds
        
        const double AVG_SECONDS_PER_CHUNK = 3.0; // Average time to generate embedding for one chunk
        const int BATCH_SIZE = 5; // Documents processed per batch
        const int BATCH_INTERVAL_SECONDS = 30; // How often the processor runs
        
        // If we have pending chunks, use that for estimation
        if (pendingChunks > 0)
        {
            // Direct estimation based on chunks
            var totalProcessingTimeSeconds = pendingChunks * AVG_SECONDS_PER_CHUNK;
            
            // Add batch interval overhead (processor runs every 30s)
            var batchesNeeded = Math.Ceiling(pendingDocs / (double)BATCH_SIZE);
            var batchOverheadSeconds = batchesNeeded * BATCH_INTERVAL_SECONDS;
            
            return (totalProcessingTimeSeconds + batchOverheadSeconds) / 60.0; // Convert to minutes
        }
        
        // If no chunks info, estimate based on pending documents
        if (pendingDocs > 0)
        {
            const double AVG_CHUNKS_PER_DOC = 15.0; // Average chunks per document
            var estimatedChunks = pendingDocs * AVG_CHUNKS_PER_DOC;
            var totalProcessingTimeSeconds = estimatedChunks * AVG_SECONDS_PER_CHUNK;
            
            var batchesNeeded = Math.Ceiling(pendingDocs / (double)BATCH_SIZE);
            var batchOverheadSeconds = batchesNeeded * BATCH_INTERVAL_SECONDS;
            
            return (totalProcessingTimeSeconds + batchOverheadSeconds) / 60.0;
        }
        
        return 0; // No pending work
    }
}
