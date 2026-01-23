using DocN.Data.Models;
using DocN.Data.Utilities;
using Microsoft.EntityFrameworkCore;

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
    
    // Advanced filtering options
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public string? DocumentType { get; set; }
    public string? Author { get; set; }
    
    // Search strategy configuration
    public bool UseBM25 { get; set; } = true;
    public double VectorWeight { get; set; } = 0.5; // Weight for vector search (0-1)
    public double TextWeight { get; set; } = 0.5; // Weight for text/BM25 search (0-1)
    public bool EnableQueryExpansion { get; set; } = false;
    public bool EnableSemanticCache { get; set; } = false;
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
    public int? BM25Rank { get; set; }
    public Dictionary<string, int>? TermFrequencies { get; set; }
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
    private readonly IBM25SearchService? _bm25SearchService;

    // Constants for vector search optimization
    private const int CandidateLimitMultiplier = 10; // Get 10x topK candidates for better results
    private const int MinCandidateLimit = 100; // Always get at least 100 candidates
    
    // SQL Server error codes for VECTOR type support detection
    private const int SqlErrorInvalidColumnName = 207; // Invalid column name (VECTOR columns don't exist)
    private const int SqlErrorInvalidDataType = 8116; // Argument data type is invalid (VECTOR type not recognized)

    public HybridSearchService(
        ApplicationDbContext context, 
        IEmbeddingService embeddingService,
        IBM25SearchService? bm25SearchService = null)
    {
        _context = context;
        _embeddingService = embeddingService;
        _bm25SearchService = bm25SearchService;
    }

    /// <summary>
    /// Perform hybrid search combining vector similarity and text search
    /// </summary>
    public async Task<List<SearchResult>> SearchAsync(string query, SearchOptions options)
    {
        // 1. Generate query embedding
        var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query);
        if (queryEmbedding == null)
        {
            // Fallback to text search only if embedding generation fails
            return await TextSearchAsync(query, options);
        }

        // 2. Perform vector search
        var vectorResults = await VectorSearchAsync(queryEmbedding, options);

        // 3. Perform text search (BM25 or simple text search)
        List<SearchResult> textResults;
        if (options.UseBM25 && _bm25SearchService != null)
        {
            var bm25Results = await _bm25SearchService.SearchAsync(
                query, 
                options.TopK * 2, 
                options.CategoryFilter, 
                options.OwnerId);
            
            textResults = bm25Results.Select(r => new SearchResult
            {
                Document = r.Document,
                VectorScore = 0,
                TextScore = 0,
                BM25Score = r.Score,
                CombinedScore = r.Score,
                TermFrequencies = r.TermFrequencies
            }).ToList();
            
            // Add BM25 ranks
            for (int i = 0; i < textResults.Count; i++)
            {
                textResults[i].BM25Rank = i + 1;
            }
        }
        else
        {
            textResults = await TextSearchAsync(query, options);
        }

        // 4. Merge results using weighted Reciprocal Rank Fusion (RRF)
        var merged = MergeWithWeightedRRF(vectorResults, textResults, options);

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

        // Date filters
        if (options.DateFrom.HasValue)
        {
            documentsQuery = documentsQuery.Where(d => d.UploadedAt >= options.DateFrom.Value);
        }

        if (options.DateTo.HasValue)
        {
            documentsQuery = documentsQuery.Where(d => d.UploadedAt <= options.DateTo.Value);
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
    /// Perform full-text search with improved case-insensitive matching
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

        // Load documents with necessary fields
        var documents = await documentsQuery.ToListAsync();

        var results = new List<SearchResult>();
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
                    CombinedScore = score
                });
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
    /// Merge results using weighted Reciprocal Rank Fusion (RRF)
    /// Allows configurable weights for vector vs text search
    /// </summary>
    private List<SearchResult> MergeWithWeightedRRF(
        List<SearchResult> vectorResults,
        List<SearchResult> textResults,
        SearchOptions options,
        int k = 60)
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
            
            // Add weighted RRF score from vector ranking
            if (result.VectorRank.HasValue)
            {
                mergedScores[docId].CombinedScore += normalizedVectorWeight * (1.0 / (k + result.VectorRank.Value));
            }
        }

        // Process text/BM25 results
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
                    BM25Rank = result.BM25Rank,
                    TermFrequencies = result.TermFrequencies,
                    CombinedScore = 0
                };
            }
            else
            {
                mergedScores[docId].TextScore = result.TextScore;
                mergedScores[docId].BM25Score = result.BM25Score;
                mergedScores[docId].TextRank = result.TextRank;
                mergedScores[docId].BM25Rank = result.BM25Rank;
                mergedScores[docId].TermFrequencies = result.TermFrequencies;
            }
            
            // Add weighted RRF score from text/BM25 ranking
            var rank = result.BM25Rank ?? result.TextRank;
            if (rank.HasValue)
            {
                mergedScores[docId].CombinedScore += normalizedTextWeight * (1.0 / (k + rank.Value));
            }
        }

        // Apply date filters if specified
        if (options.DateFrom.HasValue || options.DateTo.HasValue)
        {
            mergedScores = mergedScores
                .Where(kvp =>
                {
                    var doc = kvp.Value.Document;
                    if (options.DateFrom.HasValue && doc.UploadedAt < options.DateFrom.Value)
                        return false;
                    if (options.DateTo.HasValue && doc.UploadedAt > options.DateTo.Value)
                        return false;
                    return true;
                })
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        // Sort by combined RRF score and return top K
        return mergedScores.Values
            .OrderByDescending(r => r.CombinedScore)
            .Take(options.TopK)
            .ToList();
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
                    TextRank = result.TextRank,
                    CombinedScore = 0
                };
            }
            else
            {
                mergedScores[docId].TextScore = result.TextScore;
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
}
