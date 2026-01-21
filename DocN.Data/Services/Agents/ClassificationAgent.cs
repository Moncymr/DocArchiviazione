using Azure.AI.OpenAI;
using Azure;
using DocN.Data.Models;
using Microsoft.EntityFrameworkCore;
using OpenAI.Chat;
using System.ClientModel;
using System.Text.Json;

namespace DocN.Data.Services.Agents;

/// <summary>
/// Agent responsible for classifying documents and extracting metadata
/// </summary>
public class ClassificationAgent : IClassificationAgent
{
    private readonly ApplicationDbContext _context;
    private readonly IEmbeddingService _embeddingService;
    private ChatClient? _client;

    public string Name => "ClassificationAgent";
    public string Description => "Classifies documents, suggests categories, and extracts tags";

    public ClassificationAgent(ApplicationDbContext context, IEmbeddingService embeddingService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        InitializeClient();
    }

    private void InitializeClient()
    {
        try
        {
            var config = _context.AIConfigurations.FirstOrDefault(c => c.IsActive);
            if (config != null && !string.IsNullOrEmpty(config.AzureOpenAIEndpoint) && !string.IsNullOrEmpty(config.AzureOpenAIKey))
            {
                var azureClient = new AzureOpenAIClient(new Uri(config.AzureOpenAIEndpoint), new AzureKeyCredential(config.AzureOpenAIKey));
                _client = azureClient.GetChatClient(config.ChatDeploymentName ?? "gpt-4");
            }
        }
        catch
        {
            // Initialization can fail if database doesn't exist yet
        }
    }

    public async Task<CategorySuggestion> SuggestCategoryAsync(Document document)
    {
        if (_client == null)
        {
            InitializeClient();
            if (_client == null)
            {
                return new CategorySuggestion
                {
                    Category = "Uncategorized",
                    Confidence = 0,
                    Reasoning = "AI service not configured"
                };
            }
        }

        try
        {
            // Method 1: Direct AI classification
            var aiSuggestion = await GetAIClassification(document);

            // Method 2: Vector-based classification (find similar documents)
            var vectorSuggestion = await GetVectorBasedClassification(document);

            // Combine both methods
            if (aiSuggestion.Category == vectorSuggestion && aiSuggestion.Confidence > 0.7)
            {
                // Both methods agree and AI is confident
                return aiSuggestion;
            }
            else if (aiSuggestion.Confidence > 0.8)
            {
                // AI is very confident, trust it
                return aiSuggestion;
            }
            else
            {
                // Use AI but include vector-based as alternative
                aiSuggestion.AlternativeCategories.Add(vectorSuggestion);
                return aiSuggestion;
            }
        }
        catch (Exception ex)
        {
            return new CategorySuggestion
            {
                Category = "Uncategorized",
                Confidence = 0,
                Reasoning = $"Error: {ex.Message}"
            };
        }
    }

    private async Task<CategorySuggestion> GetAIClassification(Document document)
    {
        // Get common categories from database
        var commonCategories = await _context.Documents
            .Where(d => !string.IsNullOrEmpty(d.ActualCategory))
            .GroupBy(d => d.ActualCategory)
            .OrderByDescending(g => g.Count())
            .Take(10)
            .Select(g => g.Key)
            .ToListAsync();

        var categoriesList = commonCategories.Any() 
            ? string.Join(", ", commonCategories) 
            : "Invoice, Contract, Report, Policy, Manual, Email, Memo, Presentation, Spreadsheet, Form";

        var text = document.ExtractedText.Length > 2000 
            ? document.ExtractedText.Substring(0, 2000) 
            : document.ExtractedText;

        var prompt = $@"Analyze this document and suggest the most appropriate category.

Document: {document.FileName}
Content: {text}

Available categories: {categoriesList}

Respond with a JSON object in this exact format:
{{
  ""category"": ""suggested category name"",
  ""confidence"": 0.85,
  ""reasoning"": ""brief explanation"",
  ""alternatives"": [""alternative1"", ""alternative2""]
}}";

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage("You are a document classification expert. Always respond with valid JSON only."),
            new UserChatMessage(prompt)
        };

        var response = await _client!.CompleteChatAsync(messages);
        var jsonResponse = response.Value.Content[0].Text;

        // Parse JSON response
        try
        {
            var result = JsonSerializer.Deserialize<JsonElement>(jsonResponse);
            return new CategorySuggestion
            {
                Category = result.GetProperty("category").GetString() ?? "Uncategorized",
                Confidence = result.GetProperty("confidence").GetDouble(),
                Reasoning = result.GetProperty("reasoning").GetString() ?? "",
                AlternativeCategories = result.TryGetProperty("alternatives", out var alts)
                    ? alts.EnumerateArray().Select(a => a.GetString() ?? "").ToList()
                    : new List<string>()
            };
        }
        catch
        {
            // Fallback if JSON parsing fails
            return new CategorySuggestion
            {
                Category = "Uncategorized",
                Confidence = 0.5,
                Reasoning = "Failed to parse AI response"
            };
        }
    }

    private async Task<string> GetVectorBasedClassification(Document document)
    {
        if (document.EmbeddingVector == null)
            return "Uncategorized";

        try
        {
            // Find similar documents
            var similarDocuments = await _embeddingService.SearchSimilarDocumentsAsync(document.EmbeddingVector, topK: 5);

            // Get the most common category among similar documents
            var categoryGroups = similarDocuments
                .Where(d => !string.IsNullOrEmpty(d.ActualCategory))
                .GroupBy(d => d.ActualCategory)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();

            return categoryGroups?.Key ?? "Uncategorized";
        }
        catch
        {
            return "Uncategorized";
        }
    }

    public async Task<List<string>> ExtractTagsAsync(Document document)
    {
        if (_client == null)
        {
            InitializeClient();
            if (_client == null)
                return new List<string>();
        }

        try
        {
            var text = document.ExtractedText.Length > 2000 
                ? document.ExtractedText.Substring(0, 2000) 
                : document.ExtractedText;

            var prompt = $@"Extract 5-10 relevant tags/keywords from this document.

Document: {document.FileName}
Content: {text}

Return ONLY a JSON array of tags: [""tag1"", ""tag2"", ...]";

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage("You are a tagging expert. Always respond with valid JSON array only."),
                new UserChatMessage(prompt)
            };

            var response = await _client.CompleteChatAsync(messages);
            var jsonResponse = response.Value.Content[0].Text;

            var tags = JsonSerializer.Deserialize<List<string>>(jsonResponse);
            return tags ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    public async Task<string> ClassifyDocumentTypeAsync(Document document)
    {
        if (_client == null)
        {
            InitializeClient();
            if (_client == null)
                return "Unknown";
        }

        try
        {
            var text = document.ExtractedText.Length > 1000 
                ? document.ExtractedText.Substring(0, 1000) 
                : document.ExtractedText;

            var prompt = $@"Classify the type of this document.

Document: {document.FileName}
Content: {text}

Choose from: Invoice, Contract, Report, Email, Memo, Letter, Form, Policy, Manual, Presentation, Spreadsheet, Other

Respond with ONLY the document type, nothing else.";

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage("You are a document type classifier."),
                new UserChatMessage(prompt)
            };

            var response = await _client.CompleteChatAsync(messages);
            return response.Value.Content[0].Text.Trim();
        }
        catch
        {
            return "Unknown";
        }
    }
}
