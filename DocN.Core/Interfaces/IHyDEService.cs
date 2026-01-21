namespace DocN.Core.Interfaces;

/// <summary>
/// Servizio per HyDE (Hypothetical Document Embeddings)
/// Tecnica RAG avanzata che migliora il retrieval generando documenti ipotetici dalla query
/// </summary>
/// <remarks>
/// HyDE (Hypothetical Document Embeddings) è una tecnica innovativa che:
/// 
/// 1. **Problema Risolto**: Le query utente e i documenti hanno spesso uno "stile" diverso
///    - Query: domande brevi, informali (es: "Come ridurre i costi?")
///    - Documenti: testo formale, dettagliato (es: "Strategia di ottimizzazione dei costi...")
///    - Questo gap stilistico riduce l'efficacia del matching vettoriale
/// 
/// 2. **Soluzione HyDE**:
///    - Genera un documento ipotetico che risponderebbe alla query
///    - Usa l'embedding di questo documento (non della query) per la ricerca
///    - Il documento ipotetico è stilisticamente simile ai documenti reali
///    - Migliora significativamente il recall (trovare documenti rilevanti)
/// 
/// 3. **Workflow**:
///    Query → LLM genera risposta ipotetica → Embedding risposta → Ricerca vettoriale
///    
/// 4. **Vantaggi**:
///    - Migliora recall del 10-30% in molti scenari
///    - Particolarmente efficace per query complesse o astratte
///    - Colma il gap semantico tra query e documenti
///    
/// 5. **Quando Usare**:
///    - Query concettuali o astratte
///    - Domini specializzati con linguaggio specifico
///    - Quando la ricerca tradizionale dà risultati scarsi
/// 
/// Riferimento: "Precise Zero-Shot Dense Retrieval without Relevance Labels" (Gao et al., 2022)
/// </remarks>
public interface IHyDEService
{
    /// <summary>
    /// Genera un documento ipotetico che risponderebbe alla query
    /// </summary>
    /// <param name="query">Query originale dell'utente</param>
    /// <param name="domainContext">Contesto opzionale del dominio per migliorare la generazione</param>
    /// <returns>Documento ipotetico generato dall'AI</returns>
    /// <remarks>
    /// Il documento generato:
    /// - È stilisticamente simile ai documenti reali del corpus
    /// - Contiene informazioni che potrebbero rispondere alla query
    /// - Non deve essere fattualmente corretto (è "ipotetico")
    /// - Serve solo per migliorare il retrieval
    /// 
    /// Esempio:
    /// Query: "Come ridurre i costi operativi?"
    /// Documento ipotetico: "La riduzione dei costi operativi può essere ottenuta attraverso 
    /// l'ottimizzazione dei processi, l'automazione delle attività ripetitive, la negoziazione 
    /// con i fornitori e l'implementazione di tecnologie efficienti..."
    /// </remarks>
    Task<string> GenerateHypotheticalDocumentAsync(string query, string? domainContext = null);

    /// <summary>
    /// Genera multiple varianti di documenti ipotetici per una query
    /// </summary>
    /// <param name="query">Query originale</param>
    /// <param name="numVariants">Numero di varianti da generare (tipicamente 2-3)</param>
    /// <param name="domainContext">Contesto opzionale del dominio</param>
    /// <returns>Lista di documenti ipotetici con diverse prospettive</returns>
    /// <remarks>
    /// Generare multiple varianti:
    /// - Aumenta la copertura della ricerca
    /// - Esplora diverse interpretazioni della query
    /// - Mitiga il rischio di un singolo documento ipotetico mal generato
    /// 
    /// I risultati vengono poi aggregati (es: fusione dei risultati o media degli embeddings)
    /// </remarks>
    Task<List<string>> GenerateMultipleHypotheticalDocumentsAsync(
        string query, 
        int numVariants = 2,
        string? domainContext = null);

    /// <summary>
    /// Esegue ricerca usando HyDE: genera documento ipotetico e cerca con il suo embedding
    /// </summary>
    /// <param name="query">Query originale dell'utente</param>
    /// <param name="userId">ID utente per controllo accesso</param>
    /// <param name="topK">Numero di risultati da restituire</param>
    /// <param name="minSimilarity">Soglia minima di similarità</param>
    /// <returns>Risultati di ricerca basati su HyDE</returns>
    /// <remarks>
    /// Workflow completo:
    /// 1. Genera documento(i) ipotetico dalla query
    /// 2. Calcola embedding del documento ipotetico
    /// 3. Usa questo embedding per ricerca vettoriale
    /// 4. Restituisce i documenti reali più simili al documento ipotetico
    /// </remarks>
    Task<List<RelevantDocumentResult>> SearchWithHyDEAsync(
        string query,
        string userId,
        int topK = 10,
        double minSimilarity = 0.7);

