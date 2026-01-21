using Microsoft.SemanticKernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DocN.Data.Models;
using Azure.AI.OpenAI;
using Azure;

#pragma warning disable SKEXP0001 // Type is for evaluation purposes only
#pragma warning disable SKEXP0010 // Method is for evaluation purposes only

namespace DocN.Data.Services;

/// <summary>
/// Factory for creating Semantic Kernel instances from database AI configuration.
/// Ensures that all AI providers are loaded from the database, not from appsettings.json.
/// </summary>
public interface ISemanticKernelFactory
{
    /// <summary>
    /// Creates a Kernel instance using the active AI configuration from the database.
    /// </summary>
    Task<Kernel> CreateKernelAsync();
}

/// <summary>
/// Implementation of ISemanticKernelFactory that creates Kernel from database configuration.
/// </summary>
public class SemanticKernelFactory : ISemanticKernelFactory, IDisposable
{
    // Default model names as constants for consistency
    private const string DefaultAzureOpenAIChatModel = "gpt-4";
    private const string DefaultAzureOpenAIEmbeddingModel = "text-embedding-ada-002";
    private const string DefaultOpenAIChatModel = "gpt-4";
    private const string DefaultOpenAIEmbeddingModel = "text-embedding-ada-002";
    
    private readonly IMultiProviderAIService _aiService;
    private readonly ILogger<SemanticKernelFactory> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private Kernel? _cachedKernel;
    private DateTime _lastKernelCreation = DateTime.MinValue;
    private readonly TimeSpan _kernelCacheDuration = TimeSpan.FromMinutes(5);
    private bool _disposed;

    public SemanticKernelFactory(
        IMultiProviderAIService aiService,
        ILogger<SemanticKernelFactory> logger)
    {
        _aiService = aiService;
        _logger = logger;
    }

    public async Task<Kernel> CreateKernelAsync()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SemanticKernelFactory));
        }

        await _semaphore.WaitAsync();
        try
        {
            // Check if cached kernel is still valid (within lock to prevent race conditions)
            if (_cachedKernel != null && DateTime.UtcNow - _lastKernelCreation < _kernelCacheDuration)
            {
                _logger.LogDebug("Using cached Semantic Kernel instance");
                return _cachedKernel;
            }

            _logger.LogInformation("Creating Semantic Kernel from database AI configuration...");

            // Get active configuration from database
            var config = await _aiService.GetActiveConfigurationAsync();
            
            if (config == null)
            {
                _logger.LogWarning("No active AI configuration found in database. Creating empty Kernel.");
                // Create an empty kernel - services will need to handle this gracefully
                _cachedKernel = Kernel.CreateBuilder().Build();
                _lastKernelCreation = DateTime.UtcNow;
                return _cachedKernel;
            }

            var kernelBuilder = Kernel.CreateBuilder();
            bool hasAnyProvider = false;

            // Determine which provider to use for chat and embeddings
            var chatProvider = config.ChatProvider ?? config.ProviderType;
            var embeddingProvider = config.EmbeddingsProvider ?? config.ProviderType;

            _logger.LogInformation(
                "Configuring Semantic Kernel with Chat Provider: {ChatProvider}, Embedding Provider: {EmbeddingProvider}",
                chatProvider, embeddingProvider);

            // Configure Azure OpenAI if it's the provider or has valid keys
            if ((chatProvider == AIProviderType.AzureOpenAI || embeddingProvider == AIProviderType.AzureOpenAI) &&
                !string.IsNullOrEmpty(config.AzureOpenAIEndpoint) && !string.IsNullOrEmpty(config.AzureOpenAIKey))
            {
                try
                {
                    var endpoint = config.AzureOpenAIEndpoint;
                    var apiKey = config.AzureOpenAIKey;

                    if (chatProvider == AIProviderType.AzureOpenAI)
                    {
                        var chatDeployment = config.ChatDeploymentName ?? config.AzureOpenAIChatModel ?? DefaultAzureOpenAIChatModel;
                        kernelBuilder.AddAzureOpenAIChatCompletion(
                            deploymentName: chatDeployment,
                            endpoint: endpoint,
                            apiKey: apiKey);
                        _logger.LogInformation("Added Azure OpenAI Chat Completion: {Deployment}", chatDeployment);
                        hasAnyProvider = true;
                    }

                    if (embeddingProvider == AIProviderType.AzureOpenAI)
                    {
                        var embeddingDeployment = config.EmbeddingDeploymentName ?? config.AzureOpenAIEmbeddingModel ?? DefaultAzureOpenAIEmbeddingModel;
                        kernelBuilder.AddAzureOpenAITextEmbeddingGeneration(
                            deploymentName: embeddingDeployment,
                            endpoint: endpoint,
                            apiKey: apiKey);
                        _logger.LogInformation("Added Azure OpenAI Embedding: {Deployment}", embeddingDeployment);
                        hasAnyProvider = true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to configure Azure OpenAI in Semantic Kernel");
                }
            }

            // Configure OpenAI if it's the provider or has valid keys
            if ((chatProvider == AIProviderType.OpenAI || embeddingProvider == AIProviderType.OpenAI) &&
                !string.IsNullOrEmpty(config.OpenAIApiKey))
            {
                try
                {
                    var apiKey = config.OpenAIApiKey;

                    if (chatProvider == AIProviderType.OpenAI)
                    {
                        var chatModel = config.OpenAIChatModel ?? DefaultOpenAIChatModel;
                        kernelBuilder.AddOpenAIChatCompletion(
                            modelId: chatModel,
                            apiKey: apiKey);
                        _logger.LogInformation("Added OpenAI Chat Completion: {Model}", chatModel);
                        hasAnyProvider = true;
                    }

                    if (embeddingProvider == AIProviderType.OpenAI)
                    {
                        var embeddingModel = config.OpenAIEmbeddingModel ?? DefaultOpenAIEmbeddingModel;
                        kernelBuilder.AddOpenAITextEmbeddingGeneration(
                            modelId: embeddingModel,
                            apiKey: apiKey);
                        _logger.LogInformation("Added OpenAI Embedding: {Model}", embeddingModel);
                        hasAnyProvider = true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to configure OpenAI in Semantic Kernel");
                }
            }

            // Note: Gemini is not directly supported by Semantic Kernel's built-in services
            // It's handled separately through MultiProviderAIService
            if (chatProvider == AIProviderType.Gemini || embeddingProvider == AIProviderType.Gemini)
            {
                _logger.LogInformation(
                    "Gemini provider detected. Note: Gemini is handled through MultiProviderAIService, not Semantic Kernel directly.");
            }

            if (!hasAnyProvider)
            {
                _logger.LogWarning(
                    "No supported AI provider configured in Semantic Kernel. " +
                    "Kernel will be created but may not be functional for services that depend on it. " +
                    "Configure Azure OpenAI or OpenAI in the database for full Semantic Kernel functionality.");
            }

            _cachedKernel = kernelBuilder.Build();
            _lastKernelCreation = DateTime.UtcNow;

            _logger.LogInformation("Semantic Kernel created successfully from database configuration");
            
            return _cachedKernel;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _semaphore.Dispose();
        _disposed = true;
    }
}
