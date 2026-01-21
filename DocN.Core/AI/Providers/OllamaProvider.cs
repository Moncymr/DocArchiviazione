using DocN.Core.AI.Configuration;
using DocN.Core.AI.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OllamaSharp;
using OllamaSharp.Models;
using System.Text.Json;

namespace DocN.Core.AI.Providers;

/// <summary>
/// Provider per Ollama (modelli AI locali)
/// </summary>
public class OllamaProvider : BaseAIProvider
{
    private readonly OllamaConfiguration _config;
    private readonly OllamaApiClient _client;

    public override AIProviderType ProviderType => AIProviderType.Ollama;
    public override string ProviderName => "Ollama";

    public OllamaProvider(
        IOptions<AIProviderConfiguration> configuration,
        ILogger<OllamaProvider> logger) : base(logger)
    {
        _config = configuration.Value.Ollama 
            ?? throw new InvalidOperationException("Ollama configuration not found");

        if (string.IsNullOrEmpty(_config.Endpoint))
        {
            throw new InvalidOperationException("Ollama Endpoint is required");
        }

        _client = new OllamaApiClient(new Uri(_config.Endpoint));
        _client.SelectedModel = _config.ChatModel;
    }

    public override async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Text cannot be null, empty, or whitespace", nameof(text));
        }

        _logger.LogInformation("Generating embedding with Ollama for text of length {Length}", text.Length);

        try
        {
            var request = new EmbedRequest
            {
                Model = _config.EmbeddingModel,
                Input = new List<string> { text }
            };
            
            var response = await _client.EmbedAsync(request, cancellationToken);
            
            if (response?.Embeddings == null || !response.Embeddings.Any())
            {
                throw new InvalidOperationException("Failed to generate embedding with Ollama: Response was null or empty");
            }
            
            // Get the first embedding (since we only sent one input)
            var embedding = response.Embeddings.First();
            _logger.LogInformation("Successfully generated embedding with {Dimensions} dimensions", embedding.Length);
            return embedding;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating embedding with Ollama. Model: {Model}, Text length: {Length}", 
                _config.EmbeddingModel, text.Length);
            throw;
        }
    }

    public override async Task<List<CategorySuggestion>> SuggestCategoriesAsync(
        string documentText, 
        List<string> availableCategories, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Suggesting categories with Ollama");

        var prompt = BuildCategorySuggestionPrompt(documentText, availableCategories);

        var chat = new Chat(_client);
        var responseBuilder = new System.Text.StringBuilder();
        
        await foreach (var token in chat.SendAsync(prompt, cancellationToken: cancellationToken))
        {
            responseBuilder.Append(token);
        }
        
        var response = responseBuilder.ToString();
        if (!string.IsNullOrEmpty(response))
        {
            return ParseCategorySuggestions(response);
        }

        return new List<CategorySuggestion>();
    }

    public override async Task<List<string>> ExtractTagsAsync(
        string documentText,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Extracting tags with Ollama");

        try
        {
            var prompt = BuildTagExtractionPrompt(documentText);

            var chat = new Chat(_client);
            var responseBuilder = new System.Text.StringBuilder();
            
            await foreach (var token in chat.SendAsync(prompt, cancellationToken: cancellationToken))
            {
                responseBuilder.Append(token);
            }
            
            var response = responseBuilder.ToString();
            if (!string.IsNullOrEmpty(response))
            {
                return ParseTags(response);
            }

            return new List<string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting tags with Ollama");
            return new List<string>();
        }
    }

    public override async Task<Dictionary<string, string>> ExtractMetadataAsync(
        string documentText,
        string fileName = "",
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Extracting metadata with Ollama for file: {FileName}", fileName);

        try
        {
            var prompt = BuildMetadataExtractionPrompt(documentText, fileName);

            var chat = new Chat(_client);
            var responseBuilder = new System.Text.StringBuilder();
            
            await foreach (var token in chat.SendAsync(prompt, cancellationToken: cancellationToken))
            {
                responseBuilder.Append(token);
            }
            
            var response = responseBuilder.ToString();
            if (!string.IsNullOrEmpty(response))
            {
                return ParseMetadata(response);
            }

            return new Dictionary<string, string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting metadata with Ollama");
            return new Dictionary<string, string>();
        }
    }

    private List<CategorySuggestion> ParseCategorySuggestions(string jsonResponse)
    {
        try
        {
            // Clean up potential JSON markers
            var cleanedResponse = CleanJsonResponse(jsonResponse);

            using var jsonDoc = JsonDocument.Parse(cleanedResponse);
            var suggestions = new List<CategorySuggestion>();

            if (jsonDoc.RootElement.TryGetProperty("suggestions", out var suggestionsArray))
            {
                foreach (var item in suggestionsArray.EnumerateArray())
                {
                    suggestions.Add(new CategorySuggestion
                    {
                        CategoryName = item.GetProperty("categoryName").GetString() ?? string.Empty,
                        Confidence = item.GetProperty("confidence").GetDouble(),
                        Reasoning = item.GetProperty("reasoning").GetString() ?? string.Empty
                    });
                }
            }

            return suggestions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing category suggestions from Ollama");
            return new List<CategorySuggestion>();
        }
    }

    private List<string> ParseTags(string jsonResponse)
    {
        try
        {
            // Clean up potential JSON markers
            var cleanedResponse = CleanJsonResponse(jsonResponse);

            using var jsonDoc = JsonDocument.Parse(cleanedResponse);
            var tags = new List<string>();

            if (jsonDoc.RootElement.TryGetProperty("tags", out var tagsArray))
            {
                foreach (var item in tagsArray.EnumerateArray())
                {
                    var tag = item.GetString();
                    if (!string.IsNullOrEmpty(tag))
                    {
                        tags.Add(tag);
                    }
                }
            }

            return tags;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing tags from Ollama");
            return new List<string>();
        }
    }

    private Dictionary<string, string> ParseMetadata(string jsonResponse)
    {
        try
        {
            // Clean up potential JSON markers
            var cleanedResponse = CleanJsonResponse(jsonResponse);

            using var jsonDoc = JsonDocument.Parse(cleanedResponse);
            var metadata = new Dictionary<string, string>();

            foreach (var property in jsonDoc.RootElement.EnumerateObject())
            {
                // Convert all JSON value types to strings
                var value = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                    JsonValueKind.Number => property.Value.GetRawText(), // Get the raw number as string
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    JsonValueKind.Null => string.Empty,
                    JsonValueKind.Object => property.Value.GetRawText(), // Nested objects as JSON string
                    JsonValueKind.Array => property.Value.GetRawText(), // Arrays as JSON string
                    _ => property.Value.GetRawText()
                };
                
                if (!string.IsNullOrEmpty(value))
                {
                    metadata[property.Name] = value;
                }
            }

            _logger.LogInformation("Extracted {Count} metadata fields", metadata.Count);
            return metadata;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing metadata from Ollama");
            return new Dictionary<string, string>();
        }
    }

    private string CleanJsonResponse(string response)
    {
        // Remove markdown code blocks if present
        var cleaned = response.Trim();
        
        if (cleaned.StartsWith("```json") && cleaned.Length >= 7)
        {
            cleaned = cleaned.Substring(7);
        }
        else if (cleaned.StartsWith("```") && cleaned.Length >= 3)
        {
            cleaned = cleaned.Substring(3);
        }
        
        if (cleaned.EndsWith("```") && cleaned.Length >= 3)
        {
            cleaned = cleaned.Substring(0, cleaned.Length - 3);
        }
        
        return cleaned.Trim();
    }
}
