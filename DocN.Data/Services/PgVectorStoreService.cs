using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Pgvector;
using DocN.Core.Interfaces;
using DocN.Core.AI.Configuration;
using DocN.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace DocN.Data.Services;

/// <summary>
/// PostgreSQL pgvector implementation for optimal vector search
/// Supports HNSW index, approximate nearest neighbor (ANN) search, and metadata filtering
/// </summary>
public class PgVectorStoreService : IVectorStoreService
{
    private readonly ILogger<PgVectorStoreService> _logger;
    private readonly PgVectorConfiguration _config;
    private readonly IMMRService _mmrService;
    private readonly EnhancedRAGConfiguration _ragConfig;
    private readonly ApplicationDbContext _context;
    private readonly string _connectionString;

    /// <summary>
    /// Inizializza una nuova istanza di PgVectorStoreService con le dipendenze necessarie.
    /// </summary>
    /// <param name="logger">Logger per la registrazione degli eventi e degli errori</param>
    /// <param name="config">Configurazione per la connessione e le impostazioni di pgvector</param>
    /// <param name="mmrService">Servizio per il reranking MMR (Maximal Marginal Relevance)</param>
    /// <param name="ragConfig">Configurazione avanzata per il sistema RAG (Retrieval-Augmented Generation)</param>
    /// <param name="context">Context del database per l'accesso alle configurazioni AI</param>
    /// <remarks>
    /// Il costruttore inizializza automaticamente l'estensione pgvector nel database PostgreSQL.
    /// Tutti i parametri sono obbligatori e genereranno un'eccezione se null.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Lanciato quando uno dei parametri è null</exception>
    public PgVectorStoreService(
        ILogger<PgVectorStoreService> logger,
        IOptions<PgVectorConfiguration> config,
        IMMRService mmrService,
        IOptions<EnhancedRAGConfiguration> ragConfig,
        ApplicationDbContext context)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config.Value ?? throw new ArgumentNullException(nameof(config));
        _mmrService = mmrService ?? throw new ArgumentNullException(nameof(mmrService));
        _ragConfig = ragConfig?.Value ?? throw new ArgumentNullException(nameof(ragConfig));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _connectionString = _config.ConnectionString;

