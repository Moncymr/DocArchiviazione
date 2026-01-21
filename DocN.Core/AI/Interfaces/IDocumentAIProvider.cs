using DocN.Core.AI.Models;

namespace DocN.Core.AI.Interfaces;

/// <summary>
/// Interfaccia per i provider AI che gestiscono l'analisi e l'elaborazione dei documenti
/// </summary>
public interface IDocumentAIProvider
{
    /// <summary>
    /// Tipo di provider AI
    /// </summary>
    AIProviderType ProviderType { get; }
    
    /// <summary>
    /// Nome del provider
    /// </summary>
    string ProviderName { get; }
    
    /// <summary>
    /// Genera embedding vettoriale per un testo
    /// </summary>
    /// <param name="text">Testo da elaborare</param>
    /// <param name="cancellationToken">Token di cancellazione</param>
    /// <returns>Embedding vettoriale</returns>
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Suggerisce categorie per un documento basandosi sul contenuto
    /// </summary>
    /// <param name="documentText">Testo del documento</param>
    /// <param name="availableCategories">Categorie disponibili nel sistema</param>
    /// <param name="cancellationToken">Token di cancellazione</param>
    /// <returns>Lista di suggerimenti di categoria</returns>
    Task<List<CategorySuggestion>> SuggestCategoriesAsync(
        string documentText, 
        List<string> availableCategories, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Estrae tag rilevanti da un documento
    /// </summary>
    /// <param name="documentText">Testo del documento</param>
    /// <param name="cancellationToken">Token di cancellazione</param>
    /// <returns>Lista di tag estratti</returns>
    Task<List<string>> ExtractTagsAsync(
        string documentText,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Estrae metadati strutturati da un documento (es. fatture, contratti)
    /// </summary>
    /// <param name="documentText">Testo del documento</param>
    /// <param name="fileName">Nome del file per aiutare a identificare il tipo</param>
    /// <param name="cancellationToken">Token di cancellazione</param>
    /// <returns>Dizionario con metadati estratti (es. invoice_number, date, author, etc.)</returns>
    Task<Dictionary<string, string>> ExtractMetadataAsync(
        string documentText,
        string fileName = "",
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Analizza completamente un documento
    /// </summary>
    /// <param name="documentText">Testo del documento</param>
    /// <param name="availableCategories">Categorie disponibili nel sistema</param>
    /// <param name="cancellationToken">Token di cancellazione</param>
    /// <returns>Risultato completo dell'analisi</returns>
    Task<DocumentAnalysisResult> AnalyzeDocumentAsync(
        string documentText, 
        List<string> availableCategories, 
        CancellationToken cancellationToken = default);
}
