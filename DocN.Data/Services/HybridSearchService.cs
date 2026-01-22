using DocN.Data.Models;
using DocN.Data.Utilities;
using DocN.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DocN.Data.Services;

/// <summary>
/// Options for hybrid search
/// </summary>
public class SearchOptions
{
    public int TopK { get; set; } = 10;
    public double MinSimilarity { get; set; } = 0.3;
    public string? CategoryFilter { get; set; }
    public string? OwnerId { get; set; }
    public DocumentVisibility? VisibilityFilter { get; set; }
    
    // Advanced filters
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public string? DocumentType { get; set; }
    public string? Author { get; set; }
    
    // Weight configuration for hybrid search
    public double VectorWeight { get; set; } = 0.5; // 0.0 to 1.0
    public double TextWeight { get; set; } = 0.5;   // 0.0 to 1.0
    
    // Search behavior options
    public bool UseBM25 { get; set; } = true;
    public bool UseSemanticCache { get; set; } = true;
    public bool EnableQueryExpansion { get; set; } = false;
}

/// <summary>
/// Search result with relevance scores
/// </summary>
public class SearchResult
{
    public Document Document { get; set; } = null!;
    public double VectorScore { get; set; }
    public double TextScore { get; set; }
    public double BM25Score { get; set; }
    public double CombinedScore { get; set; }
    public int? VectorRank { get; set; }
    public int? TextRank { get; set; }
    public bool FromSemanticCache { get; set; }
    public string? MatchedQuery { get; set; } // For semantic cache hits
}

/// <summary>
/// Service for hybrid search combining vector similarity and full-text search
/// </summary>
public interface IHybridSearchService
{
    /// <summary>
    /// Perform hybrid search combining vector and full-text search with Reciprocal Rank Fusion
    /// </summary>
    Task<List<SearchResult>> SearchAsync(string query, SearchOptions options);

    /// <summary>
    /// Perform vector-only search
    /// </summary>
    Task<List<SearchResult>> VectorSearchAsync(float[] queryEmbedding, SearchOptions options);

    /// <summary>
    /// Perform full-text search only
    /// </summary>
    Task<List<SearchResult>> TextSearchAsync(string query, SearchOptions options);
}

public class HybridSearchService : IHybridSearchService
{
    private readonly ApplicationDbContext _context;
    private readonly IEmbeddingService _embeddingService;
    private readonly DocN.Core.Interfaces.IBM25Service? _bm25Service;
    private readonly DocN.Core.Interfaces.ISemanticCacheService? _semanticCacheService;
    private readonly DocN.Core.Interfaces.IQueryRewritingService? _queryRewritingService;
    private readonly ILogger<HybridSearchService>? _logger;

    // Constants for vector search optimization
    private const int CandidateLimitMultiplier = 10; // Get 10x topK candidates for better results
    private const int MinCandidateLimit = 100; // Always get at least 100 candidates
    
    // SQL Server error codes for VECTOR type support detection
    private const int SqlErrorInvalidColumnName = 207; // Invalid column name (VECTOR columns don't exist)
    private const int SqlErrorInvalidDataType = 8116; // Argument data type is invalid (VECTOR type not recognized)

    public HybridSearchService(
        ApplicationDbContext context, 
        IEmbeddingService embeddingService,
        DocN.Core.Interfaces.IBM25Service? bm25Service = null,
        DocN.Core.Interfaces.ISemanticCacheService? semanticCacheService = null,
        DocN.Core.Interfaces.IQueryRewritingService? queryRewritingService = null,
        ILogger<HybridSearchService>? logger = null)
    {
        _context = context;
        _embeddingService = embeddingService;
        _bm25Service = bm25Service;
        _semanticCacheService = semanticCacheService;
        _queryRewritingService = queryRewritingService;
        _logger = logger;
    }

    /// <summary>
    /// Perform hybrid search combining vector similarity and text search
    /// </summary>
    public async Task<List<SearchResult>> SearchAsync(string query, SearchOptions options)
    {
        // 1. Generate query embedding once (will be used for both cache and search)
        var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query);
        