        // Initialize pgvector
        InitializePgVector();
    }

    /// <summary>
    /// Inizializza l'estensione pgvector nel database PostgreSQL.
    /// </summary>
    /// <remarks>
    /// Questo metodo esegue il comando SQL "CREATE EXTENSION IF NOT EXISTS vector" per abilitare
    /// il supporto ai vettori in PostgreSQL. L'estensione pgvector è necessaria per:
    /// - Memorizzare vettori di embedding ad alta dimensionalità
    /// - Eseguire ricerche di similarità vettoriale
    /// - Utilizzare operatori di distanza vettoriale (<=> per distanza coseno)
    /// - Creare indici HNSW per ricerche approssimate veloci
    /// Gli errori vengono registrati ma non interrompono l'esecuzione.
    /// </remarks>
    private void InitializePgVector()
    {
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            // Enable pgvector extension
            using var cmd = new NpgsqlCommand("CREATE EXTENSION IF NOT EXISTS vector", connection);
            cmd.ExecuteNonQuery();

            _logger.LogInformation("pgvector extension enabled successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize pgvector extension");
        }
    }

    /// <summary>
    /// Memorizza un vettore di embedding nel database PostgreSQL con metadata opzionali.
    /// </summary>
    /// <param name="id">Identificatore univoco del vettore (tipicamente l'ID del chunk di documento)</param>
    /// <param name="vector">Array di valori float che rappresenta il vettore di embedding</param>
    /// <param name="metadata">Dizionario opzionale di metadata da associare al vettore (es. titolo, fonte, timestamp)</param>
    /// <returns>True se il vettore è stato memorizzato con successo, false in caso di errore</returns>
    /// <remarks>
    /// Questa operazione utilizza UPSERT (INSERT...ON CONFLICT DO UPDATE) per:
    /// - Inserire un nuovo vettore se l'ID non esiste
    /// - Aggiornare il vettore esistente se l'ID è già presente
    /// I metadata vengono serializzati in formato JSONB per consentire query e filtri efficienti.
    /// La connessione al database viene gestita automaticamente (await using).
    /// Timestamp created_at e updated_at vengono gestiti automaticamente in UTC.
    /// </remarks>
    /// <inheritdoc/>
    public async Task<bool> StoreVectorAsync(string id, float[] vector, Dictionary<string, object>? metadata = null)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            var metadataJson = metadata != null 
                ? System.Text.Json.JsonSerializer.Serialize(metadata) 
                : "{}";

            var sql = @"
                INSERT INTO document_vectors (id, embedding, metadata, created_at)
                VALUES (@id, @embedding, @metadata::jsonb, @createdAt)
                ON CONFLICT (id) DO UPDATE 
                SET embedding = @embedding, 
                    metadata = @metadata::jsonb,
                    updated_at = @updatedAt";

            await using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("embedding", new Vector(vector));
            cmd.Parameters.AddWithValue("metadata", metadataJson);
            cmd.Parameters.AddWithValue("createdAt", DateTime.UtcNow);
            cmd.Parameters.AddWithValue("updatedAt", DateTime.UtcNow);

            await cmd.ExecuteNonQueryAsync();
            _logger.LogDebug("Stored vector with ID: {Id}", id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing vector with ID: {Id}", id);
            return false;
        }
    }

    /// <summary>
    /// Cerca i vettori più simili al vettore di query utilizzando la ricerca di similarità coseno.
    /// </summary>
    /// <param name="queryVector">Vettore di embedding della query per cui cercare risultati simili</param>
    /// <param name="topK">Numero massimo di risultati da restituire (default: 10)</param>
    /// <param name="metadataFilter">Filtri opzionali sui metadata per restringere la ricerca</param>
    /// <param name="minSimilarity">Soglia minima di similarità coseno (0-1, default: 0.7)</param>
    /// <returns>Lista di VectorSearchResult ordinati per similarità decrescente</returns>
    /// <remarks>
    /// Questa operazione sfrutta l'operatore di distanza coseno di pgvector (<=>):
    /// - Calcola 1 - distanza_coseno per ottenere il punteggio di similarità (0-1)
    /// - Utilizza automaticamente l'indice HNSW se presente per ricerche veloci (ANN)
    /// - Recupera topK*2 candidati iniziali per compensare il filtro di similarità
    /// - Applica il filtro minSimilarity per garantire solo risultati di qualità
    /// 
    /// L'indice HNSW (Hierarchical Navigable Small World) fornisce:
    /// - Ricerca approssimata dei vicini più prossimi in tempo sub-lineare O(log n)
    /// - Alta precisione con parametri m=16 e ef_construction=64
    /// - Ottimizzazione automatica per grandi dataset di vettori
    /// 
    /// I metadata vengono deserializzati da JSONB in Dictionary per facilità d'uso.
    /// </remarks>
    /// <inheritdoc/>
    public async Task<List<VectorSearchResult>> SearchSimilarVectorsAsync(
        float[] queryVector,
        int topK = 10,
        Dictionary<string, object>? metadataFilter = null,
        double minSimilarity = 0.7)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            // Build WHERE clause for metadata filtering
            var whereClause = BuildMetadataFilter(metadataFilter);

            // Use cosine distance (1 - cosine similarity) with pgvector
            // HNSW index will automatically be used if it exists
            var sql = $@"
                SELECT 
                    id,
                    embedding,
                    metadata,
                    1 - (embedding <=> @queryVector) as similarity
                FROM document_vectors
                {whereClause}
                ORDER BY embedding <=> @queryVector
                LIMIT @limit";

            await using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("queryVector", new Vector(queryVector));
            cmd.Parameters.AddWithValue("limit", topK * 2); // Get more for filtering

            var results = new List<VectorSearchResult>();

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var similarity = reader.GetDouble(3);
                
                // Filter by minimum similarity
                if (similarity < minSimilarity)
                    continue;

                var vectorData = reader.GetFieldValue<Vector>(1);
                var metadataJson = reader.GetString(2);
                var metadata = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(metadataJson) 
                              ?? new Dictionary<string, object>();

                results.Add(new VectorSearchResult
                {
                    Id = reader.GetString(0),
                    Vector = vectorData.ToArray(),
                    SimilarityScore = similarity,
                    Metadata = metadata
                });

                if (results.Count >= topK)
                    break;
            }

            _logger.LogInformation("Found {Count} similar vectors (threshold: {Threshold})", results.Count, minSimilarity);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching similar vectors");
            return new List<VectorSearchResult>();
        }
    }

    /// <summary>
    /// Cerca vettori simili applicando il reranking MMR (Maximal Marginal Relevance) per diversificare i risultati.
    /// </summary>
    /// <param name="queryVector">Vettore di embedding della query</param>
    /// <param name="topK">Numero di risultati finali da restituire dopo il reranking (default: 10)</param>
    /// <param name="lambda">Parametro di bilanciamento MMR tra rilevanza e diversità (0-1, default: 0.5)</param>
    /// <param name="metadataFilter">Filtri opzionali sui metadata</param>
    /// <returns>Lista di VectorSearchResult re-ordinati con punteggi MMR</returns>
    /// <remarks>
    /// MMR (Maximal Marginal Relevance) bilancia rilevanza e diversità nei risultati:
    /// - Lambda = 1.0: massima rilevanza (come ricerca standard)
    /// - Lambda = 0.0: massima diversità (risultati molto diversi tra loro)
    /// - Lambda = 0.5: bilanciamento equilibrato (raccomandato)
    /// 
    /// Processo di ricerca e reranking:
    /// 1. Recupera topK*3 candidati iniziali (minSimilarity: 0.5) per maggiore copertura
    /// 2. Determina il lambda effettivo con priorità: parametro esplicito > config DB > appsettings
    /// 3. Applica l'algoritmo MMR per selezionare iterativamente i topK risultati più rilevanti e diversi
    /// 4. Restituisce risultati con sia SimilarityScore (coseno) che MMRScore
    /// 
    /// Vantaggi del reranking MMR:
    /// - Riduce la ridondanza nei risultati RAG
    /// - Migliora la copertura di informazioni diverse
    /// - Ottimizza il contesto fornito al LLM
    /// - Configurabile dinamicamente via database per ottimizzazione runtime
    /// </remarks>
    /// <inheritdoc/>
    public async Task<List<VectorSearchResult>> SearchWithMMRAsync(
        float[] queryVector,
        int topK = 10,
        double lambda = 0.5,
        Dictionary<string, object>? metadataFilter = null)
    {
        try
        {
            // Get more candidates for MMR
            var candidates = await SearchSimilarVectorsAsync(
                queryVector,
                topK * 3,
                metadataFilter,
                minSimilarity: 0.5);

            if (!candidates.Any())
            {
                return new List<VectorSearchResult>();
            }

            // Get effective lambda: database config > parameter > appsettings
            var effectiveLambda = await GetEffectiveLambdaAsync(lambda);
            
            _logger.LogInformation(
                "MMR reranking with lambda={Lambda} (configured={ConfiguredLambda}, database={FromDatabase})",
                effectiveLambda, _ragConfig.Reranking.MMRLambda, effectiveLambda != lambda && effectiveLambda != _ragConfig.Reranking.MMRLambda);

            // Convert to MMR candidates
            var mmrCandidates = candidates.Select(c => new CandidateVector
            {
                Id = c.Id,
                Vector = c.Vector,
                InitialScore = c.SimilarityScore,
                Metadata = c.Metadata
            }).ToList();

            // Apply MMR reranking with configured lambda
            var mmrResults = await _mmrService.RerankWithMMRAsync(
                queryVector,
                mmrCandidates,
                topK,
                effectiveLambda);

            // Convert back to VectorSearchResult
            return mmrResults.Select(r => new VectorSearchResult
            {
                Id = r.Id,
                Vector = r.Vector,
                SimilarityScore = r.InitialScore,
                MMRScore = r.MMRScore,
                Metadata = r.Metadata
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in MMR search");
            return new List<VectorSearchResult>();
        }
    }

    /// <summary>
    /// Crea o aggiorna un indice vettoriale sul database PostgreSQL per ottimizzare le ricerche.
    /// </summary>
    /// <param name="indexName">Nome dell'indice da creare o aggiornare</param>
    /// <param name="indexType">Tipo di indice vettoriale da creare (default: HNSW)</param>
    /// <returns>True se l'indice è stato creato con successo, false in caso di errore</returns>
    /// <remarks>
    /// Tipi di indice supportati da pgvector:
    /// 
    /// 1. HNSW (Hierarchical Navigable Small World) - RACCOMANDATO:
    ///    - Algoritmo di ricerca approssimata (ANN) di alta qualità
    ///    - Parametri: m=16 (connessioni per nodo), ef_construction=64 (qualità costruzione)
    ///    - Complessità: O(log n) per query, ottimo per dataset grandi (>10K vettori)
    ///    - Recall tipico: 95-99% con velocità 100x superiore a ricerca esatta
    ///    - Overhead memoria: ~16 bytes * m per vettore
    /// 
    /// 2. IVFFlat (Inverted File with Flat compression):
    ///    - Indice basato su clustering con k-means
    ///    - Parametri: lists=100 (numero di cluster)
    ///    - Più veloce da costruire di HNSW, ma recall inferiore
    ///    - Richiede VACUUM ANALYZE per prestazioni ottimali
    /// 
    /// 3. Flat (nessun indice vettoriale):
    ///    - Ricerca esatta lineare O(n)
    ///    - Solo per dataset piccoli (<1K vettori) o testing
    /// 
    /// L'operazione esegue DROP INDEX IF EXISTS prima di ricreare l'indice per garantire
    /// configurazione pulita. Durante la creazione, la tabella rimane accessibile in lettura.
    /// Per dataset grandi, la creazione dell'indice HNSW può richiedere diversi minuti.
    /// </remarks>
    /// <inheritdoc/>
    public async Task<bool> CreateOrUpdateIndexAsync(string indexName, VectorIndexType indexType = VectorIndexType.HNSW)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            // Drop existing index if exists
            var dropSql = $"DROP INDEX IF EXISTS {indexName}";
            await using var dropCmd = new NpgsqlCommand(dropSql, connection);
            await dropCmd.ExecuteNonQueryAsync();

            // Create index based on type
            string createSql = indexType switch
            {
                VectorIndexType.HNSW => $@"
                    CREATE INDEX {indexName} ON document_vectors 
                    USING hnsw (embedding vector_cosine_ops)
                    WITH (m = 16, ef_construction = 64)",
                
                VectorIndexType.IVFFlat => $@"
                    CREATE INDEX {indexName} ON document_vectors 
                    USING ivfflat (embedding vector_cosine_ops)
                    WITH (lists = 100)",
                
                VectorIndexType.Flat => $@"
                    CREATE INDEX {indexName} ON document_vectors 
                    USING btree (id)",
                
                _ => throw new ArgumentException($"Unsupported index type: {indexType}")
            };

            await using var createCmd = new NpgsqlCommand(createSql, connection);
            await createCmd.ExecuteNonQueryAsync();

            _logger.LogInformation("Created {IndexType} index: {IndexName}", indexType, indexName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating index: {IndexName}", indexName);
            return false;
        }
    }

    /// <summary>
    /// Recupera un vettore di embedding specifico dal database tramite il suo ID.
    /// </summary>
    /// <param name="id">Identificatore univoco del vettore da recuperare</param>
    /// <returns>Array di float rappresentante il vettore, o null se non trovato</returns>
    /// <remarks>
    /// Questa operazione esegue una query diretta per ID (ricerca esatta, non vettoriale).
    /// Utilizza l'indice sulla chiave primaria per prestazioni O(1).
    /// Il vettore viene convertito dal tipo nativo pgvector.Vector a float[] standard.
    /// Utile per verificare vettori esistenti o per operazioni di confronto manuale.
    /// </remarks>
    /// <inheritdoc/>
    public async Task<float[]?> GetVectorAsync(string id)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = "SELECT embedding FROM document_vectors WHERE id = @id";
            await using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("id", id);

            var result = await cmd.ExecuteScalarAsync();
            if (result is Vector vector)
            {
                return vector.ToArray();
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting vector with ID: {Id}", id);
            return null;
        }
    }

    /// <summary>
    /// Elimina un vettore specifico dal database.
    /// </summary>
    /// <param name="id">Identificatore univoco del vettore da eliminare</param>
    /// <returns>True se il vettore è stato eliminato, false se non esisteva o si è verificato un errore</returns>
    /// <remarks>
    /// L'operazione di DELETE è atomica e utilizza l'indice sulla chiave primaria.
    /// Rimuove completamente il record inclusi vettore, metadata e timestamp.
    /// Il numero di righe eliminate viene verificato per determinare il successo dell'operazione.
    /// Dopo molte eliminazioni, considerare VACUUM per recuperare spazio su disco.
    /// </remarks>
    /// <inheritdoc/>
    public async Task<bool> DeleteVectorAsync(string id)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = "DELETE FROM document_vectors WHERE id = @id";
            await using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("id", id);

            var rowsAffected = await cmd.ExecuteNonQueryAsync();
            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting vector with ID: {Id}", id);
            return false;
        }
    }

    /// <summary>
    /// Memorizza molteplici vettori in una singola transazione per prestazioni ottimali.
    /// </summary>
    /// <param name="entries">Lista di VectorEntry contenenti ID, vettori e metadata da memorizzare</param>
    /// <returns>Numero di vettori memorizzati con successo</returns>
    /// <remarks>
    /// Operazione batch ottimizzata per l'inserimento di grandi quantità di vettori:
    /// 
    /// Vantaggi dell'approccio transazionale:
    /// - Tutti i vettori vengono inseriti o nessuno (atomicità ACID)
    /// - Riduce l'overhead di rete e di connessione (un'unica transazione)
    /// - Migliora le prestazioni di ~10-100x rispetto a inserimenti singoli
    /// - Garantisce consistenza del database in caso di errori
    /// 
    /// Ogni entry supporta UPSERT (INSERT...ON CONFLICT):
    /// - Inserisce nuovi vettori se l'ID non esiste
    /// - Aggiorna vettori esistenti preservando l'integrità dei dati
    /// 
    /// Best practices:
    /// - Raccomandato per operazioni di indicizzazione documenti
    /// - Considerare batch di 100-1000 vettori per bilanciare memoria e prestazioni
    /// - Per dataset molto grandi, dividere in batch multipli
    /// - La transazione viene automaticamente rollback in caso di errore
    /// 
    /// Dopo batch insert significativi, eseguire ANALYZE per aggiornare le statistiche
    /// del query planner e ottimizzare le successive ricerche vettoriali.
    /// </remarks>
    /// <inheritdoc/>
    public async Task<int> BatchStoreVectorsAsync(List<VectorEntry> entries)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var transaction = await connection.BeginTransactionAsync();

            var successCount = 0;
            foreach (var entry in entries)
            {
                var metadataJson = entry.Metadata != null 
                    ? System.Text.Json.JsonSerializer.Serialize(entry.Metadata) 
                    : "{}";

                var sql = @"
                    INSERT INTO document_vectors (id, embedding, metadata, created_at)
                    VALUES (@id, @embedding, @metadata::jsonb, @createdAt)
                    ON CONFLICT (id) DO UPDATE 
                    SET embedding = @embedding, 
                        metadata = @metadata::jsonb,
                        updated_at = @updatedAt";

                await using var cmd = new NpgsqlCommand(sql, connection, transaction);
                cmd.Parameters.AddWithValue("id", entry.Id);
                cmd.Parameters.AddWithValue("embedding", new Vector(entry.Vector));
                cmd.Parameters.AddWithValue("metadata", metadataJson);
                cmd.Parameters.AddWithValue("createdAt", DateTime.UtcNow);
                cmd.Parameters.AddWithValue("updatedAt", DateTime.UtcNow);

                await cmd.ExecuteNonQueryAsync();
                successCount++;
            }

            await transaction.CommitAsync();
            _logger.LogInformation("Batch stored {Count} vectors", successCount);
            return successCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in batch store vectors");
            return 0;
        }
    }

    /// <summary>
    /// Recupera statistiche complete sul database vettoriale per monitoraggio e debugging.
    /// </summary>
    /// <returns>Oggetto VectorDatabaseStats contenente metriche del database vettoriale</returns>
    /// <remarks>
    /// Statistiche fornite:
    /// - TotalVectors: numero totale di vettori memorizzati
    /// - VectorDimension: dimensionalità dei vettori (es. 1536 per OpenAI ada-002)
    /// - StorageSize: dimensione totale della tabella (formattata human-readable da pg_size_pretty)
    /// - IndexType: tipo di indice vettoriale in uso (es. "pgvector HNSW")
    /// - IndexExists: presenza di indici vettoriali
    /// 
    /// Utilizza funzioni native PostgreSQL:
    /// - COUNT(*) per contare i vettori
    /// - pg_total_relation_size() per calcolare lo spazio occupato (include indici e TOAST)
    /// - pg_size_pretty() per formattazione leggibile (es. "1.2 GB")
    /// 
    /// Queste metriche sono utili per:
    /// - Monitoraggio della crescita del database
    /// - Pianificazione della capacità e scaling
    /// - Debugging di problemi di prestazioni
    /// - Verifica della corretta configurazione degli indici
    /// 
    /// La query è leggera e può essere eseguita frequentemente per monitoring.
    /// </remarks>
    /// <inheritdoc/>
    public async Task<VectorDatabaseStats> GetStatsAsync()
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT 
                    COUNT(*) as total_vectors,
                    pg_size_pretty(pg_total_relation_size('document_vectors')) as storage_size
                FROM document_vectors";

            await using var cmd = new NpgsqlCommand(sql, connection);
            await using var reader = await cmd.ExecuteReaderAsync();
            
            if (await reader.ReadAsync())
            {
                var totalVectors = reader.GetInt64(0);
                var storageSize = reader.GetString(1);

                // Get dimension from first vector
                reader.Close();
                var dimSql = "SELECT embedding FROM document_vectors LIMIT 1";
                await using var dimCmd = new NpgsqlCommand(dimSql, connection);
                var firstVector = await dimCmd.ExecuteScalarAsync() as Vector;

                return new VectorDatabaseStats
                {
                    TotalVectors = totalVectors,
                    VectorDimension = firstVector?.ToArray().Length ?? 0,
                    StorageSizeBytes = 0, // Would need to parse pg_size_pretty
                    IndexType = "pgvector HNSW",
                    IndexExists = true
                };
            }

            return new VectorDatabaseStats();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting stats");
            return new VectorDatabaseStats();
        }
    }

    // Helper methods

    /// <summary>
    /// Costruisce una clausola WHERE SQL per filtrare i vettori in base ai metadata JSONB.
    /// </summary>
    /// <param name="metadataFilter">Dizionario di filtri da applicare ai metadata</param>
    /// <returns>Stringa SQL con la clausola WHERE, o stringa vuota se nessun filtro</returns>
    /// <remarks>
    /// Genera condizioni SQL per interrogare i metadata memorizzati come JSONB:
    /// - Utilizza l'operatore -> per accedere alle chiavi JSONB
    /// - Confronta i valori usando uguaglianza esatta
    /// - Combina più filtri con AND logico
    /// 
    /// Esempio di output per filtri {titolo: "doc1", tipo: "pdf"}:
    /// "WHERE metadata->'titolo' = '\"doc1\"' AND metadata->'tipo' = '\"pdf\"'"
    /// 
    /// PostgreSQL supporta indici GIN su colonne JSONB per query efficienti.
    /// Considerare CREATE INDEX ON document_vectors USING gin(metadata) per grandi dataset.
    /// </remarks>
    private string BuildMetadataFilter(Dictionary<string, object>? metadataFilter)
    {
        if (metadataFilter == null || !metadataFilter.Any())
        {
            return "";
        }

        var conditions = new List<string>();
        foreach (var filter in metadataFilter)
        {
            // Build JSONB filter conditions
            conditions.Add($"metadata->'{filter.Key}' = '\"{filter.Value}\"'");
        }

        return conditions.Any() ? "WHERE " + string.Join(" AND ", conditions) : "";
    }

    /// <summary>
    /// Determina il valore lambda effettivo per MMR con priorità configurabile a runtime.
    /// </summary>
    /// <param name="parameterLambda">Valore lambda fornito come parametro della chiamata</param>
    /// <returns>Valore lambda effettivo da utilizzare per il reranking MMR</returns>
    /// <remarks>
    /// Gerarchia di priorità per determinare il lambda (parametro MMR):
    /// 1. Parametro esplicito (se diverso dal default 0.5) - massima priorità
    /// 2. Configurazione database (AIConfiguration attiva) - permette tuning runtime
    /// 3. Configurazione appsettings.json - fallback predefinito
    /// 
    /// Questo approccio a 3 livelli consente:
    /// - Override puntuale per richieste specifiche (parametro esplicito)
    /// - Ottimizzazione dinamica senza riavvio (config database)
    /// - Valori predefiniti stabili per l'applicazione (appsettings)
    /// 
    /// Il lambda dal database viene validato (0 &lt; lambda ≤ 1.0) prima dell'uso.
    /// Errori di accesso al database vengono gestiti con graceful fallback ad appsettings.
    /// Il valore scelto viene loggato per tracciabilità e debugging.
    /// </remarks>
    private async Task<double> GetEffectiveLambdaAsync(double parameterLambda)
    {
        try
        {
            // If explicitly provided (not default 0.5), use it
            if (parameterLambda != 0.5)
            {
                return parameterLambda;
            }

            // Try to get from database (active AIConfiguration)
            var dbConfig = await _context.AIConfigurations
                .Where(c => c.IsActive)
                .OrderByDescending(c => c.Id)
                .FirstOrDefaultAsync();

            if (dbConfig != null && dbConfig.MMRLambda > 0 && dbConfig.MMRLambda <= 1.0)
            {
                _logger.LogDebug("Using MMR Lambda from database: {Lambda}", dbConfig.MMRLambda);
                return dbConfig.MMRLambda;
            }

            // Fallback to appsettings.json config
            _logger.LogDebug("Using MMR Lambda from appsettings: {Lambda}", _ragConfig.Reranking.MMRLambda);
            return _ragConfig.Reranking.MMRLambda;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error loading MMR Lambda from database, using appsettings");
            return _ragConfig.Reranking.MMRLambda;
        }
    }
}

/// <summary>
/// Configuration for pgvector
/// </summary>
public class PgVectorConfiguration
{
    public string ConnectionString { get; set; } = string.Empty;
    public string TableName { get; set; } = "document_vectors";
    public int DefaultDimension { get; set; } = 1536;
    public bool AutoCreateTable { get; set; } = true;
}