    /// <summary>
    /// Esegue ricerca ibrida: combina risultati di ricerca standard e HyDE
    /// </summary>
    /// <param name="query">Query originale</param>
    /// <param name="userId">ID utente</param>
    /// <param name="topK">Numero di risultati finali</param>
    /// <param name="hydeWeight">Peso dei risultati HyDE (0-1, default 0.6)</param>
    /// <returns>Risultati combinati da entrambi gli approcci</returns>
    /// <remarks>
    /// Approccio ibrido:
    /// - Esegue ricerca standard con embedding della query
    /// - Esegue ricerca HyDE con embedding del documento ipotetico
    /// - Combina i risultati con pesi configurabili
    /// - Migliora sia precision che recall
    /// 
    /// Vantaggi:
    /// - Più robusto di HyDE puro (se il documento ipotetico è sbagliato)
    /// - Migliore copertura rispetto alla ricerca standard
    /// </remarks>
    Task<List<RelevantDocumentResult>> SearchHybridWithHyDEAsync(
        string query,
        string userId,
        int topK = 10,
        double hydeWeight = 0.6);

    /// <summary>
    /// Valuta se HyDE è appropriato per una data query
    /// </summary>
    /// <param name="query">Query da analizzare</param>
    /// <returns>Risultato dell'analisi con raccomandazione</returns>
    /// <remarks>
    /// HyDE è più efficace per:
    /// - Query complesse o concettuali
    /// - Query che richiedono ragionamento
    /// - Query in domini specializzati
    /// 
    /// HyDE potrebbe non servire per:
    /// - Query semplici keyword-based
    /// - Ricerche esatte (nomi propri, codici, etc.)
    /// - Query molto brevi
    /// </remarks>
    Task<HyDERecommendation> AnalyzeQueryForHyDEAsync(string query);
}

/// <summary>
/// Risultato dell'analisi per determinare se usare HyDE
/// </summary>
public class HyDERecommendation
{
    /// <summary>
    /// Indica se HyDE è raccomandato per questa query
    /// </summary>
    public bool IsRecommended { get; set; }

    /// <summary>
    /// Livello di confidenza nella raccomandazione (0-1)
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Motivo della raccomandazione
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Tipo di query identificato
    /// </summary>
    public QueryType QueryType { get; set; }

    /// <summary>
    /// Peso suggerito per HyDE se usato in modalità ibrida (0-1)
    /// </summary>
    public double SuggestedHyDEWeight { get; set; } = 0.6;
}

/// <summary>
/// Tipi di query per HyDE
/// </summary>
public enum QueryType
{
    /// <summary>
    /// Query semplice keyword-based (HyDE meno utile)
    /// </summary>
    Simple,

    /// <summary>
    /// Query concettuale o astratta (HyDE molto utile)
    /// </summary>
    Conceptual,

    /// <summary>
    /// Query che richiede ragionamento (HyDE molto utile)
    /// </summary>
    Reasoning,

    /// <summary>
    /// Query di ricerca esatta (HyDE non raccomandato)
    /// </summary>
    Exact,

    /// <summary>
    /// Query complessa con multiple parti (HyDE utile)
    /// </summary>
    Complex
}

/// <summary>
/// Configurazione per HyDE
/// </summary>
public class HyDEConfiguration
{
    /// <summary>
    /// Indica se HyDE è abilitato
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Numero di documenti ipotetici da generare
    /// </summary>
    public int NumHypotheticalDocs { get; set; } = 1;

    /// <summary>
    /// Lunghezza target del documento ipotetico (in token)
    /// </summary>
    public int TargetDocumentLength { get; set; } = 200;

    /// <summary>
    /// Temperatura per generazione documenti ipotetici (0-1)
    /// </summary>
    /// <remarks>
    /// - Bassa (0.3-0.5): Più conservativo e coerente
    /// - Alta (0.7-0.9): Più creativo e diversificato
    /// </remarks>
    public double Temperature { get; set; } = 0.7;

    /// <summary>
    /// Usa modalità ibrida (combina ricerca standard e HyDE)
    /// </summary>
    public bool UseHybridMode { get; set; } = true;

    /// <summary>
    /// Peso dei risultati HyDE in modalità ibrida (0-1)
    /// </summary>
    public double HyDEWeight { get; set; } = 0.6;

    /// <summary>
    /// Usa analisi automatica per decidere se applicare HyDE
    /// </summary>
    public bool AutoDecideHyDE { get; set; } = true;
}
