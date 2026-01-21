using DocN.Core.Interfaces;
using DocN.Data.Models;
using DocN.Data.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DocN.Data.Services;

/// <summary>
/// No-op implementation of ISemanticRAGService for when AI services are not configured
/// Provides similarity search capabilities using existing embeddings in the database
/// </summary>
public class NoOpSemanticRAGService : ISemanticRAGService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<NoOpSemanticRAGService> _logger;

    public NoOpSemanticRAGService(
        ApplicationDbContext context,
        ILogger<NoOpSemanticRAGService> logger)
    {
        _context = context;
        _logger = logger;
    }
    public Task<SemanticRAGResponse> GenerateResponseAsync(
        string query, 
        string userId, 
        int? conversationId = null, 
        List<int>? specificDocumentIds = null, 
        int topK = 5)
    {
        return Task.FromResult(new SemanticRAGResponse
        {
            Answer = "AI services are not configured. Please configure Azure OpenAI or OpenAI in appsettings.json.",
            SourceDocuments = new List<RelevantDocumentResult>(),
            Metadata = new Dictionary<string, object>
            {
                { "error", "AI services not configured" }
            }
        });
    }

    public async IAsyncEnumerable<string> GenerateStreamingResponseAsync(
        string query, 
        string userId, 
        int? conversationId = null, 
        List<int>? specificDocumentIds = null)
    {
        yield return "AI services are not configured. Please configure Azure OpenAI or OpenAI in appsettings.json.";
        await Task.CompletedTask;
    }

    public Task<List<RelevantDocumentResult>> SearchDocumentsAsync(
        string query, 
        string userId, 
        int topK = 10, 
        double minSimilarity = 0.7)
    {
        // Return empty list when AI services are not configured
        return Task.FromResult(new List<RelevantDocumentResult>());
    }

    public async Task<List<RelevantDocumentResult>> SearchDocumentsWithEmbeddingAsync(
        float[] queryEmbedding,
        string userId,
        int topK = 10,
        double minSimilarity = 0.7)
    {
        try
        {
            _logger.LogDebug("Searching documents with pre-generated embedding for user: {UserId} (NoOp mode)", userId);

            if (queryEmbedding == null || queryEmbedding.Length == 0)
            {
                _logger.LogWarning("Query embedding is null or empty");
                return new List<RelevantDocumentResult>();
            }

            // Check if using SQL Server for optimization
            var isSqlServer = _context.Database.IsSqlServer();
            
            if (isSqlServer)
            {
                // Try to use SQL Server VECTOR_DISTANCE if available (SQL Server 2025+)
                try
                {
                    return await SearchDocumentsWithVectorDistanceAsync(queryEmbedding, userId, topK, minSimilarity);
                }
                catch (Microsoft.Data.SqlClient.SqlException sqlEx)
                {
                    // SQL error codes for VECTOR type support detection
                    const int SqlErrorInvalidColumnName = 207; // Invalid column name
                    const int SqlErrorInvalidDataType = 8116; // Argument data type is invalid
                    
                    // Check if error is due to VECTOR type not being supported (older SQL Server version)
                    bool isVectorNotSupported = sqlEx.Number == SqlErrorInvalidColumnName || 
                                               sqlEx.Number == SqlErrorInvalidDataType;
                    
                    if (isVectorNotSupported)
                    {
                        _logger.LogInformation(
                            "SQL Server VECTOR_DISTANCE not available (SQL error {ErrorNumber}). " +
                            "Falling back to in-memory calculation. Consider upgrading to SQL Server 2025 for better performance.",
                            sqlEx.Number);
                        // Fall back to in-memory calculation
                        return await SearchDocumentsInMemoryAsync(queryEmbedding, userId, topK, minSimilarity);
                    }
                    else
                    {
                        _logger.LogWarning(sqlEx, "SQL error during VECTOR_DISTANCE search (error {ErrorNumber}), falling back to in-memory calculation", 
                            sqlEx.Number);
                        return await SearchDocumentsInMemoryAsync(queryEmbedding, userId, topK, minSimilarity);
                    }
                }
                catch (ArgumentException argEx)
                {
                    _logger.LogInformation(argEx, "VECTOR_DISTANCE requires 768 or 1536 dimensions, got {Dimension}. Falling back to in-memory calculation.", 
                        queryEmbedding.Length);
                    return await SearchDocumentsInMemoryAsync(queryEmbedding, userId, topK, minSimilarity);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Unexpected error during VECTOR_DISTANCE search, falling back to in-memory calculation");
                    return await SearchDocumentsInMemoryAsync(queryEmbedding, userId, topK, minSimilarity);
                }
            }
            else
            {
                // For non-SQL Server (e.g., in-memory testing), use fallback
                return await SearchDocumentsInMemoryAsync(queryEmbedding, userId, topK, minSimilarity);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching documents with embedding for user: {UserId}", userId);
            return new List<RelevantDocumentResult>();
        }
    }

    /// <summary>
    /// Perform vector search using SQL Server VECTOR_DISTANCE function for optimal performance
    /// </summary>
    private async Task<List<RelevantDocumentResult>> SearchDocumentsWithVectorDistanceAsync(
        float[] queryEmbedding,
        string userId,
        int topK,
        double minSimilarity)
    {
        // Determine which vector field to use based on embedding dimension
        var embeddingDimension = queryEmbedding.Length;
        string docVectorColumn, chunkVectorColumn;
        
        // Use whitelist approach for security - only allow known valid column names
        if (embeddingDimension == Utilities.EmbeddingValidationHelper.SupportedDimension768)
        {
            docVectorColumn = "EmbeddingVector768";
            chunkVectorColumn = "ChunkEmbedding768";
        }
        else if (embeddingDimension == Utilities.EmbeddingValidationHelper.SupportedDimension1536)
        {
            docVectorColumn = "EmbeddingVector1536";
            chunkVectorColumn = "ChunkEmbedding1536";
        }
        else
        {
            throw new ArgumentException(
                $"Unsupported embedding dimension: {embeddingDimension}. " +
                $"Expected {Utilities.EmbeddingValidationHelper.SupportedDimension768} or " +
                $"{Utilities.EmbeddingValidationHelper.SupportedDimension1536}.");
        }
        
        // Serialize query embedding to JSON format (required for VECTOR type)
        var embeddingJson = System.Text.Json.JsonSerializer.Serialize(queryEmbedding);

        // Use raw SQL with VECTOR_DISTANCE function for document-level search
        var docSql = $@"
            SELECT TOP (@topK)
                d.Id,
                d.FileName,
                d.ActualCategory,
                d.ExtractedText,
                CAST(VECTOR_DISTANCE('cosine', d.{docVectorColumn}, CAST(@queryEmbedding AS VECTOR({embeddingDimension}))) AS FLOAT) AS SimilarityScore
            FROM Documents d
            WHERE d.OwnerId = @userId
                AND d.{docVectorColumn} IS NOT NULL
                AND VECTOR_DISTANCE('cosine', d.{docVectorColumn}, CAST(@queryEmbedding AS VECTOR({embeddingDimension}))) >= @minSimilarity
            ORDER BY SimilarityScore DESC";

        // Use raw SQL with VECTOR_DISTANCE function for chunk-level search
        var chunkSql = $@"
            SELECT TOP (@topK)
                dc.DocumentId,
                dc.ChunkText,
                dc.ChunkIndex,
                d.FileName,
                d.ActualCategory,
                CAST(VECTOR_DISTANCE('cosine', dc.{chunkVectorColumn}, CAST(@queryEmbedding AS VECTOR({embeddingDimension}))) AS FLOAT) AS SimilarityScore
            FROM DocumentChunks dc
            INNER JOIN Documents d ON dc.DocumentId = d.Id
            WHERE d.OwnerId = @userId
                AND dc.{chunkVectorColumn} IS NOT NULL
                AND VECTOR_DISTANCE('cosine', dc.{chunkVectorColumn}, CAST(@queryEmbedding AS VECTOR({embeddingDimension}))) >= @minSimilarity
            ORDER BY SimilarityScore DESC";

        var results = new List<RelevantDocumentResult>();
        var existingDocIds = new HashSet<int>();

        await _context.Database.OpenConnectionAsync();
        var connection = _context.Database.GetDbConnection();

        // Execute chunk-level search (higher priority)
        using (var chunkCommand = connection.CreateCommand())
        {
            chunkCommand.CommandText = chunkSql;
            
            var embeddingParam = chunkCommand.CreateParameter();
            embeddingParam.ParameterName = "@queryEmbedding";
            embeddingParam.Value = embeddingJson;
            chunkCommand.Parameters.Add(embeddingParam);
            
            var topKParam = chunkCommand.CreateParameter();
            topKParam.ParameterName = "@topK";
            topKParam.Value = topK;
            chunkCommand.Parameters.Add(topKParam);
            
            var minSimParam = chunkCommand.CreateParameter();
            minSimParam.ParameterName = "@minSimilarity";
            minSimParam.Value = minSimilarity;
            chunkCommand.Parameters.Add(minSimParam);
            
            var userIdParam = chunkCommand.CreateParameter();
            userIdParam.ParameterName = "@userId";
            userIdParam.Value = userId;
            chunkCommand.Parameters.Add(userIdParam);

            using var chunkReader = await chunkCommand.ExecuteReaderAsync();
            while (await chunkReader.ReadAsync())
            {
                var documentId = chunkReader.GetInt32(0);
                var chunkText = chunkReader.GetString(1);
                var chunkIndex = chunkReader.GetInt32(2);
                var fileName = chunkReader.GetString(3);
                var category = chunkReader.IsDBNull(4) ? null : chunkReader.GetString(4);
                var score = chunkReader.GetDouble(5);

                results.Add(new RelevantDocumentResult
                {
                    DocumentId = documentId,
                    FileName = fileName,
                    Category = category,
                    SimilarityScore = score,
                    RelevantChunk = chunkText,
                    ChunkIndex = chunkIndex
                });
                existingDocIds.Add(documentId);
            }
        }

        // Add document-level results if we don't have enough chunks
        if (results.Count < topK)
        {
            var remaining = topK - results.Count;
            
            using (var docCommand = connection.CreateCommand())
            {
                docCommand.CommandText = docSql;
                
                var embeddingParam = docCommand.CreateParameter();
                embeddingParam.ParameterName = "@queryEmbedding";
                embeddingParam.Value = embeddingJson;
                docCommand.Parameters.Add(embeddingParam);
                
                var topKParam = docCommand.CreateParameter();
                topKParam.ParameterName = "@topK";
                topKParam.Value = remaining * 2; // Get extra to filter out duplicates
                docCommand.Parameters.Add(topKParam);
                
                var minSimParam = docCommand.CreateParameter();
                minSimParam.ParameterName = "@minSimilarity";
                minSimParam.Value = minSimilarity;
                docCommand.Parameters.Add(minSimParam);
                
                var userIdParam = docCommand.CreateParameter();
                userIdParam.ParameterName = "@userId";
                userIdParam.Value = userId;
                docCommand.Parameters.Add(userIdParam);

                using var docReader = await docCommand.ExecuteReaderAsync();
                while (await docReader.ReadAsync() && results.Count < topK)
                {
                    var documentId = docReader.GetInt32(0);
                    
                    // Skip if already included from chunks
                    if (existingDocIds.Contains(documentId))
                        continue;

                    var fileName = docReader.GetString(1);
                    var category = docReader.IsDBNull(2) ? null : docReader.GetString(2);
                    var extractedText = docReader.IsDBNull(3) ? null : docReader.GetString(3);
                    var score = docReader.GetDouble(4);

                    results.Add(new RelevantDocumentResult
                    {
                        DocumentId = documentId,
                        FileName = fileName,
                        Category = category,
                        SimilarityScore = score,
                        ExtractedText = extractedText
                    });
                    existingDocIds.Add(documentId);
                }
            }
        }

        _logger.LogInformation(
            "✅ SQL Server VECTOR_DISTANCE search completed successfully using {VectorField} ({Dimension} dimensions): found {Count} results above {MinSim:P0} threshold", 
            docVectorColumn, embeddingDimension, results.Count, minSimilarity);

        return results;
    }

    /// <summary>
    /// Fallback vector search using in-memory cosine similarity calculation
    /// Used when VECTOR_DISTANCE is not available (older SQL Server or non-SQL Server database)
    /// </summary>
    private async Task<List<RelevantDocumentResult>> SearchDocumentsInMemoryAsync(
        float[] queryEmbedding,
        string userId,
        int topK,
        double minSimilarity)
    {
        // Get all documents with embeddings for the user
        // Query the actual mapped fields: EmbeddingVector768 or EmbeddingVector1536
        var documents = await _context.Documents
            .Where(d => d.OwnerId == userId && (d.EmbeddingVector768 != null || d.EmbeddingVector1536 != null))
            .ToListAsync();

        _logger.LogInformation("Found {Count} documents with embeddings for user {UserId}", documents.Count, userId);
        
        // Calculate similarity scores for documents
        var scoredDocs = new List<(Document doc, double score)>();
        foreach (var doc in documents)
        {
            // Use the EmbeddingVector property getter which returns the populated field
            var docEmbedding = doc.EmbeddingVector;
            if (docEmbedding == null) continue;

            var similarity = VectorMathHelper.CosineSimilarity(queryEmbedding, docEmbedding);
            if (similarity >= minSimilarity)
            {
                scoredDocs.Add((doc, similarity));
            }
        }

        _logger.LogInformation("Found {Count} documents above similarity threshold {Threshold:P0}", scoredDocs.Count, minSimilarity);

        // Get chunks for better precision
        // Query the actual mapped fields: ChunkEmbedding768 or ChunkEmbedding1536
        var chunks = await _context.DocumentChunks
            .Include(c => c.Document)
            .Where(c => c.Document!.OwnerId == userId && (c.ChunkEmbedding768 != null || c.ChunkEmbedding1536 != null))
            .ToListAsync();

        var scoredChunks = new List<(DocumentChunk chunk, double score)>();
        foreach (var chunk in chunks)
        {
            // Use the ChunkEmbedding property getter which returns the populated field
            var chunkEmbedding = chunk.ChunkEmbedding;
            if (chunkEmbedding == null) continue;

            var similarity = VectorMathHelper.CosineSimilarity(queryEmbedding, chunkEmbedding);
            if (similarity >= minSimilarity)
            {
                scoredChunks.Add((chunk, similarity));
            }
        }

        // Combine document-level and chunk-level results
        var results = new List<RelevantDocumentResult>();
        
        // Add chunk-based results (higher priority)
        var topChunks = scoredChunks.OrderByDescending(x => x.score).Take(topK).ToList();
        var existingDocIds = new HashSet<int>();
        
        foreach (var (chunk, score) in topChunks)
        {
            if (chunk.Document == null) continue;

            results.Add(new RelevantDocumentResult
            {
                DocumentId = chunk.DocumentId,
                FileName = chunk.Document.FileName,
                Category = chunk.Document.ActualCategory,
                SimilarityScore = score,
                RelevantChunk = chunk.ChunkText,
                ChunkIndex = chunk.ChunkIndex
            });
            existingDocIds.Add(chunk.DocumentId);
        }

        // Add document-level results if we don't have enough chunks
        if (results.Count < topK)
        {
            foreach (var (doc, score) in scoredDocs.OrderByDescending(x => x.score))
            {
                // Stop if we've reached topK results
                if (results.Count >= topK)
                    break;
                    
                // Avoid duplicates
                if (existingDocIds.Contains(doc.Id))
                    continue;

                results.Add(new RelevantDocumentResult
                {
                    DocumentId = doc.Id,
                    FileName = doc.FileName,
                    Category = doc.ActualCategory,
                    SimilarityScore = score,
                    ExtractedText = doc.ExtractedText
                });
                existingDocIds.Add(doc.Id);
            }
        }

        _logger.LogDebug("Returning {Count} total results (in-memory calculation)", results.Count);
        return results;
    }

}
