using DocN.Core.AI.Configuration;
using DocN.Core.AI.Interfaces;
using DocN.Core.AI.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DocN.Core.AI.Providers;

/// <summary>
/// Factory per creare provider AI
/// </summary>
public class AIProviderFactory : IAIProviderFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly AIProviderConfiguration _configuration;

    public AIProviderFactory(
        IServiceProvider serviceProvider,
        IOptions<AIProviderConfiguration> configuration)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration.Value;
    }

    public IDocumentAIProvider CreateProvider(AIProviderType providerType)
    {
        return providerType switch
        {
            AIProviderType.AzureOpenAI => _serviceProvider.GetRequiredService<AzureOpenAIProvider>(),
            AIProviderType.OpenAI => _serviceProvider.GetRequiredService<OpenAIProvider>(),
            AIProviderType.Gemini => _serviceProvider.GetRequiredService<GeminiProvider>(),
            AIProviderType.Ollama => _serviceProvider.GetRequiredService<OllamaProvider>(),
            AIProviderType.Groq => _serviceProvider.GetRequiredService<GroqProvider>(),
            _ => throw new ArgumentException($"Provider type {providerType} not supported", nameof(providerType))
        };
    }

    public IDocumentAIProvider GetDefaultProvider()
    {
        return CreateProvider(_configuration.DefaultProvider);
    }
}
