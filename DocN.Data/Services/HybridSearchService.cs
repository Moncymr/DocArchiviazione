using DocN.Data.Models;
using DocN.Data.Utilities;
using Microsoft.EntityFrameworkCore;

namespace DocN.Data.Services;

/// <summary>
/// Opzioni di configurazione per ricerca ibrida.
/// </summary>
public class SearchOptions
{
    /// <summary>Numero massimo risultati da restituire (default 10)</summary>
    public int TopK { get; set; } = 10;
    
    /// <summary>Soglia minima di similarità (0-1, default 0.3)</summary>
    public double MinSimilarity { get; set; } = 0.3;
    
    /// <summary>Filtro opzionale per categoria documento</summary>
    public string? CategoryFilter { get; set; }
    
    /// <summary>Filtro opzionale per owner/proprietario documento</summary>
    public string? OwnerId { get; set; }
    
    /// <summary>Filtro opzionale per visibilità documento</summary>
    public DocumentVisibility? VisibilityFilter { get; set; }
}

/// <summary>
/// Risultato di ricerca con score di rilevanza multipli.
/// </summary>
public class SearchResult
{
    /// <summary>Documento trovato</summary>
    public Document Document { get; set; } = null!;
    
    /// <summary>Score similarità vettoriale (0-1, cosine similarity)</summary>
    public double VectorScore { get; set; }
    
    /// <summary>Score ricerca testuale (ponderato per campi)</summary>
    public double TextScore { get; set; }
    
    /// <summary>Score combinato finale (RRF per ricerca ibrida)</summary>
    public double CombinedScore { get; set; }
    
    /// <summary>Ranking nella ricerca vettoriale (1-based)</summary>
    public int? VectorRank { get; set; }
    
    /// <summary>Ranking nella ricerca testuale (1-based)</summary>
    public int? TextRank { get; set; }
}

/// <summary>
/// Interfaccia servizio per ricerca ibrida combinando similarità vettoriale e full-text search.
/// </summary>
public interface IHybridSearchService
{
    /// <summary>
    /// Esegue ricerca ibrida combinando vector e full-text search con Reciprocal Rank Fusion.
    /// </summary>
    /// <param name="query">Query utente in linguaggio naturale</param>
    /// <param name="options">Opzioni ricerca (topK, filtri, soglie)</param>
    /// <returns>Risultati ordinati per score combinato RRF</returns>
    Task<List<SearchResult>> SearchAsync(string query, SearchOptions options);

    /// <summary>
    /// Esegue ricerca solo vettoriale su embedding.
    /// </summary>
    /// <param name="queryEmbedding">Vettore embedding della query</param>
    /// <param name="options">Opzioni ricerca (topK, filtri, soglie)</param>
    /// <returns>Risultati ordinati per cosine similarity</returns>
    Task<List<SearchResult>> VectorSearchAsync(float[] queryEmbedding, SearchOptions options);

    /// <summary>
    /// Esegue ricerca solo full-text su contenuti.
    /// </summary>
    /// <param name="query">Query testuale</param>
    /// <param name="options">Opzioni ricerca (topK, filtri)</param>
    /// <returns>Risultati ordinati per score testuale ponderato</returns>
    Task<List<SearchResult>> TextSearchAsync(string query, SearchOptions options);
}

/// <summary>
/// Servizio ricerca ibrida combinando similarità vettoriale e full-text search con Reciprocal Rank Fusion.
/// </summary>
/// <remarks>
/// Strategie ricerca supportate:
/// 1. Hybrid: combina vector + text con RRF per migliori risultati
/// 2. Vector-only: ricerca semantica pura su embedding
/// 3. Text-only: ricerca keyword-based con ponderazione campi
/// 
/// Ottimizzazioni:
/// - SQL Server 2025+: usa VECTOR_DISTANCE nativo per performance
/// - Versioni precedenti: fallback a calcolo in-memory con candidati limitati
/// - Filtri metadata applicati a livello DB prima del caricamento
/// </remarks>
public class HybridSearchService : IHybridSearchService
{
    private readonly ApplicationDbContext _context;
    private readonly IEmbeddingService _embeddingService;

    // Constants for vector search optimization
    private const int CandidateLimitMultiplier = 10; // Get 10x topK candidates for better results
    private const int MinCandidateLimit = 100; // Always get at least 100 candidates
    
    // SQL Server error codes for VECTOR type support detection
    private const int SqlErrorInvalidColumnName = 207; // Invalid column name (VECTOR columns don't exist)
    private const int SqlErrorInvalidDataType = 8116; // Argument data type is invalid (VECTOR type not recognized)

    /// <summary>
    /// Costruttore con dependency injection.
    /// </summary>
    /// <param name="context">Contesto database EF Core</param>
    /// <param name="embeddingService">Servizio per generazione embedding</param>
    public HybridSearchService(ApplicationDbContext context, IEmbeddingService embeddingService)
    {
        _context = context;
        _embeddingService = embeddingService;
    }

    /// <summary>
    /// Esegue ricerca ibrida combinando similarità vettoriale e ricerca testuale.
    /// </summary>
    /// <param name="query">Query utente in linguaggio naturale</param>
    /// <param name="options">Opzioni ricerca (topK, filtri, soglie)</param>
    /// <returns>Risultati ordinati per score RRF combinato</returns>
    /// <remarks>
    /// Workflow:
    /// 1. Genera embedding della query tramite EmbeddingService
    /// 2. Esegue ricerca vettoriale parallela
    /// 3. Esegue ricerca testuale parallela
    /// 4. Merge risultati con Reciprocal Rank Fusion (RRF)
    /// Fallback: se embedding fallisce, usa solo ricerca testuale.
    /// </remarks>
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

