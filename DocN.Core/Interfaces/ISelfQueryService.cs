namespace DocN.Core.Interfaces;

/// <summary>
/// Servizio per Self-Query: estrazione automatica di filtri strutturati da linguaggio naturale
/// Permette agli utenti di specificare filtri complessi usando linguaggio naturale invece di sintassi query
/// </summary>
/// <remarks>
/// Self-Query è una tecnica RAG avanzata che:
/// 
/// 1. **Problema Risolto**: Gli utenti vogliono filtrare risultati ma non conoscono la sintassi query
///    - Query: "documenti finanziari degli ultimi 3 mesi"
///    - L'utente vuole: category="Finanziario" AND date >= (oggi - 3 mesi)
///    - Ma non sa come esprimere questi filtri programmaticamente
/// 
/// 2. **Soluzione Self-Query**:
///    - LLM analizza la query in linguaggio naturale
///    - Estrae query semantica + filtri strutturati
///    - Applica filtri al database/indice
///    - Esegue ricerca semantica sui risultati filtrati
/// 
/// 3. **Workflow**:
///    Query NL → LLM → {query semantica, filtri} → Applicazione filtri → Ricerca → Risultati
///    
/// 4. **Vantaggi**:
///    - Interfaccia utente più naturale e accessibile
///    - Migliora precisione eliminando risultati irrilevanti
///    - Riduce carico computazionale (meno documenti da analizzare)
///    - Supporta filtri complessi senza sintassi speciale
/// 
/// 5. **Tipi di Filtri Supportati**:
///    - Date (es: "ultimi 6 mesi", "da gennaio 2024")
///    - Categorie (es: "solo fatture", "documenti HR")
///    - Metadata (es: "approvati", "confidenziali")
///    - Range numerici (es: "importo > 1000")
///    - Autore/Owner (es: "caricati da Mario")
/// 
/// Riferimento: Parte della suite LangChain self-query retriever
/// </remarks>
public interface ISelfQueryService
{
    /// <summary>
    /// Analizza una query in linguaggio naturale ed estrae filtri strutturati
    /// </summary>
    /// <param name="naturalLanguageQuery">Query in linguaggio naturale con filtri impliciti</param>
    /// <param name="availableFilters">Filtri disponibili nel sistema (per guidare l'estrazione)</param>
    /// <returns>Query semantica separata dai filtri strutturati</returns>
    /// <remarks>
    /// Esempio:
    /// Input: "Mostrami le fatture degli ultimi 3 mesi superiori a 1000 euro"
    /// Output: 
    /// - Query semantica: "fatture"
    /// - Filtri: [
    ///     {field: "category", operator: "equals", value: "Fattura"},
    ///     {field: "uploadDate", operator: ">=", value: "2024-10-01"},
    ///     {field: "amount", operator: ">", value: 1000}
    ///   ]
    /// </remarks>
    Task<SelfQueryResult> ParseQueryWithFiltersAsync(
        string naturalLanguageQuery,
        List<FilterDefinition>? availableFilters = null);

    /// <summary>
    /// Applica filtri estratti alla ricerca documentale
    /// </summary>
    /// <param name="semanticQuery">Query semantica (senza filtri)</param>
    /// <param name="filters">Filtri da applicare</param>
    /// <param name="userId">ID utente per controllo accesso</param>
    /// <param name="topK">Numero di risultati da restituire</param>
    /// <returns>Risultati filtrati e ordinati per rilevanza</returns>
    /// <remarks>
    /// Combina:
    /// 1. Filtri database (SQL WHERE) per ridurre candidati
    /// 2. Ricerca semantica sui candidati filtrati
    /// 3. Ordinamento per rilevanza
    /// </remarks>
    Task<List<RelevantDocumentResult>> SearchWithFiltersAsync(
        string semanticQuery,
        List<ExtractedFilter> filters,
        string userId,
        int topK = 10);

    /// <summary>
    /// Esegue self-query completo: parsing + ricerca in un'unica chiamata
    /// </summary>
    /// <param name="naturalLanguageQuery">Query in linguaggio naturale</param>
    /// <param name="userId">ID utente</param>
    /// <param name="topK">Numero di risultati</param>
    /// <returns>Risultati filtrati della ricerca</returns>
    /// <remarks>
    /// Metodo convenienza che:
    /// 1. Analizza query ed estrae filtri
    /// 2. Applica filtri e esegue ricerca
    /// 3. Restituisce risultati
    /// </remarks>
    Task<SelfQuerySearchResult> ExecuteSelfQueryAsync(
        string naturalLanguageQuery,
        string userId,
        int topK = 10);

    /// <summary>
    /// Valida e normalizza i filtri estratti
    /// </summary>
    /// <param name="filters">Filtri estratti da validare</param>
    /// <param name="availableFilters">Definizioni dei filtri disponibili</param>
    /// <returns>Filtri validati e normalizzati</returns>
    /// <remarks>
    /// Validazione include:
    /// - Verifica che i campi esistano
    /// - Controllo tipi di dato
    /// - Normalizzazione valori (es: date)
    /// - Rimozione filtri invalidi
    /// </remarks>
    Task<List<ExtractedFilter>> ValidateAndNormalizeFiltersAsync(
        List<ExtractedFilter> filters,
        List<FilterDefinition> availableFilters);

    /// <summary>
    /// Ottiene le definizioni dei filtri disponibili nel sistema
    /// </summary>
    /// <returns>Lista di filtri che possono essere estratti e applicati</returns>
    /// <remarks>
    /// Include filtri su:
    /// - Metadata documenti (categoria, tags, ecc.)
    /// - Date (upload, modifica)
    /// - Proprietà utente (owner, shared with)
    /// - Campi custom
    /// </remarks>
    Task<List<FilterDefinition>> GetAvailableFiltersAsync();
}

