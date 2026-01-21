namespace DocN.Core.Interfaces;

/// <summary>
/// Servizio per il re-ranking avanzato dei risultati di ricerca
/// Utilizza cross-encoder e modelli AI per riordinare i risultati in base alla rilevanza effettiva
/// </summary>
/// <remarks>
/// Il re-ranking è una tecnica RAG avanzata che:
/// - Migliora la precisione riordinando i risultati dopo la ricerca iniziale
/// - Utilizza modelli più potenti (cross-encoder) rispetto ai bi-encoder usati per embeddings
/// - Calcola score di rilevanza contestuali tra query e documenti
/// - Riduce false positives e migliora la qualità complessiva dei risultati
/// 
/// Differenza tra Bi-encoder e Cross-encoder:
/// - Bi-encoder: Codifica query e documenti separatamente (veloce, usato per retrieval iniziale)
/// - Cross-encoder: Processa query+documento insieme (più accurato ma lento, usato per re-ranking)
/// </remarks>
public interface IReRankingService
{
    /// <summary>
    /// Riordina i risultati di ricerca usando cross-encoder per calcolare rilevanza contestuale
    /// </summary>
    /// <param name="query">Query originale dell'utente</param>
    /// <param name="results">Risultati iniziali da riordinare</param>
    /// <param name="topK">Numero di risultati migliori da restituire dopo re-ranking</param>
    /// <returns>Risultati riordinati per rilevanza effettiva</returns>
    /// <remarks>
    /// Il re-ranking calcola score di rilevanza più accurati considerando:
    /// - Interazione semantica tra query e documento
    /// - Contesto completo di entrambi
    /// - Matching di concetti astratti oltre keywords
    /// </remarks>
    Task<List<RelevantDocumentResult>> ReRankResultsAsync(
        string query,
        List<RelevantDocumentResult> results,
        int topK);

    /// <summary>
    /// Calcola lo score di rilevanza contestuale tra query e un documento
    /// </summary>
    /// <param name="query">Query dell'utente</param>
    /// <param name="documentText">Testo del documento da valutare</param>
    /// <returns>Score di rilevanza tra 0 e 1, dove 1 indica massima rilevanza</returns>
    /// <remarks>
    /// Utilizza un modello cross-encoder che:
    /// 1. Concatena query + documento
    /// 2. Processa insieme attraverso transformer
    /// 3. Produce score di rilevanza calibrato
    /// 
    /// Più accurato del cosine similarity su embeddings separati
    /// </remarks>
    Task<double> CalculateRelevanceScoreAsync(string query, string documentText);

    /// <summary>
    /// Riordina risultati usando un approccio ibrido: cross-encoder + LLM
    /// </summary>
    /// <param name="query">Query originale</param>
    /// <param name="results">Risultati da riordinare</param>
    /// <param name="topK">Numero di top risultati da restituire</param>
    /// <returns>Risultati riordinati con score ibrido</returns>
    /// <remarks>
    /// Approccio ibrido:
    /// 1. Cross-encoder per score tecnico di matching
    /// 2. LLM per valutazione semantica profonda
    /// 3. Combinazione pesata degli score
    /// 
    /// Più lento ma molto più accurato per query complesse
    /// </remarks>
    Task<List<RelevantDocumentResult>> ReRankWithLLMAsync(
        string query,
        List<RelevantDocumentResult> results,
        int topK);

    /// <summary>
    /// Filtra risultati che non raggiungono una soglia minima di rilevanza dopo re-ranking
    /// </summary>
    /// <param name="query">Query originale</param>
    /// <param name="results">Risultati da filtrare</param>
    /// <param name="minRelevanceScore">Soglia minima di rilevanza (0-1)</param>
    /// <returns>Solo risultati che superano la soglia</returns>
    /// <remarks>
    /// Utile per eliminare risultati marginali che potrebbero confondere l'utente
    /// o peggiorare la qualità della risposta RAG
    /// </remarks>
    Task<List<RelevantDocumentResult>> FilterByRelevanceThresholdAsync(
        string query,
        List<RelevantDocumentResult> results,
        double minRelevanceScore = 0.5);
}

/// <summary>
/// Configurazione per il servizio di re-ranking
/// </summary>
public class ReRankingConfiguration
{
    /// <summary>
    /// Indica se il re-ranking è abilitato
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Modello da utilizzare per il re-ranking
    /// </summary>
    /// <remarks>
    /// Opzioni comuni:
    /// - "cross-encoder" (default, veloce e accurato)
    /// - "llm-based" (più lento ma molto accurato)
    /// - "hybrid" (combinazione di entrambi)
    /// </remarks>
    public string Model { get; set; } = "cross-encoder";

    /// <summary>
    /// Soglia minima di rilevanza per includere un risultato (0-1)
    /// </summary>
    public double MinRelevanceScore { get; set; } = 0.3;

    /// <summary>
    /// Numero massimo di risultati da processare nel re-ranking
    /// </summary>
    /// <remarks>
    /// Limita il numero per performance. Tipicamente 20-50 risultati.
    /// </remarks>
    public int MaxCandidates { get; set; } = 30;

    /// <summary>
    /// Peso dello score cross-encoder nell'approccio ibrido (0-1)
    /// </summary>
    public double CrossEncoderWeight { get; set; } = 0.6;

    /// <summary>
    /// Peso dello score LLM nell'approccio ibrido (0-1)
    /// </summary>
    public double LLMWeight { get; set; } = 0.4;
}