        // 2. Try semantic cache first if enabled and embedding was generated
        if (options.UseSemanticCache && _semanticCacheService != null && queryEmbedding != null)
        {
            var cachedResults = await _semanticCacheService.GetCachedResultsAsync<List<SearchResult>>(
                query, queryEmbedding, similarityThreshold: 0.95);
            
            if (cachedResults != null)
            {
                _logger?.LogInformation("Returning cached results for query: {Query}", TruncateQuery(query));
                return cachedResults;
            }
        }
        
        // 3. Apply query expansion if enabled
        string searchQuery = query;
        if (options.EnableQueryExpansion && _queryRewritingService != null)
        {
            try
            {
                searchQuery = await _queryRewritingService.ExpandQueryAsync(query);
                _logger?.LogDebug("Query expanded: '{Original}' → '{Expanded}'", query, searchQuery);
                
                // Re-generate embedding for expanded query
                queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(searchQuery);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Query expansion failed, using original query");
            }
        }
        
        // 4. Check if we have a valid embedding
        if (queryEmbedding == null)
        {
            // Fallback to text search only if embedding generation fails
            return await TextSearchAsync(searchQuery, options);
        }

        // 5. Perform vector search
        var vectorResults = await VectorSearchAsync(queryEmbedding, options);

        // 6. Perform full-text search (with BM25 if enabled)
        var textResults = await TextSearchAsync(searchQuery, options);

        // 7. Merge results using weighted fusion
        var merged = MergeWithWeightedFusion(vectorResults, textResults, options);
        
        // 8. Cache results if semantic cache is enabled
        if (options.UseSemanticCache && _semanticCacheService != null && queryEmbedding != null)
        {
            await _semanticCacheService.SetCachedResultsAsync(query, queryEmbedding, merged);
        }

