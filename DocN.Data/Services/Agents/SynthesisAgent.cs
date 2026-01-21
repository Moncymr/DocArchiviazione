using Azure.AI.OpenAI;
using Azure;
using DocN.Data.Models;
using Microsoft.EntityFrameworkCore;
using OpenAI.Chat;
using System.ClientModel;
using System.Text;

namespace DocN.Data.Services.Agents;

/// <summary>
/// Agent responsible for synthesizing answers from retrieved documents
/// </summary>
public class SynthesisAgent : ISynthesisAgent
{
    private readonly ApplicationDbContext _context;
    private ChatClient? _client;

    public string Name => "SynthesisAgent";
    public string Description => "Synthesizes natural language answers from retrieved documents";

    public SynthesisAgent(ApplicationDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
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

    public async Task<string> SynthesizeAsync(string query, List<Document> documents, List<Message>? conversationHistory = null)
    {
        if (_client == null)
        {
            InitializeClient();
            if (_client == null)
                return "AI service not configured.";
        }

        try
        {
            var config = _context.AIConfigurations.FirstOrDefault(c => c.IsActive);
            var systemPrompt = config?.SystemPrompt ?? 
                "You are a helpful assistant that answers questions based on provided documents. " +
                "Always cite the source documents in your answer.";

            // Build context from documents
            var contextBuilder = new StringBuilder();
            contextBuilder.AppendLine("Use the following documents to answer the question:");
            contextBuilder.AppendLine();

            for (int i = 0; i < documents.Count; i++)
            {
                var doc = documents[i];
                contextBuilder.AppendLine($"Document {i + 1}: {doc.FileName}");
                contextBuilder.AppendLine($"Category: {doc.ActualCategory ?? doc.SuggestedCategory ?? "Unknown"}");
                
                // Truncate very long texts
                var text = doc.ExtractedText.Length > 3000 
                    ? doc.ExtractedText.Substring(0, 3000) + "..." 
                    : doc.ExtractedText;
                
                contextBuilder.AppendLine($"Content: {text}");
                contextBuilder.AppendLine();
            }

            contextBuilder.AppendLine($"Question: {query}");
            contextBuilder.AppendLine();
            contextBuilder.AppendLine("Provide a comprehensive answer based on the documents above. Include document references in your answer.");

            // Build message list
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt)
            };

            // Add conversation history if provided
            if (conversationHistory != null && conversationHistory.Any())
            {
                foreach (var msg in conversationHistory.TakeLast(5))
                {
                    if (msg.Role == "user")
                        messages.Add(new UserChatMessage(msg.Content));
                    else if (msg.Role == "assistant")
                        messages.Add(new AssistantChatMessage(msg.Content));
                }
            }

            // Add current context and query
            messages.Add(new UserChatMessage(contextBuilder.ToString()));

            // Generate response
            var response = await _client.CompleteChatAsync(messages);
            return response.Value.Content[0].Text;
        }
        catch (Exception ex)
        {
            return $"Error generating response: {ex.Message}";
        }
    }

    public async Task<string> SynthesizeFromChunksAsync(string query, List<DocumentChunk> chunks, List<Message>? conversationHistory = null)
    {
        if (_client == null)
        {
            InitializeClient();
            if (_client == null)
                return "AI service not configured.";
        }

        try
        {
            var config = _context.AIConfigurations.FirstOrDefault(c => c.IsActive);
            var systemPrompt = config?.SystemPrompt ?? 
                "You are a helpful assistant that answers questions based on provided document excerpts. " +
                "Always cite the source documents in your answer.";

            // Build context from chunks
            var contextBuilder = new StringBuilder();
            contextBuilder.AppendLine("Use the following document excerpts to answer the question:");
            contextBuilder.AppendLine();

            // Group chunks by document
            var groupedChunks = chunks.GroupBy(c => c.DocumentId).ToList();

            for (int i = 0; i < groupedChunks.Count; i++)
            {
                var docChunks = groupedChunks[i].OrderBy(c => c.ChunkIndex).ToList();
                var firstChunk = docChunks.First();
                
                // Use already loaded Document navigation property to avoid N+1 query
                var docName = firstChunk.Document?.FileName ?? $"Document {firstChunk.DocumentId}";
                
                contextBuilder.AppendLine($"Source {i + 1}: {docName}");
                
                foreach (var chunk in docChunks)
                {
                    contextBuilder.AppendLine($"  Excerpt {chunk.ChunkIndex + 1}: {chunk.ChunkText}");
                }
                
                contextBuilder.AppendLine();
            }

            contextBuilder.AppendLine($"Question: {query}");
            contextBuilder.AppendLine();
            contextBuilder.AppendLine("Provide a comprehensive answer based on the excerpts above. Include source references in your answer.");

            // Build message list
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt)
            };

            // Add conversation history if provided
            if (conversationHistory != null && conversationHistory.Any())
            {
                foreach (var msg in conversationHistory.TakeLast(5))
                {
                    if (msg.Role == "user")
                        messages.Add(new UserChatMessage(msg.Content));
                    else if (msg.Role == "assistant")
                        messages.Add(new AssistantChatMessage(msg.Content));
                }
            }

            // Add current context and query
            messages.Add(new UserChatMessage(contextBuilder.ToString()));

            // Generate response
            var response = await _client.CompleteChatAsync(messages);
            return response.Value.Content[0].Text;
        }
        catch (Exception ex)
        {
            return $"Error generating response: {ex.Message}";
        }
    }
}
