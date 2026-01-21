namespace DocN.Core.SemanticKernel;

/// <summary>
/// Configuration for Semantic Kernel with multiple AI providers
/// </summary>
public class SemanticKernelConfig
{
    /// <summary>
    /// Default embedding provider: Gemini, OpenAI, or AzureOpenAI
    /// </summary>
    public string DefaultEmbeddingProvider { get; set; } = "Gemini";

    /// <summary>
    /// Default chat completion provider
    /// </summary>
    public string DefaultChatProvider { get; set; } = "Gemini";

    /// <summary>
    /// Gemini configuration
    /// </summary>
    public GeminiConfig Gemini { get; set; } = new();

    /// <summary>
    /// OpenAI configuration
    /// </summary>
    public OpenAIConfig OpenAI { get; set; } = new();

    /// <summary>
    /// Azure OpenAI configuration
    /// </summary>
    public AzureOpenAIConfig AzureOpenAI { get; set; } = new();
}

/// <summary>
/// Gemini (Google AI) configuration
/// </summary>
public class GeminiConfig
{
    public string ApiKey { get; set; } = string.Empty;
    public string EmbeddingModel { get; set; } = "text-embedding-004"; // Latest Gemini embedding model
    public string ChatModel { get; set; } = "gemini-1.5-pro";
    public string? ApiEndpoint { get; set; }
}

/// <summary>
/// OpenAI configuration
/// </summary>
public class OpenAIConfig
{
    public string ApiKey { get; set; } = string.Empty;
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";
    public string ChatModel { get; set; } = "gpt-4-turbo";
    public string? OrganizationId { get; set; }
}

/// <summary>
/// Azure OpenAI configuration
/// </summary>
public class AzureOpenAIConfig
{
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string EmbeddingDeployment { get; set; } = "text-embedding-ada-002";
    public string ChatDeployment { get; set; } = "gpt-4";
    public string ApiVersion { get; set; } = "2024-02-15-preview";
}
