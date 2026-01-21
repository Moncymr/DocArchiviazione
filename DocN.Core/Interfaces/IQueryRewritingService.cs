namespace DocN.Core.Interfaces;

/// <summary>
/// Servizio per la riformulazione intelligente delle query
/// Migliora le query utente attraverso espansione, riformulazione e multi-query generation
/// </summary>
/// <remarks>
/// Query Rewriting è una tecnica RAG avanzata che:
/// - Espande query ambigue con sinonimi e termini correlati
/// - Riformula query poco chiare in versioni più specifiche
/// - Genera multiple varianti della query per migliorare il recall
/// - Semplifica query complesse in sotto-query più gestibili
/// </remarks>
public interface IQueryRewritingService
{
    /// <summary>
    /// Riformula una query ambigua o poco chiara in una versione più specifica e ricercabile
    /// </summary>
    /// <param name="originalQuery">Query originale dell'utente</param>
    /// <param name="conversationContext">Contesto conversazionale opzionale per disambiguazione</param>
    /// <returns>Query riformulata più chiara e specifica</returns>
    /// <remarks>
    /// Esempio: "quello" → "il documento sull'analisi finanziaria Q3 2024"
    /// Utilizza il contesto conversazionale per risolvere riferimenti ambigui
    /// </remarks>
    Task<string> RewriteQueryAsync(string originalQuery, string? conversationContext = null);

    /// <summary>
    /// Espande una query con sinonimi, termini correlati e variazioni linguistiche
    /// </summary>
    /// <param name="query">Query da espandere</param>
    /// <param name="maxExpansions">Numero massimo di termini aggiuntivi da includere</param>
    /// <returns>Query espansa con termini aggiuntivi</returns>
    /// <remarks>
    /// Esempio: "fattura" → "fattura OR invoice OR ricevuta OR documento fiscale"
    /// Migliora il recall includendo varianti semantiche
    /// </remarks>
    Task<string> ExpandQueryAsync(string query, int maxExpansions = 3);

    /// <summary>
    /// Genera multiple varianti della query per aumentare la copertura della ricerca
    /// </summary>
    /// <param name="query">Query originale</param>
    /// <param name="numVariants">Numero di varianti da generare (default: 3)</param>
    /// <returns>Lista di query varianti con prospettive diverse</returns>
    /// <remarks>
    /// Esempio per "Come migliorare le vendite?":
    /// 1. "Strategie per incrementare il fatturato"
    /// 2. "Tecniche di ottimizzazione commerciale"
    /// 3. "Best practices per aumentare le performance di vendita"
    /// 
    /// Permette di recuperare documenti rilevanti che usano terminologia diversa
    /// </remarks>
    Task<List<string>> GenerateMultiQueryVariantsAsync(string query, int numVariants = 3);

    /// <summary>
    /// Decompone una query complessa in sotto-query più semplici e gestibili
    /// </summary>
    /// <param name="complexQuery">Query complessa con multiple domande o concetti</param>
    /// <returns>Lista di sotto-query più semplici</returns>
    /// <remarks>
    /// Esempio: "Qual è il budget 2024 e chi sono i responsabili del progetto X?"
    /// → ["budget 2024", "responsabili progetto X"]
    /// 
    /// Permette di processare separatamente ogni parte e combinare i risultati
    /// </remarks>
    Task<List<string>> DecomposeComplexQueryAsync(string complexQuery);

    /// <summary>
    /// Analizza una query per identificare possibili ambiguità o problemi
    /// </summary>
    /// <param name="query">Query da analizzare</param>
    /// <returns>Risultato dell'analisi con suggerimenti di miglioramento</returns>
    Task<QueryAnalysisResult> AnalyzeQueryQualityAsync(string query);
}

/// <summary>
/// Risultato dell'analisi di qualità di una query
/// </summary>
public class QueryAnalysisResult
{
    /// <summary>
    /// Score di qualità della query (0-1, dove 1 è ottimale)
    /// </summary>
    public double QualityScore { get; set; }

    /// <summary>
    /// Indica se la query è troppo vaga o ambigua
    /// </summary>
    public bool IsAmbiguous { get; set; }

    /// <summary>
    /// Indica se la query è troppo complessa
    /// </summary>
    public bool IsComplex { get; set; }

    /// <summary>
    /// Indica se la query è troppo generica
    /// </summary>
    public bool IsTooGeneric { get; set; }

    /// <summary>
    /// Suggerimenti per migliorare la query
    /// </summary>
    public List<string> Suggestions { get; set; } = new();

    /// <summary>
    /// Query suggerita riformulata (se necessario)
    /// </summary>
    public string? SuggestedRewrite { get; set; }
}
