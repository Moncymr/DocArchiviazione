using Azure;
using Azure.AI.OpenAI;
using DocN.Core.AI.Configuration;
using DocN.Core.AI.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using OpenAI.Embeddings;
using System.Text.Json;

namespace DocN.Core.AI.Providers;

/// <summary>
/// Provider per Azure OpenAI
/// </summary>
public class AzureOpenAIProvider : BaseAIProvider
{
    private readonly AzureOpenAIConfiguration _config;
    private readonly AzureOpenAIClient _client;

    public override AIProviderType ProviderType => AIProviderType.AzureOpenAI;
    public override string ProviderName => "Azure OpenAI";

    public AzureOpenAIProvider(
        IOptions<AIProviderConfiguration> configuration,
        ILogger<AzureOpenAIProvider> logger) : base(logger)
    {
        _config = configuration.Value.AzureOpenAI 
            ?? throw new InvalidOperationException("Azure OpenAI configuration not found");

        if (string.IsNullOrEmpty(_config.Endpoint) || string.IsNullOrEmpty(_config.ApiKey))
        {
            throw new InvalidOperationException("Azure OpenAI Endpoint and ApiKey are required");
        }

        _client = new AzureOpenAIClient(
            new Uri(_config.Endpoint),
            new AzureKeyCredential(_config.ApiKey));
    }

    public override async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating embedding with Azure OpenAI");

        var embeddingClient = _client.GetEmbeddingClient(_config.EmbeddingDeployment);
        var response = await embeddingClient.GenerateEmbeddingAsync(text, cancellationToken: cancellationToken);
        
        return response.Value.ToFloats().ToArray();
    }

    public override async Task<List<CategorySuggestion>> SuggestCategoriesAsync(
        string documentText, 
        List<string> availableCategories, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Suggesting categories with Azure OpenAI");

        var chatClient = _client.GetChatClient(_config.ChatDeployment);
        var prompt = BuildCategorySuggestionPrompt(documentText, availableCategories);

        var chatMessages = new List<ChatMessage>
        {
            new SystemChatMessage("Sei un assistente che analizza documenti e suggerisce categorie appropriate."),
            new UserChatMessage(prompt)
        };

        var response = await chatClient.CompleteChatAsync(chatMessages, cancellationToken: cancellationToken);
        var content = response.Value.Content[0].Text;

        return ParseCategorySuggestions(content);
    }

    public override async Task<List<string>> ExtractTagsAsync(
        string documentText,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Extracting tags with Azure OpenAI");

        try
        {
            var chatClient = _client.GetChatClient(_config.ChatDeployment);
            var prompt = BuildTagExtractionPrompt(documentText);

            var chatMessages = new List<ChatMessage>
            {
                new SystemChatMessage("Sei un assistente che estrae tag rilevanti dai documenti."),
                new UserChatMessage(prompt)
            };

            var response = await chatClient.CompleteChatAsync(chatMessages, cancellationToken: cancellationToken);
            var content = response.Value.Content[0].Text;

            return ParseTags(content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting tags with Azure OpenAI");
            return new List<string>();
        }
    }

    private List<CategorySuggestion> ParseCategorySuggestions(string jsonResponse)
    {
        try
        {
            var jsonDoc = JsonDocument.Parse(jsonResponse);
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
            _logger.LogError(ex, "Error parsing category suggestions");
            return new List<CategorySuggestion>();
        }
    }

    private List<string> ParseTags(string jsonResponse)
    {
        try
        {
            var jsonDoc = JsonDocument.Parse(jsonResponse);
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
            _logger.LogError(ex, "Error parsing tags");
            return new List<string>();
        }
    }

    public override async Task<Dictionary<string, string>> ExtractMetadataAsync(
        string documentText,
        string fileName = "",
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Extracting metadata with Azure OpenAI for file: {FileName}", fileName);

        try
        {
            var chatClient = _client.GetChatClient(_config.ChatDeployment);
            var prompt = BuildMetadataExtractionPrompt(documentText, fileName);

            var chatMessages = new List<ChatMessage>
            {
                new SystemChatMessage("Sei un assistente esperto nell'estrazione di metadati strutturati da documenti."),
                new UserChatMessage(prompt)
            };

            var response = await chatClient.CompleteChatAsync(chatMessages, cancellationToken: cancellationToken);
            var content = response.Value.Content[0].Text;

            return ParseMetadata(content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting metadata with Azure OpenAI");
            return new Dictionary<string, string>();
        }
    }

    private Dictionary<string, string> ParseMetadata(string jsonResponse)
    {
        try
        {
            var jsonDoc = JsonDocument.Parse(jsonResponse);
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
            _logger.LogError(ex, "Error parsing metadata");
            return new Dictionary<string, string>();
        }
    }
}
