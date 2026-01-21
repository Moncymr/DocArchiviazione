namespace DocN.Core.Interfaces;

/// <summary>
/// Interface for category suggestion service using AI
/// </summary>
public interface ICategoryService
{
    /// <summary>
    /// Suggest category using dual approach: Direct AI + Vector similarity
    /// </summary>
    /// <param name="documentText">Document text</param>
    /// <param name="embedding">Document embedding vector</param>
    /// <param name="availableCategories">List of available categories</param>
    /// <returns>Category suggestion with confidence</returns>
    Task<CategorySuggestion> SuggestCategoryAsync(string documentText, float[]? embedding, List<string> availableCategories);

    /// <summary>
    /// Suggest category using direct AI classification (Gemini by default)
    /// </summary>
    /// <param name="documentText">Document text</param>
    /// <param name="availableCategories">List of available categories</param>
    /// <returns>Suggested category with confidence</returns>
    Task<(string Category, float Confidence)> SuggestCategoryDirectAsync(string documentText, List<string> availableCategories);

    /// <summary>
    /// Suggest category using vector similarity with existing documents
    /// </summary>
    /// <param name="embedding">Document embedding</param>
    /// <returns>Suggested category with confidence</returns>
    Task<(string Category, float Confidence)> SuggestCategoryVectorAsync(float[] embedding);
}

/// <summary>
/// Result of category suggestion with both methods
/// </summary>
public class CategorySuggestion
{
    public string AICategory { get; set; } = string.Empty;
    public float AIConfidence { get; set; }
    public string VectorCategory { get; set; } = string.Empty;
    public float VectorConfidence { get; set; }
    public string FinalCategory { get; set; } = string.Empty;
    public float FinalConfidence { get; set; }
    public string Method { get; set; } = string.Empty; // "AI", "Vector", or "Hybrid"
}
