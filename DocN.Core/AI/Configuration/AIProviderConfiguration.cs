using DocN.Core.AI.Models;

namespace DocN.Core.AI.Configuration;

/// <summary>
/// Configurazione per i provider AI
/// </summary>
public class AIProviderConfiguration
{
    /// <summary>
    /// Provider predefinito da utilizzare
    /// </summary>
    public AIProviderType DefaultProvider { get; set; } = AIProviderType.AzureOpenAI;
    
    /// <summary>
    /// Configurazione Azure OpenAI
    /// </summary>
    public AzureOpenAIConfiguration? AzureOpenAI { get; set; }
    
    /// <summary>
    /// Configurazione OpenAI
    /// </summary>
    public OpenAIConfiguration? OpenAI { get; set; }
    
    /// <summary>
    /// Configurazione Google Gemini
    /// </summary>
    public GeminiConfiguration? Gemini { get; set; }
    
    /// <summary>
    /// Configurazione Ollama
    /// </summary>
    public OllamaConfiguration? Ollama { get; set; }
    
    /// <summary>
    /// Configurazione Groq
    /// </summary>
    public GroqConfiguration? Groq { get; set; }
}

/// <summary>
/// Configurazione per Azure OpenAI
/// </summary>
public class AzureOpenAIConfiguration
{
    /// <summary>
    /// Endpoint di Azure OpenAI
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;
    
    /// <summary>
    /// Chiave API
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;
    
    /// <summary>
    /// Nome del deployment per embeddings
    /// </summary>
    public string EmbeddingDeployment { get; set; } = "text-embedding-ada-002";
    
    /// <summary>
    /// Nome del deployment per chat/completion
    /// </summary>
    public string ChatDeployment { get; set; } = "gpt-4";
    
    /// <summary>
    /// Versione API
    /// </summary>
    public string ApiVersion { get; set; } = "2024-02-15-preview";
}

/// <summary>
/// Configurazione per OpenAI
/// </summary>
public class OpenAIConfiguration
{
    /// <summary>
    /// Chiave API OpenAI
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;
    
    /// <summary>
    /// Modello per embeddings
    /// </summary>
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";
    
    /// <summary>
    /// Modello per chat/completion
    /// </summary>
    public string ChatModel { get; set; } = "gpt-4-turbo";
    
    /// <summary>
    /// Organization ID (opzionale)
    /// </summary>
    public string? OrganizationId { get; set; }
}

/// <summary>
/// Configurazione per Google Gemini
/// </summary>
public class GeminiConfiguration
{
    /// <summary>
    /// Chiave API Gemini
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;
    
    /// <summary>
    /// Modello per embeddings
    /// </summary>
    public string EmbeddingModel { get; set; } = "text-embedding-004";
    
    /// <summary>
    /// Modello per generazione testo
    /// </summary>
    public string GenerationModel { get; set; } = "gemini-1.5-pro";
    
    /// <summary>
    /// Endpoint API (opzionale, default usa endpoint Google)
    /// </summary>
    public string? ApiEndpoint { get; set; }
}

/// <summary>
/// Configurazione per Ollama
/// </summary>
public class OllamaConfiguration
{
    /// <summary>
    /// Endpoint Ollama (default: http://localhost:11434)
    /// </summary>
    public string Endpoint { get; set; } = "http://localhost:11434";
    
    /// <summary>
    /// Modello per embeddings
    /// </summary>
    public string EmbeddingModel { get; set; } = "nomic-embed-text";
    
    /// <summary>
    /// Modello per chat/completion
    /// </summary>
    public string ChatModel { get; set; } = "llama3";
}

/// <summary>
/// Configurazione per Groq
/// </summary>
public class GroqConfiguration
{
    /// <summary>
    /// Chiave API Groq
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;
    
    /// <summary>
    /// Modello per embeddings - NOTA: Groq non supporta embeddings nativamente.
    /// Configurare un altro provider (Gemini, OpenAI, Ollama) per gli embeddings.
    /// </summary>
    [Obsolete("Groq does not support embeddings. Use another provider for embeddings.")]
    public string EmbeddingModel { get; set; } = string.Empty;
    
    /// <summary>
    /// Modello per chat/completion
    /// </summary>
    public string ChatModel { get; set; } = "llama-3.1-8b-instant";
    
    /// <summary>
    /// Endpoint API Groq
    /// </summary>
    public string Endpoint { get; set; } = "https://api.groq.com/openai/v1";
}