        // 3. Perform full-text search
        var textResults = await TextSearchAsync(query, options);

        // 4. Merge results using Reciprocal Rank Fusion (RRF)
        var merged = MergeWithRRF(vectorResults, textResults, options.TopK);

        return merged;
    }

    /// <summary>
    /// Esegue ricerca similarità vettoriale con ottimizzazione database usando VECTOR_DISTANCE quando disponibile.
    /// </summary>
    /// <param name="queryEmbedding">Vettore embedding della query (768 o 1536 dimensioni)</param>
    /// <param name="options">Opzioni ricerca (topK, filtri, soglie)</param>
    /// <returns>Risultati ordinati per cosine similarity decrescente</returns>
    /// <remarks>
    /// Strategia adattiva:
    /// - SQL Server 2025+: usa VECTOR_DISTANCE nativo (performance ottimali)
    /// - Versioni precedenti/errori: fallback a calcolo in-memory
    /// - Non-SQL Server: fallback automatico
    /// Gestione errori: cattura SqlException per rilevare supporto VECTOR type.
    /// </remarks>
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
    /// Esegue ricerca similarità vettoriale usando funzione VECTOR_DISTANCE di SQL Server.
    /// Fornisce performance ottimali calcolando similarità a livello database.
    /// </summary>
    /// <param name="queryEmbedding">Vettore embedding query</param>
    /// <param name="options">Opzioni ricerca</param>
    /// <returns>Risultati con score cosine similarity</returns>
    /// <exception cref="ArgumentException">Se dimensione embedding non supportata (non 768/1536)</exception>
    /// <remarks>
    /// Requisiti: SQL Server 2025+ con supporto tipo VECTOR nativo.
    /// Sicurezza: usa whitelist per column names (prevenzione SQL injection).
    /// Serializzazione: embedding convertito in JSON per CAST a VECTOR type.
    /// Restituisce 2x topK risultati per fusion successiva.
    /// </remarks>
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
    /// Ricerca vettoriale fallback con calcolo cosine similarity in-memory.
    /// Usata quando VECTOR_DISTANCE non disponibile (SQL Server vecchi o database in-memory).
    /// </summary>
    /// <param name="queryEmbedding">Vettore embedding query</param>
    /// <param name="options">Opzioni ricerca</param>
    /// <returns>Risultati con score cosine similarity calcolato in-memory</returns>
    /// <remarks>
    /// OTTIMIZZAZIONI:
    /// - Limita candidati caricati in memoria (topK * 10, min 100)
    /// - Ordina per UploadedAt DESC per ridurre candidati irrelevanti
    /// - Filtra già a livello DB su metadata (categoria, owner, visibility)
    /// - Calcola similarità solo per documenti con embedding
    /// Restituisce 2x topK risultati per fusion successiva.
    /// </remarks>
    private async Task<List<SearchResult>> VectorSearchInMemoryAsync(float[] queryEmbedding, SearchOptions options)
    {
        // Build query with filters
        // OTTIMIZZAZIONE: Query i campi effettivi mappati: EmbeddingVector768 o EmbeddingVector1536
        var documentsQuery = _context.Documents
            .AsNoTracking()
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

        // OTTIMIZZAZIONE: Limita candidati prima di caricare in memoria (10x topK o min 100)
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
    /// Esegue ricerca full-text con matching case-insensitive migliorato.
    /// </summary>
    /// <param name="query">Query testuale con keywords</param>
    /// <param name="options">Opzioni ricerca (topK, filtri)</param>
    /// <returns>Risultati ordinati per score testuale ponderato</returns>
    /// <remarks>
    /// Algoritmo scoring:
    /// - Estrae keywords da query (split su delimitatori, min 2 caratteri)
    /// - Cerca in campi multipli con pesi diversi:
    ///   * FileName: peso 1.0 (massima priorità)
    ///   * ExtractedText: peso 0.8 (media priorità)
    ///   * ActualCategory: peso 0.5 (bassa priorità)
    /// - Score finale: (somma pesi matched) / (tot keywords) * (coverage ratio)
    /// - Coverage ratio: (keywords matchate) / (tot keywords)
    /// 
    /// OTTIMIZZAZIONE: AsNoTracking per query read-only.
    /// Restituisce 2x topK risultati per fusion successiva.
    /// </remarks>
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

        // OTTIMIZZAZIONE: AsNoTracking per query read-only
        var documentsQuery = _context.Documents.AsNoTracking();

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
    /// Merge risultati usando Reciprocal Rank Fusion (RRF) per combinare ranking multipli.
    /// </summary>
    /// <param name="vectorResults">Risultati ricerca vettoriale con ranking</param>
    /// <param name="textResults">Risultati ricerca testuale con ranking</param>
    /// <param name="topK">Numero massimo risultati finali</param>
    /// <param name="k">Costante RRF (default 60, standard in letteratura)</param>
    /// <returns>Risultati merged ordinati per score RRF combinato</returns>
    /// <remarks>
    /// Formula RRF: score = sum(1 / (k + rank)) per ogni ranking dove appare documento.
    /// - k=60: valore standard che bilancia contributo di ranking diversi
    /// - Documenti in entrambe liste ricevono score da entrambe
    /// - Documenti in una sola lista ricevono score solo da quella
    /// 
    /// Vantaggi RRF:
    /// - Indipendente da scale diverse degli score originali
    /// - Favorisce documenti che appaiono in entrambe le ricerche
    /// - Robusto a outlier in un singolo ranking
    /// </remarks>
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
