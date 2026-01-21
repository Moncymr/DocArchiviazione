using DocN.Core.AI.Configuration;
using DocN.Core.AI.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using System.Text.Json;

namespace DocN.Core.AI.Providers;

/// <summary>
/// Provider per Groq (API cloud veloce compatibile con OpenAI)
/// </summary>
public class GroqProvider : BaseAIProvider
{
    private readonly GroqConfiguration _config;
    private readonly OpenAIClient _client;

    public override AIProviderType ProviderType => AIProviderType.Groq;
    public override string ProviderName => "Groq";

    public GroqProvider(
        IOptions<AIProviderConfiguration> configuration,
        ILogger<GroqProvider> logger) : base(logger)
    {
        _config = configuration.Value.Groq 
            ?? throw new InvalidOperationException("Groq configuration not found");

        if (string.IsNullOrEmpty(_config.ApiKey))
        {
            throw new InvalidOperationException("Groq ApiKey is required");
        }

        // Groq usa un'API compatibile con OpenAI
        var options = new OpenAIClientOptions();
        options.Endpoint = new Uri(_config.Endpoint);

        _client = new OpenAIClient(new ApiKeyCredential(_config.ApiKey), options);
    }

    public override async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Groq does not support embeddings natively. Consider using a different provider for embeddings.");
        
        // Groq non supporta embeddings, quindi generiamo un embedding fittizio
        // In produzione, dovresti usare un altro provider per gli embeddings
        throw new NotSupportedException("Groq does not support embeddings. Please use OpenAI, Azure OpenAI, Gemini, or Ollama for embeddings.");
    }

    public override async Task<List<CategorySuggestion>> SuggestCategoriesAsync(
        string documentText, 
        List<string> availableCategories, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Suggesting categories with Groq");

        var chatClient = _client.GetChatClient(_config.ChatModel);
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
        _logger.LogInformation("Extracting tags with Groq");

        try
        {
            var chatClient = _client.GetChatClient(_config.ChatModel);
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
            _logger.LogError(ex, "Error extracting tags with Groq");
            return new List<string>();
        }
    }

    public override async Task<Dictionary<string, string>> ExtractMetadataAsync(
        string documentText,
        string fileName = "",
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Extracting metadata with Groq for file: {FileName}", fileName);

        try
        {
            var chatClient = _client.GetChatClient(_config.ChatModel);
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
            _logger.LogError(ex, "Error extracting metadata with Groq");
            return new Dictionary<string, string>();
        }
    }

    private List<CategorySuggestion> ParseCategorySuggestions(string jsonResponse)
    {
        try
        {
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
            _logger.LogError(ex, "Error parsing category suggestions from Groq");
            return new List<CategorySuggestion>();
        }
    }

    private List<string> ParseTags(string jsonResponse)
    {
        try
        {
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
            _logger.LogError(ex, "Error parsing tags from Groq");
            return new List<string>();
        }
    }

    private Dictionary<string, string> ParseMetadata(string jsonResponse)
    {
        try
        {
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
            _logger.LogError(ex, "Error parsing metadata from Groq");
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
