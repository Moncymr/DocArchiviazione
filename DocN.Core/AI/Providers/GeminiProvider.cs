using DocN.Core.AI.Configuration;
using DocN.Core.AI.Models;
using Mscc.GenerativeAI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace DocN.Core.AI.Providers;

/// <summary>
/// Provider per Google Gemini
/// </summary>
public class GeminiProvider : BaseAIProvider
{
    private readonly GeminiConfiguration _config;
    private readonly GoogleAI _client;

    public override AIProviderType ProviderType => AIProviderType.Gemini;
    public override string ProviderName => "Google Gemini";

    public GeminiProvider(
        IOptions<AIProviderConfiguration> configuration,
        ILogger<GeminiProvider> logger) : base(logger)
    {
        _config = configuration.Value.Gemini 
            ?? throw new InvalidOperationException("Gemini configuration not found");

        if (string.IsNullOrEmpty(_config.ApiKey))
        {
            throw new InvalidOperationException("Gemini ApiKey is required");
        }

        _client = new GoogleAI(_config.ApiKey);
    }

    public override async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Text cannot be null, empty, or whitespace", nameof(text));
        }

        _logger.LogInformation("Generating embedding with Gemini for text of length {Length}", text.Length);

        try
        {
            var model = _client.GenerativeModel(model: _config.EmbeddingModel);
            // Use TaskType.RetrievalDocument for better semantic retrieval quality
            var response = await model.EmbedContent(
                content: text,
                taskType: Mscc.GenerativeAI.TaskType.RetrievalDocument,
                cancellationToken: cancellationToken);
            
            if (response == null)
            {
                throw new InvalidOperationException("Failed to generate embedding with Gemini: Response was null");
            }
            
            if (response.Embedding?.Values == null)
            {
                throw new InvalidOperationException("Failed to generate embedding with Gemini: Embedding.Values was null");
            }
            
            var embedding = response.Embedding.Values.ToArray();
            _logger.LogInformation("Successfully generated embedding with {Dimensions} dimensions", embedding.Length);
            return embedding;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating embedding with Gemini. Model: {Model}, Text length: {Length}", 
                _config.EmbeddingModel, text.Length);
            throw;
        }
    }

    public override async Task<List<CategorySuggestion>> SuggestCategoriesAsync(
        string documentText, 
        List<string> availableCategories, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Suggesting categories with Gemini");

        var model = _client.GenerativeModel(model: _config.GenerationModel);
        var prompt = BuildCategorySuggestionPrompt(documentText, availableCategories);

        var response = await model.GenerateContent(prompt);
        
        if (response?.Text != null)
        {
            return ParseCategorySuggestions(response.Text);
        }

        return new List<CategorySuggestion>();
    }

    public override async Task<List<string>> ExtractTagsAsync(
        string documentText,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Extracting tags with Gemini");

        try
        {
            var model = _client.GenerativeModel(model: _config.GenerationModel);
            var prompt = BuildTagExtractionPrompt(documentText);

            var response = await model.GenerateContent(prompt);
            
            if (response?.Text != null)
            {
                return ParseTags(response.Text);
            }

            return new List<string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting tags with Gemini");
            return new List<string>();
        }
    }

    private List<CategorySuggestion> ParseCategorySuggestions(string jsonResponse)
    {
        try
        {
            // Gemini a volte include ```json markers, rimuoviamoli
            var cleanedResponse = jsonResponse.Trim();
            if (cleanedResponse.StartsWith("```json"))
            {
                cleanedResponse = cleanedResponse.Substring(7);
            }
            if (cleanedResponse.StartsWith("```"))
            {
                cleanedResponse = cleanedResponse.Substring(3);
            }
            if (cleanedResponse.EndsWith("```"))
            {
                cleanedResponse = cleanedResponse.Substring(0, cleanedResponse.Length - 3);
            }
            cleanedResponse = cleanedResponse.Trim();

            var jsonDoc = JsonDocument.Parse(cleanedResponse);
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
            _logger.LogError(ex, "Error parsing category suggestions from Gemini");
            return new List<CategorySuggestion>();
        }
    }

    private List<string> ParseTags(string jsonResponse)
    {
        try
        {
            // Gemini a volte include ```json markers, rimuoviamoli
            var cleanedResponse = jsonResponse.Trim();
            if (cleanedResponse.StartsWith("```json"))
            {
                cleanedResponse = cleanedResponse.Substring(7);
            }
            if (cleanedResponse.StartsWith("```"))
            {
                cleanedResponse = cleanedResponse.Substring(3);
            }
            if (cleanedResponse.EndsWith("```"))
            {
                cleanedResponse = cleanedResponse.Substring(0, cleanedResponse.Length - 3);
            }
            cleanedResponse = cleanedResponse.Trim();

            var jsonDoc = JsonDocument.Parse(cleanedResponse);
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
            _logger.LogError(ex, "Error parsing tags from Gemini");
            return new List<string>();
        }
    }

    public override async Task<Dictionary<string, string>> ExtractMetadataAsync(
        string documentText,
        string fileName = "",
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Extracting metadata with Gemini for file: {FileName}", fileName);

        try
        {
            var model = _client.GenerativeModel(model: _config.GenerationModel);
            var prompt = BuildMetadataExtractionPrompt(documentText, fileName);

            var response = await model.GenerateContent(prompt);
            
            if (response?.Text != null)
            {
                return ParseMetadata(response.Text);
            }

            return new Dictionary<string, string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting metadata with Gemini");
            return new Dictionary<string, string>();
        }
    }

    private Dictionary<string, string> ParseMetadata(string jsonResponse)
    {
        try
        {
            // Gemini a volte include ```json markers, rimuoviamoli
            var cleanedResponse = jsonResponse.Trim();
            if (cleanedResponse.StartsWith("```json"))
            {
                cleanedResponse = cleanedResponse.Substring(7);
            }
            if (cleanedResponse.StartsWith("```"))
            {
                cleanedResponse = cleanedResponse.Substring(3);
            }
            if (cleanedResponse.EndsWith("```"))
            {
                cleanedResponse = cleanedResponse.Substring(0, cleanedResponse.Length - 3);
            }
            cleanedResponse = cleanedResponse.Trim();

            var jsonDoc = JsonDocument.Parse(cleanedResponse);
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
            _logger.LogError(ex, "Error parsing metadata from Gemini");
            return new Dictionary<string, string>();
        }
    }
}
