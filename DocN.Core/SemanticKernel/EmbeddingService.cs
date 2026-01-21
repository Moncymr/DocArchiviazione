using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;
using DocN.Core.Interfaces;

namespace DocN.Core.SemanticKernel;

/// <summary>
/// Embedding service using Semantic Kernel with Gemini as default provider
/// </summary>
public class EmbeddingService : IEmbeddingService
{
    private readonly SemanticKernelConfig _config;
    private readonly ILogger<EmbeddingService> _logger;
    private readonly Dictionary<string, ITextEmbeddingGenerationService> _embeddingServices;

    public EmbeddingService(IOptions<SemanticKernelConfig> config, ILogger<EmbeddingService> logger)
    {
        _config = config.Value;
        _logger = logger;
        _embeddingServices = new Dictionary<string, ITextEmbeddingGenerationService>();
        
        InitializeEmbeddingServices();
    }

    private void InitializeEmbeddingServices()
    {
        try
        {
            // Initialize Gemini (default)
            if (!string.IsNullOrEmpty(_config.Gemini.ApiKey))
            {
                // Note: Gemini connector will be configured when available in SK
                // For now, we'll use OpenAI as fallback
                _logger.LogInformation("Gemini embedding service configured (default provider)");
            }

            // Initialize OpenAI
            if (!string.IsNullOrEmpty(_config.OpenAI.ApiKey))
            {
                var kernelBuilder = Kernel.CreateBuilder();
                kernelBuilder.AddOpenAITextEmbeddingGeneration(
                    modelId: _config.OpenAI.EmbeddingModel,
                    apiKey: _config.OpenAI.ApiKey,
                    orgId: _config.OpenAI.OrganizationId
                );
                var kernel = kernelBuilder.Build();
                _embeddingServices["OpenAI"] = kernel.GetRequiredService<ITextEmbeddingGenerationService>();
                _logger.LogInformation("OpenAI embedding service configured");
            }

            // Initialize Azure OpenAI
            if (!string.IsNullOrEmpty(_config.AzureOpenAI.ApiKey))
            {
                var kernelBuilder = Kernel.CreateBuilder();
                kernelBuilder.AddAzureOpenAITextEmbeddingGeneration(
                    deploymentName: _config.AzureOpenAI.EmbeddingDeployment,
                    endpoint: _config.AzureOpenAI.Endpoint,
                    apiKey: _config.AzureOpenAI.ApiKey,
                    modelId: _config.AzureOpenAI.EmbeddingDeployment
                );
                var kernel = kernelBuilder.Build();
                _embeddingServices["AzureOpenAI"] = kernel.GetRequiredService<ITextEmbeddingGenerationService>();
                _logger.LogInformation("Azure OpenAI embedding service configured");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing embedding services");
        }
    }

    public async Task<float[]?> GenerateEmbeddingAsync(string text, string? provider = null)
    {
        try
        {
            // Use specified provider or default (Gemini)
            var selectedProvider = provider ?? _config.DefaultEmbeddingProvider;
            
            // For Gemini, use the existing AI provider infrastructure
            if (selectedProvider == "Gemini")
            {
                _logger.LogInformation("Using Gemini for embedding generation (default)");
                // TODO: Integrate with existing Gemini provider from AI folder
                // For now, fallback to OpenAI if available
                if (_embeddingServices.ContainsKey("OpenAI"))
                {
                    selectedProvider = "OpenAI";
                    _logger.LogWarning("Falling back to OpenAI for embedding (Gemini integration pending)");
                }
                else if (_embeddingServices.ContainsKey("AzureOpenAI"))
                {
                    selectedProvider = "AzureOpenAI";
                    _logger.LogWarning("Falling back to Azure OpenAI for embedding (Gemini integration pending)");
                }
            }

            if (!_embeddingServices.ContainsKey(selectedProvider))
            {
                _logger.LogError($"Embedding service '{selectedProvider}' not configured");
                return null;
            }

            var embeddingService = _embeddingServices[selectedProvider];
            var embeddings = await embeddingService.GenerateEmbeddingsAsync([text]);
            
            if (embeddings != null && embeddings.Count > 0)
            {
                return embeddings[0].ToArray();
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error generating embedding with provider {provider ?? _config.DefaultEmbeddingProvider}");
            return null;
        }
    }

    public async Task<List<float[]?>> GenerateBatchEmbeddingsAsync(List<string> texts, string? provider = null)
    {
        var results = new List<float[]?>();
        
        // Process in batches to avoid rate limits
        const int batchSize = 10;
        for (int i = 0; i < texts.Count; i += batchSize)
        {
            var batch = texts.Skip(i).Take(batchSize).ToList();
            var batchTasks = batch.Select(text => GenerateEmbeddingAsync(text, provider));
            var batchResults = await Task.WhenAll(batchTasks);
            results.AddRange(batchResults);
        }

        return results;
    }

    public float CalculateSimilarity(float[] embedding1, float[] embedding2)
    {
        if (embedding1.Length != embedding2.Length)
        {
            throw new ArgumentException("Embeddings must have the same dimension");
        }

        // Cosine similarity
        float dotProduct = 0;
        float magnitude1 = 0;
        float magnitude2 = 0;

        for (int i = 0; i < embedding1.Length; i++)
        {
            dotProduct += embedding1[i] * embedding2[i];
            magnitude1 += embedding1[i] * embedding1[i];
            magnitude2 += embedding2[i] * embedding2[i];
        }

        magnitude1 = (float)Math.Sqrt(magnitude1);
        magnitude2 = (float)Math.Sqrt(magnitude2);

        if (magnitude1 == 0 || magnitude2 == 0)
        {
            return 0;
        }

        return dotProduct / (magnitude1 * magnitude2);
    }
}