/// <summary>
/// Risultato del parsing self-query
/// </summary>
public class SelfQueryResult
{
    /// <summary>
    /// Query semantica estratta (senza filtri)
    /// </summary>
    public string SemanticQuery { get; set; } = string.Empty;

    /// <summary>
    /// Filtri strutturati estratti dalla query
    /// </summary>
    public List<ExtractedFilter> Filters { get; set; } = new();

    /// <summary>
    /// Indica se l'estrazione dei filtri è stata successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Messaggi di errore o warning durante l'estrazione
    /// </summary>
    public List<string> Messages { get; set; } = new();

    /// <summary>
    /// Query originale in linguaggio naturale
    /// </summary>
    public string OriginalQuery { get; set; } = string.Empty;
}

/// <summary>
/// Risultato completo di una self-query search
/// </summary>
public class SelfQuerySearchResult
{
    /// <summary>
    /// Risultati della ricerca
    /// </summary>
    public List<RelevantDocumentResult> Results { get; set; } = new();

    /// <summary>
    /// Query semantica utilizzata
    /// </summary>
    public string SemanticQuery { get; set; } = string.Empty;

    /// <summary>
    /// Filtri applicati
    /// </summary>
    public List<ExtractedFilter> AppliedFilters { get; set; } = new();

    /// <summary>
    /// Statistiche sulla ricerca
    /// </summary>
    public SearchStatistics Statistics { get; set; } = new();
}

/// <summary>
/// Filtro estratto dalla query in linguaggio naturale
/// </summary>
public class ExtractedFilter
{
    /// <summary>
    /// Nome del campo su cui filtrare (es: "category", "uploadDate")
    /// </summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>
    /// Operatore di confronto
    /// </summary>
    public FilterOperator Operator { get; set; }

    /// <summary>
    /// Valore del filtro (può essere string, number, date)
    /// </summary>
    public object Value { get; set; } = null!;

    /// <summary>
    /// Tipo di dato del valore
    /// </summary>
    public FilterValueType ValueType { get; set; }

    /// <summary>
    /// Operatore logico per combinare con altri filtri
    /// </summary>
    public LogicalOperator LogicalOperator { get; set; } = LogicalOperator.And;
}

/// <summary>
/// Definizione di un filtro disponibile nel sistema
/// </summary>
public class FilterDefinition
{
    /// <summary>
    /// Nome del campo (es: "category", "uploadDate")
    /// </summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>
    /// Nome user-friendly del campo (es: "Categoria", "Data di Upload")
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Tipo di dato del campo
    /// </summary>
    public FilterValueType DataType { get; set; }

    /// <summary>
    /// Operatori supportati per questo campo
    /// </summary>
    public List<FilterOperator> SupportedOperators { get; set; } = new();

    /// <summary>
    /// Valori possibili (per campi enum/category)
    /// </summary>
    public List<string>? PossibleValues { get; set; }

    /// <summary>
    /// Descrizione del campo per aiutare l'LLM nell'estrazione
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Esempi di uso del filtro in linguaggio naturale
    /// </summary>
    public List<string> Examples { get; set; } = new();
}

/// <summary>
/// Operatori di confronto per filtri
/// </summary>
public enum FilterOperator
{
    /// <summary>Uguale a</summary>
    Equals,
    /// <summary>Diverso da</summary>
    NotEquals,
    /// <summary>Maggiore di</summary>
    GreaterThan,
    /// <summary>Maggiore o uguale</summary>
    GreaterThanOrEqual,
    /// <summary>Minore di</summary>
    LessThan,
    /// <summary>Minore o uguale</summary>
    LessThanOrEqual,
    /// <summary>Contiene (per stringhe)</summary>
    Contains,
    /// <summary>Non contiene (per stringhe)</summary>
    NotContains,
    /// <summary>Inizia con (per stringhe)</summary>
    StartsWith,
    /// <summary>Finisce con (per stringhe)</summary>
    EndsWith,
    /// <summary>In lista (per array)</summary>
    In,
    /// <summary>Non in lista (per array)</summary>
    NotIn
}

/// <summary>
/// Tipo di valore del filtro
/// </summary>
public enum FilterValueType
{
    String,
    Number,
    Date,
    Boolean,
    Array
}

/// <summary>
/// Operatore logico per combinare filtri
/// </summary>
public enum LogicalOperator
{
    And,
    Or,
    Not
}

/// <summary>
/// Statistiche sulla ricerca self-query
/// </summary>
public class SearchStatistics
{
    /// <summary>
    /// Numero totale di documenti prima dei filtri
    /// </summary>
    public int TotalDocuments { get; set; }

    /// <summary>
    /// Numero di documenti dopo l'applicazione dei filtri
    /// </summary>
    public int FilteredDocuments { get; set; }

    /// <summary>
    /// Numero di risultati restituiti
    /// </summary>
    public int ReturnedResults { get; set; }

    /// <summary>
    /// Tempo impiegato per l'estrazione filtri (ms)
    /// </summary>
    public long FilterExtractionTimeMs { get; set; }

    /// <summary>
    /// Tempo impiegato per la ricerca (ms)
    /// </summary>
    public long SearchTimeMs { get; set; }

    /// <summary>
    /// Numero di filtri estratti
    /// </summary>
    public int FiltersExtracted { get; set; }
}
