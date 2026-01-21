using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using DocN.Core.Interfaces;
using DocN.Core.SemanticKernel;

namespace DocN.Core.Extensions;

/// <summary>
/// Extension methods for registering DocN services with Semantic Kernel and Gemini as default
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add all DocN Core services including Semantic Kernel with Gemini as default embedding provider
    /// </summary>
    public static IServiceCollection AddDocNServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure Semantic Kernel with Gemini as default
        services.Configure<SemanticKernelConfig>(configuration.GetSection("SemanticKernel"));

        // Register embedding service (uses Gemini by default)
        services.AddSingleton<IEmbeddingService, EmbeddingService>();

        // Register other core services
        // These will be implemented in subsequent phases
        // services.AddScoped<IDocumentExtractor, DocumentExtractor>();
        // services.AddScoped<IChunkingService, ChunkingService>();
        // services.AddScoped<ICategoryService, CategoryService>();
        // services.AddScoped<IRAGService, RAGService>();

        return services;
    }

    /// <summary>
    /// Add Semantic Kernel services with custom configuration
    /// </summary>
    public static IServiceCollection AddSemanticKernelServices(
        this IServiceCollection services, 
        Action<SemanticKernelConfig> configureOptions)
    {
        services.Configure(configureOptions);
        services.AddSingleton<IEmbeddingService, EmbeddingService>();
        
        return services;
    }
}
