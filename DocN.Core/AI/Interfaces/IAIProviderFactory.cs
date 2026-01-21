using DocN.Core.AI.Models;

namespace DocN.Core.AI.Interfaces;

/// <summary>
/// Factory per creare istanze di provider AI
/// </summary>
public interface IAIProviderFactory
{
    /// <summary>
    /// Crea un provider AI del tipo specificato
    /// </summary>
    /// <param name="providerType">Tipo di provider da creare</param>
    /// <returns>Istanza del provider AI</returns>
    IDocumentAIProvider CreateProvider(AIProviderType providerType);
    
    /// <summary>
    /// Ottiene il provider predefinito configurato
    /// </summary>
    /// <returns>Provider AI predefinito</returns>
    IDocumentAIProvider GetDefaultProvider();
}