        return merged;
    }

    /// <summary>
    /// Perform vector similarity search with database optimization using VECTOR_DISTANCE when available
    /// </summary>
    public async Task<List<SearchResult>> VectorSearchAsync(float[] queryEmbedding, SearchOptions options)
    {
        // Check if using SQL Server for optimization
        var isSqlServer = _context.Database.IsSqlServer();
        
        if (!isSqlServer)
        {
            // For non-SQL Server (e.g., in-memory testing), use fallback
            return await VectorSearchInMemoryAsync(queryEmbedding, options);
        }

        // Try to use SQL Server VECTOR_DISTANCE if available (SQL Server 2025+)
        try
        {
            return await VectorSearchWithVectorDistanceAsync(queryEmbedding, options);
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            // Check if error is due to VECTOR type not being supported (older SQL Server version)
            bool isVectorNotSupported = sqlEx.Number == SqlErrorInvalidColumnName || 
                                       sqlEx.Number == SqlErrorInvalidDataType;
            
            if (isVectorNotSupported)
            {
                // Fall back to in-memory calculation
                return await VectorSearchInMemoryAsync(queryEmbedding, options);
            }
            else
            {
                // Re-throw other SQL exceptions
                throw;
            }
        }
        catch (ArgumentException)
        {
            // Unsupported embedding dimension (not 768 or 1536) - fall back to in-memory calculation
            return await VectorSearchInMemoryAsync(queryEmbedding, options);
        }
        catch (Exception)
        {
            // For unexpected errors, fall back to in-memory calculation
            return await VectorSearchInMemoryAsync(queryEmbedding, options);
        }
    }

    /// <summary>
    /// Perform vector similarity search using SQL Server VECTOR_DISTANCE function
    /// This provides optimal performance by computing similarity at the database level
    /// </summary>
    private async Task<List<SearchResult>> VectorSearchWithVectorDistanceAsync(float[] queryEmbedding, SearchOptions options)
    {
        // Determine which vector field to use based on embedding dimension
        var embeddingDimension = queryEmbedding.Length;
        string docVectorColumn;
        
        // Use whitelist approach for security - only allow known valid column names
        if (embeddingDimension == Utilities.EmbeddingValidationHelper.SupportedDimension768)
        {
            docVectorColumn = "EmbeddingVector768";
        }
        else if (embeddingDimension == Utilities.EmbeddingValidationHelper.SupportedDimension1536)
        {
            docVectorColumn = "EmbeddingVector1536";
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

        // Build WHERE clause dynamically based on filters
        var whereConditions = new List<string> { $"d.{docVectorColumn} IS NOT NULL" };
        
        if (!string.IsNullOrEmpty(options.OwnerId))
        {
            whereConditions.Add("d.OwnerId = @ownerId");
        }
        
        if (!string.IsNullOrEmpty(options.CategoryFilter))
        {
            whereConditions.Add("d.ActualCategory = @categoryFilter");
        }
        
        if (options.VisibilityFilter.HasValue)
        {
            whereConditions.Add("d.Visibility = @visibilityFilter");
        }
        
        whereConditions.Add($"VECTOR_DISTANCE('cosine', d.{docVectorColumn}, CAST(@queryEmbedding AS VECTOR({embeddingDimension}))) >= @minSimilarity");
        
        var whereClause = string.Join(" AND ", whereConditions);

        // Use raw SQL with VECTOR_DISTANCE function
        var sql = $@"
            SELECT TOP (@topK)
                d.Id,
                d.FileName,
                d.FilePath,
                d.ContentType,
                d.FileSize,
                d.ExtractedText,
                d.ActualCategory,
                d.UploadedAt,
                d.OwnerId,
                d.Visibility,
                CAST(VECTOR_DISTANCE('cosine', d.{docVectorColumn}, CAST(@queryEmbedding AS VECTOR({embeddingDimension}))) AS FLOAT) AS SimilarityScore
            FROM Documents d
            WHERE {whereClause}
            ORDER BY SimilarityScore DESC";

        // Execute the query
        using var command = _context.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        
        var embeddingParam = command.CreateParameter();
        embeddingParam.ParameterName = "@queryEmbedding";
        embeddingParam.Value = embeddingJson;
        command.Parameters.Add(embeddingParam);
        
        var topKParam = command.CreateParameter();
        topKParam.ParameterName = "@topK";
        topKParam.Value = options.TopK * 2; // Return 2x TopK for fusion
        command.Parameters.Add(topKParam);
        
        var minSimParam = command.CreateParameter();
        minSimParam.ParameterName = "@minSimilarity";
        minSimParam.Value = options.MinSimilarity;
        command.Parameters.Add(minSimParam);
        
        if (!string.IsNullOrEmpty(options.OwnerId))
        {
            var ownerIdParam = command.CreateParameter();
            ownerIdParam.ParameterName = "@ownerId";
            ownerIdParam.Value = options.OwnerId;
            command.Parameters.Add(ownerIdParam);
        }
        
        if (!string.IsNullOrEmpty(options.CategoryFilter))
        {
            var categoryParam = command.CreateParameter();
            categoryParam.ParameterName = "@categoryFilter";
            categoryParam.Value = options.CategoryFilter;
            command.Parameters.Add(categoryParam);
        }
        
        if (options.VisibilityFilter.HasValue)
        {
            var visibilityParam = command.CreateParameter();
            visibilityParam.ParameterName = "@visibilityFilter";
            visibilityParam.Value = (int)options.VisibilityFilter.Value;
            command.Parameters.Add(visibilityParam);
        }

        await _context.Database.OpenConnectionAsync();

        var results = new List<SearchResult>();

        // Note: Connection is managed by Entity Framework and will be disposed with the DbContext
        using var reader = await command.ExecuteReaderAsync();
        int rank = 1;
        while (await reader.ReadAsync())
        {
            var doc = new Document
            {
                Id = reader.GetInt32(0),
                FileName = reader.GetString(1),
                FilePath = reader.GetString(2),
                ContentType = reader.GetString(3),
                FileSize = reader.GetInt64(4),
                ExtractedText = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                ActualCategory = reader.IsDBNull(6) ? null : reader.GetString(6),
                UploadedAt = reader.GetDateTime(7),
                OwnerId = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                Visibility = (DocumentVisibility)reader.GetInt32(9)
            };
            var score = reader.GetDouble(10);

            results.Add(new SearchResult
            {
                Document = doc,
                VectorScore = score,
                TextScore = 0,
                CombinedScore = score,
                VectorRank = rank++
            });
        }

        return results;
    }

    /// <summary>
    /// Fallback vector search using in-memory cosine similarity calculation
    /// Used when VECTOR_DISTANCE is not available (older SQL Server or in-memory database)
    /// </summary>
    private async Task<List<SearchResult>> VectorSearchInMemoryAsync(float[] queryEmbedding, SearchOptions options)
    {
        // Build query with filters
        // Query the actual mapped fields: EmbeddingVector768 or EmbeddingVector1536
        var documentsQuery = _context.Documents
            .Where(d => d.EmbeddingVector768 != null || d.EmbeddingVector1536 != null);

        // Apply filters
        if (!string.IsNullOrEmpty(options.CategoryFilter))
        {
            documentsQuery = documentsQuery.Where(d => d.ActualCategory == options.CategoryFilter);
        }

        if (!string.IsNullOrEmpty(options.OwnerId))
        {
            documentsQuery = documentsQuery.Where(d => d.OwnerId == options.OwnerId);
        }

        if (options.VisibilityFilter.HasValue)
        {
            documentsQuery = documentsQuery.Where(d => d.Visibility == options.VisibilityFilter.Value);
        }
        
        // Advanced filters
        if (options.DateFrom.HasValue)
        {
            documentsQuery = documentsQuery.Where(d => d.UploadedAt >= options.DateFrom.Value);
        }
        
        if (options.DateTo.HasValue)
        {
            documentsQuery = documentsQuery.Where(d => d.UploadedAt <= options.DateTo.Value);
        }
        
        if (!string.IsNullOrEmpty(options.DocumentType))
        {
            documentsQuery = documentsQuery.Where(d => d.ContentType == options.DocumentType);
        }
        
        // Note: Author filter is mapped to OwnerId as the Document model doesn't have a separate Author field
        if (!string.IsNullOrEmpty(options.Author))
        {
            documentsQuery = documentsQuery.Where(d => d.OwnerId == options.Author);
        }

        // Optimize: Limit candidates before loading into memory
        var candidateLimit = Math.Max(options.TopK * CandidateLimitMultiplier, MinCandidateLimit);
        documentsQuery = documentsQuery.OrderByDescending(d => d.UploadedAt).Take(candidateLimit);

        var documents = await documentsQuery.ToListAsync();

        // Calculate cosine similarity for each document
        var results = new List<SearchResult>();
        foreach (var doc in documents)
        {
            if (doc.EmbeddingVector == null) continue;

            var similarity = VectorMathHelper.CosineSimilarity(queryEmbedding, doc.EmbeddingVector);
            
            if (similarity >= options.MinSimilarity)
            {
                results.Add(new SearchResult
                {
                    Document = doc,
                    VectorScore = similarity,
                    TextScore = 0,
                    BM25Score = 0,
                    CombinedScore = similarity
                });
            }
        }

        // Sort by similarity and add ranks
        results = results.OrderByDescending(r => r.VectorScore).ToList();
        for (int i = 0; i < results.Count; i++)
        {
            results[i].VectorRank = i + 1;
        }

        return results.Take(options.TopK * 2).ToList(); // Return 2x TopK for fusion
    }

    /// <summary>
    /// Perform full-text search with improved case-insensitive matching and optional BM25 scoring
    /// </summary>
    public async Task<List<SearchResult>> TextSearchAsync(string query, SearchOptions options)
    {
        // Extract keywords from query
        var keywords = query.Split(new[] { ' ', ',', '.', ';', ':', '-', '_' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(k => k.Length > 1) // Filter out single characters
            .ToList();

        if (!keywords.Any())
        {
            return new List<SearchResult>();
        }

        var documentsQuery = _context.Documents.AsQueryable();

        // Apply filters
        if (!string.IsNullOrEmpty(options.CategoryFilter))
        {
            documentsQuery = documentsQuery.Where(d => d.ActualCategory == options.CategoryFilter);
        }

        if (!string.IsNullOrEmpty(options.OwnerId))
        {
            documentsQuery = documentsQuery.Where(d => d.OwnerId == options.OwnerId);
        }

        if (options.VisibilityFilter.HasValue)
        {
            documentsQuery = documentsQuery.Where(d => d.Visibility == options.VisibilityFilter.Value);
        }
        
        // Advanced filters
        if (options.DateFrom.HasValue)
        {
            documentsQuery = documentsQuery.Where(d => d.UploadedAt >= options.DateFrom.Value);
        }
        
        if (options.DateTo.HasValue)
        {
            documentsQuery = documentsQuery.Where(d => d.UploadedAt <= options.DateTo.Value);
        }
        
        if (!string.IsNullOrEmpty(options.DocumentType))
        {
            documentsQuery = documentsQuery.Where(d => d.ContentType == options.DocumentType);
        }
        
        // Note: Author filter is mapped to OwnerId as the Document model doesn't have a separate Author field
        if (!string.IsNullOrEmpty(options.Author))
        {
            documentsQuery = documentsQuery.Where(d => d.OwnerId == options.Author);
        }

        // Load documents with necessary fields
        var documents = await documentsQuery.ToListAsync();

        var results = new List<SearchResult>();
        
        // If BM25 is enabled and service is available, use it for better scoring
        if (options.UseBM25 && _bm25Service != null)
        {
            // Update BM25 statistics if needed
            var docTexts = documents.ToDictionary(d => d.Id, d => d.ExtractedText ?? "");
            _bm25Service.UpdateDocumentStatistics(docTexts);
            
            // Calculate BM25 scores
            var bm25Scores = _bm25Service.CalculateScores(query, docTexts);
            
            foreach (var (docId, score) in bm25Scores)
            {
                if (score > 0)
                {
                    var doc = documents.FirstOrDefault(d => d.Id == docId);
                    if (doc != null)
                    {
                        results.Add(new SearchResult
                        {
                            Document = doc,
                            VectorScore = 0,
                            TextScore = score,
                            BM25Score = score,
                            CombinedScore = score
                        });
                    }
                }
            }
        }
        else
        {
            // Use simple keyword matching
            foreach (var doc in documents)
            {
                // Count matches using case-insensitive comparison
                // Track unique keyword matches to avoid double-counting
                var matchedKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                double totalScore = 0;
                
                foreach (var keyword in keywords)
                {
                    bool keywordMatched = false;
                    double keywordScore = 0;
                    
                    // Check in FileName (highest weight: 1.0)
                    if (doc.FileName != null && doc.FileName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    {
                        keywordMatched = true;
                        keywordScore = Math.Max(keywordScore, 1.0);
                    }
                    
                    // Check in ExtractedText (medium weight: 0.8)
                    if (doc.ExtractedText != null && doc.ExtractedText.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    {
                        keywordMatched = true;
                        keywordScore = Math.Max(keywordScore, 0.8);
                    }
                    
                    // Check in ActualCategory (lowest weight: 0.5)
                    if (doc.ActualCategory != null && doc.ActualCategory.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    {
                        keywordMatched = true;
                        keywordScore = Math.Max(keywordScore, 0.5);
                    }
                    
                    if (keywordMatched)
                    {
                        matchedKeywords.Add(keyword);
                        totalScore += keywordScore;
                    }
                }
                
                if (matchedKeywords.Count > 0)
                {
                    // Calculate score:
                    // - Base score is the sum of weighted field scores (totalScore)
                    // - Normalized by total number of query keywords to get average match weight
                    // - Multiplied by keyword coverage ratio (matched keywords / total keywords)
                    // This rewards documents that match more keywords and match in higher-priority fields
                    var score = (totalScore / (double)keywords.Count) * ((double)matchedKeywords.Count / keywords.Count);
                    
                    results.Add(new SearchResult
                    {
                        Document = doc,
                        VectorScore = 0,
                        TextScore = score,
                        BM25Score = 0,
                        CombinedScore = score
                    });
                }
            }
        }

        // Sort by score and add ranks
        results = results.OrderByDescending(r => r.TextScore).ToList();
        for (int i = 0; i < results.Count; i++)
        {
            results[i].TextRank = i + 1;
        }

        return results.Take(options.TopK * 2).ToList(); // Return 2x TopK for fusion
    }

    /// <summary>
    /// Merge results using Reciprocal Rank Fusion (RRF)
    /// RRF formula: score = sum(1 / (k + rank)) for each ranking
    /// </summary>
    private List<SearchResult> MergeWithRRF(
        List<SearchResult> vectorResults,
        List<SearchResult> textResults,
        int topK,
        int k = 60)
    {
        var mergedScores = new Dictionary<int, SearchResult>();

        // Process vector results
        foreach (var result in vectorResults)
        {
            var docId = result.Document.Id;
            if (!mergedScores.ContainsKey(docId))
            {
                mergedScores[docId] = new SearchResult
                {
                    Document = result.Document,
                    VectorScore = result.VectorScore,
                    VectorRank = result.VectorRank,
                    TextScore = 0,
                    BM25Score = 0,
                    CombinedScore = 0
                };
            }
            
            // Add RRF score from vector ranking
            if (result.VectorRank.HasValue)
            {
                mergedScores[docId].CombinedScore += 1.0 / (k + result.VectorRank.Value);
            }
        }

        // Process text results
        foreach (var result in textResults)
        {
            var docId = result.Document.Id;
            if (!mergedScores.ContainsKey(docId))
            {
                mergedScores[docId] = new SearchResult
                {
                    Document = result.Document,
                    VectorScore = 0,
                    TextScore = result.TextScore,
                    BM25Score = result.BM25Score,
                    TextRank = result.TextRank,
                    CombinedScore = 0
                };
            }
            else
            {
                mergedScores[docId].TextScore = result.TextScore;
                mergedScores[docId].BM25Score = result.BM25Score;
                mergedScores[docId].TextRank = result.TextRank;
            }
            
            // Add RRF score from text ranking
            if (result.TextRank.HasValue)
            {
                mergedScores[docId].CombinedScore += 1.0 / (k + result.TextRank.Value);
            }
        }

        // Sort by combined RRF score and return top K
        return mergedScores.Values
            .OrderByDescending(r => r.CombinedScore)
            .Take(topK)
            .ToList();
    }
    
    /// <summary>
    /// Merge results using weighted fusion based on user-configured weights
    /// Allows fine-tuning the balance between semantic and keyword-based search
    /// </summary>
    private List<SearchResult> MergeWithWeightedFusion(
        List<SearchResult> vectorResults,
        List<SearchResult> textResults,
        SearchOptions options)
    {
        var mergedScores = new Dictionary<int, SearchResult>();
        
        // Normalize weights
        var totalWeight = options.VectorWeight + options.TextWeight;
        var normalizedVectorWeight = totalWeight > 0 ? options.VectorWeight / totalWeight : 0.5;
        var normalizedTextWeight = totalWeight > 0 ? options.TextWeight / totalWeight : 0.5;

        // Process vector results
        foreach (var result in vectorResults)
        {
            var docId = result.Document.Id;
            if (!mergedScores.ContainsKey(docId))
            {
                mergedScores[docId] = new SearchResult
                {
                    Document = result.Document,
                    VectorScore = result.VectorScore,
                    VectorRank = result.VectorRank,
                    TextScore = 0,
                    BM25Score = 0,
                    CombinedScore = 0
                };
            }
            
            // Add weighted vector score
            mergedScores[docId].CombinedScore += result.VectorScore * normalizedVectorWeight;
        }

        // Process text results
        foreach (var result in textResults)
        {
            var docId = result.Document.Id;
            if (!mergedScores.ContainsKey(docId))
            {
                mergedScores[docId] = new SearchResult
                {
                    Document = result.Document,
                    VectorScore = 0,
                    TextScore = result.TextScore,
                    BM25Score = result.BM25Score,
                    TextRank = result.TextRank,
                    CombinedScore = 0
                };
            }
            else
            {
                mergedScores[docId].TextScore = result.TextScore;
                mergedScores[docId].BM25Score = result.BM25Score;
                mergedScores[docId].TextRank = result.TextRank;
            }
            
            // Add weighted text score (BM25 or simple keyword matching)
            var textScore = result.BM25Score > 0 ? result.BM25Score : result.TextScore;
            mergedScores[docId].CombinedScore += textScore * normalizedTextWeight;
        }

        // Sort by combined weighted score and return top K
        return mergedScores.Values
            .OrderByDescending(r => r.CombinedScore)
            .Take(options.TopK)
            .ToList();
    }
    
    /// <summary>
    /// Truncate query for logging to prevent sensitive data exposure
    /// </summary>
    private string TruncateQuery(string query)
    {
        return query.Length > 50 ? query.Substring(0, 47) + "..." : query;
    }
}
