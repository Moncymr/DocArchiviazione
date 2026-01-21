namespace DocN.Core.AI.Models;

/// <summary>
/// Risultato dell'analisi AI di un documento
/// </summary>
public class DocumentAnalysisResult
{
    /// <summary>
    /// Testo estratto dal documento
    /// </summary>
    public string ExtractedText { get; set; } = string.Empty;
    
    /// <summary>
    /// Suggerimenti di categoria
    /// </summary>
    public List<CategorySuggestion> CategorySuggestions { get; set; } = new();
    
    /// <summary>
    /// Embedding vettoriale del documento
    /// </summary>
    public DocumentEmbedding? Embedding { get; set; }
    
    /// <summary>
    /// Tag estratti dal documento
    /// </summary>
    public List<string> ExtractedTags { get; set; } = new();
    
    /// <summary>
    /// Metadati estratti
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}
