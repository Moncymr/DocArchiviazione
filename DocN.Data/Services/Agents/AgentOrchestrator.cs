using DocN.Data.Models;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace DocN.Data.Services.Agents;

/// <summary>
/// Orchestrates multiple agents to handle complex workflows
/// </summary>
public class AgentOrchestrator : IAgentOrchestrator
{
    private readonly IRetrievalAgent _retrievalAgent;
    private readonly ISynthesisAgent _synthesisAgent;
    private readonly IClassificationAgent _classificationAgent;
    private readonly ApplicationDbContext _context;

    public AgentOrchestrator(
        IRetrievalAgent retrievalAgent,
        ISynthesisAgent synthesisAgent,
        IClassificationAgent classificationAgent,
        ApplicationDbContext context)
    {
        _retrievalAgent = retrievalAgent ?? throw new ArgumentNullException(nameof(retrievalAgent));
        _synthesisAgent = synthesisAgent ?? throw new ArgumentNullException(nameof(synthesisAgent));
        _classificationAgent = classificationAgent ?? throw new ArgumentNullException(nameof(classificationAgent));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Process a query using multi-agent workflow:
    /// 1. RetrievalAgent finds relevant documents/chunks
    /// 2. SynthesisAgent generates answer from retrieved content
    /// </summary>
    public async Task<AgentOrchestrationResult> ProcessQueryAsync(
        string query,
        string? userId = null,
        int? conversationId = null)
    {
        var totalStopwatch = Stopwatch.StartNew();
        var result = new AgentOrchestrationResult();

        try
        {
            // Load conversation history if provided
            List<Message>? conversationHistory = null;
            if (conversationId.HasValue)
            {
                conversationHistory = await _context.Messages
                    .Where(m => m.ConversationId == conversationId.Value)
                    .OrderBy(m => m.Timestamp)
                    .ToListAsync();
            }

            // Step 1: Retrieval - get relevant documents
            var retrievalStopwatch = Stopwatch.StartNew();
            
            // Try chunk-based retrieval first (more precise)
            var chunks = await _retrievalAgent.RetrieveChunksAsync(query, userId, topK: 10);
            
            if (chunks.Any())
            {
                result.RetrievedChunks = chunks;
                result.RetrievalStrategy = "chunk-based";
                
                // Also get the parent documents for context
                var docIds = chunks.Select(c => c.DocumentId).Distinct().ToList();
                result.RetrievedDocuments = await _context.Documents
                    .Where(d => docIds.Contains(d.Id))
                    .ToListAsync();
            }
            else
            {
                // Fallback to document-level retrieval
                var documents = await _retrievalAgent.RetrieveAsync(query, userId, topK: 5);
                result.RetrievedDocuments = documents;
                result.RetrievalStrategy = "document-based";
            }
            
            retrievalStopwatch.Stop();
            result.RetrievalTime = retrievalStopwatch.Elapsed;

            // Step 2: Synthesis - generate answer
            var synthesisStopwatch = Stopwatch.StartNew();
            
            if (result.RetrievedChunks.Any())
            {
                // Use chunk-based synthesis for more precise answers
                result.Answer = await _synthesisAgent.SynthesizeFromChunksAsync(
                    query,
                    result.RetrievedChunks,
                    conversationHistory);
            }
            else if (result.RetrievedDocuments.Any())
            {
                // Use document-based synthesis
                result.Answer = await _synthesisAgent.SynthesizeAsync(
                    query,
                    result.RetrievedDocuments,
                    conversationHistory);
            }
            else
            {
                result.Answer = "I couldn't find any relevant documents to answer your question.";
            }
            
            synthesisStopwatch.Stop();
            result.SynthesisTime = synthesisStopwatch.Elapsed;
        }
        catch (Exception ex)
        {
            result.Answer = $"Error processing query: {ex.Message}";
        }

        totalStopwatch.Stop();
        result.TotalTime = totalStopwatch.Elapsed;

        return result;
    }

    /// <summary>
    /// Classify a document using the classification agent
    /// </summary>
    public async Task<DocumentClassificationResult> ClassifyDocumentAsync(Document document)
    {
        var result = new DocumentClassificationResult();

        try
        {
            // Run classification tasks in parallel for efficiency
            var categoryTask = _classificationAgent.SuggestCategoryAsync(document);
            var tagsTask = _classificationAgent.ExtractTagsAsync(document);
            var typeTask = _classificationAgent.ClassifyDocumentTypeAsync(document);

            await Task.WhenAll(categoryTask, tagsTask, typeTask);

            result.CategorySuggestion = await categoryTask;
            result.Tags = await tagsTask;
            result.DocumentType = await typeTask;
        }
        catch (Exception ex)
        {
            result.CategorySuggestion = new CategorySuggestion
            {
                Category = "Uncategorized",
                Confidence = 0,
                Reasoning = $"Error: {ex.Message}"
            };
        }

        return result;
    }
}
