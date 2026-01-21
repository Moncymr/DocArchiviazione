using Azure.AI.OpenAI;
using Azure;
using DocN.Data.Models;
using System.Text;
using OpenAI.Chat;
using System.ClientModel;

namespace DocN.Data.Services;

public interface ICategoryService
{
    Task<(string Category, string Reasoning)> SuggestCategoryAsync(string fileName, string extractedText);
}

public class CategoryService : ICategoryService
{
    private readonly ApplicationDbContext _context;
    private ChatClient? _client;

    public CategoryService(ApplicationDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        InitializeClient();
    }

    private void InitializeClient()
    {
        var config = _context.AIConfigurations.FirstOrDefault(c => c.IsActive);
        if (config != null && !string.IsNullOrEmpty(config.AzureOpenAIEndpoint) && !string.IsNullOrEmpty(config.AzureOpenAIKey))
        {
            var azureClient = new AzureOpenAIClient(new Uri(config.AzureOpenAIEndpoint), new AzureKeyCredential(config.AzureOpenAIKey));
            _client = azureClient.GetChatClient(config.ChatDeploymentName ?? "gpt-4");
        }
    }

    public async Task<(string Category, string Reasoning)> SuggestCategoryAsync(string fileName, string extractedText)
    {
        if (_client == null)
            return ("Uncategorized", "AI service not configured");

        try
        {
            // Get existing categories from database
            var existingCategories = await Task.Run(() => 
                _context.Documents
                    .Where(d => !string.IsNullOrEmpty(d.ActualCategory))
                    .Select(d => d.ActualCategory)
                    .Distinct()
                    .ToList());

            var categoriesHint = existingCategories.Any() 
                ? $"Existing categories in the system: {string.Join(", ", existingCategories)}. You can suggest one of these or propose a new category if none fit."
                : "This is a new system, suggest an appropriate category.";

            var prompt = $@"Analyze this document and suggest the best category for it. Also explain your reasoning.

File name: {fileName}
Content preview: {TruncateText(extractedText, 500)}

{categoriesHint}

Respond in JSON format:
{{
    ""category"": ""suggested category name"",
    ""reasoning"": ""detailed explanation of why this category fits, mentioning specific keywords, content type, or patterns you identified""
}}";

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage("You are a document classification expert. Analyze documents and suggest appropriate categories with clear reasoning."),
                new UserChatMessage(prompt)
            };

            var response = await _client.CompleteChatAsync(messages);
            var content = response.Value.Content[0].Text;

            // Parse JSON response
            var result = System.Text.Json.JsonSerializer.Deserialize<CategorySuggestion>(content);
            return (result?.Category ?? "Uncategorized", result?.Reasoning ?? "No reasoning provided");
        }
        catch (Exception ex)
        {
            return ("Uncategorized", $"Error: {ex.Message}");
        }
    }

    private string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;

        return text.Substring(0, maxLength) + "...";
    }

    private class CategorySuggestion
    {
        public string Category { get; set; } = string.Empty;
        public string Reasoning { get; set; } = string.Empty;
    }
}
