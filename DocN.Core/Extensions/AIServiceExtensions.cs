using DocN.Core.AI.Configuration;
using DocN.Core.AI.Interfaces;
using DocN.Core.AI.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DocN.Core.Extensions;

/// <summary>
/// Estensioni per registrare i servizi AI nella dependency injection
/// </summary>
public static class AIServiceExtensions
{
    /// <summary>
    /// Aggiunge i servizi AI al container di dependency injection
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Configuration</param>
    /// <returns>Service collection per chaining</returns>
    public static IServiceCollection AddDocNAIServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Registra la configurazione
        services.Configure<AIProviderConfiguration>(
            configuration.GetSection("AIProvider"));

        // Registra i provider come servizi transient
        services.AddTransient<AzureOpenAIProvider>();
        services.AddTransient<OpenAIProvider>();
        services.AddTransient<GeminiProvider>();
        services.AddTransient<OllamaProvider>();
        services.AddTransient<GroqProvider>();

        // Registra la factory come singleton
        services.AddSingleton<IAIProviderFactory, AIProviderFactory>();

        return services;
    }
}
