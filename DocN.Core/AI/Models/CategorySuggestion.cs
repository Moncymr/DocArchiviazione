namespace DocN.Core.AI.Models;

/// <summary>
/// Suggerimento di categoria per un documento
/// </summary>
public class CategorySuggestion
{
    /// <summary>
    /// Nome della categoria suggerita
    /// </summary>
    public string CategoryName { get; set; } = string.Empty;
    
    /// <summary>
    /// Livello di confidenza (0-1)
    /// </summary>
    public double Confidence { get; set; }
    
    /// <summary>
    /// Motivazione del suggerimento
    /// </summary>
    public string Reasoning { get; set; } = string.Empty;
}
