namespace DocN.Core.AI.Models;

/// <summary>
/// Tipo di provider AI supportato
/// </summary>
public enum AIProviderType
{
    /// <summary>
    /// Azure OpenAI Service
    /// </summary>
    AzureOpenAI,
    
    /// <summary>
    /// OpenAI API
    /// </summary>
    OpenAI,
    
    /// <summary>
    /// Google Gemini API
    /// </summary>
    Gemini,
    
    /// <summary>
    /// Ollama (local AI models)
    /// </summary>
    Ollama,
    
    /// <summary>
    /// Groq (fast cloud API)
    /// </summary>
    Groq
}
