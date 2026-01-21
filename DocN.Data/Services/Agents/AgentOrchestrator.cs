using DocN.Data.Models;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace DocN.Data.Services.Agents;

/// <summary>
/// Orchestratore multi-agente per gestire workflow complessi di recupero, sintesi e classificazione.
/// </summary>
/// <remarks>
/// Coordina tre agenti specializzati:
/// - RetrievalAgent: recupero documenti/chunk rilevanti
/// - SynthesisAgent: generazione risposte da contenuti recuperati
/// - ClassificationAgent: classificazione e tagging documenti
/// </remarks>
public class AgentOrchestrator : IAgentOrchestrator
{
    private readonly IRetrievalAgent _retrievalAgent;
    private readonly ISynthesisAgent _synthesisAgent;
    private readonly IClassificationAgent _classificationAgent;
    private readonly ApplicationDbContext _context;

    /// <summary>
    /// Costruttore con dependency injection degli agenti e del contesto database.
    /// </summary>
    /// <param name="retrievalAgent">Agente per recupero documenti e chunk</param>
    /// <param name="synthesisAgent">Agente per sintesi risposte</param>
    /// <param name="classificationAgent">Agente per classificazione documenti</param>
    /// <param name="context">Contesto database EF Core</param>
    /// <exception cref="ArgumentNullException">Se qualche dipendenza è null</exception>
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
    /// Processa una query utente tramite workflow multi-agente orchestrato.
    /// </summary>
    /// <param name="query">Query utente in linguaggio naturale</param>
    /// <param name="userId">ID utente opzionale per filtrare documenti accessibili</param>
    /// <param name="conversationId">ID conversazione opzionale per contesto storico</param>
    /// <returns>Risultato contenente risposta, documenti/chunk recuperati e metriche timing</returns>
    /// <remarks>
    /// Workflow:
    /// 1. Carica storico conversazione se presente (per contesto)
    /// 2. RetrievalAgent: cerca chunk rilevanti (fallback a documenti interi)
    /// 3. SynthesisAgent: genera risposta da contenuti recuperati
    /// Metriche: traccia tempi di retrieval, synthesis e totale per monitoring.
    /// OTTIMIZZAZIONE: AsNoTracking su query read-only storico messaggi.
    /// </remarks>
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
            // OTTIMIZZAZIONE: AsNoTracking per query read-only
            List<Message>? conversationHistory = null;
            if (conversationId.HasValue)
            {
                conversationHistory = await _context.Messages
                    .AsNoTracking()
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
                // OTTIMIZZAZIONE: AsNoTracking per query read-only
                var docIds = chunks.Select(c => c.DocumentId).Distinct().ToList();
                result.RetrievedDocuments = await _context.Documents
                    .AsNoTracking()
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
    /// Classifica un documento usando l'agente di classificazione AI.
    /// </summary>
    /// <param name="document">Documento da classificare</param>
    /// <returns>Risultato contenente categoria suggerita, tag estratti e tipo documento</returns>
    /// <remarks>
    /// Esegue tre task di classificazione in parallelo per efficienza:
    /// 1. Suggerimento categoria (es. "Fatture", "Contratti")
    /// 2. Estrazione tag automatici dal contenuto
    /// 3. Classificazione tipo documento (PDF, Word, etc.)
    /// OTTIMIZZAZIONE: Task.WhenAll per esecuzione parallela e riduzione latency totale.
    /// </remarks>
    public async Task<DocumentClassificationResult> ClassifyDocumentAsync(Document document)
    {
        var result = new DocumentClassificationResult();

        try
        {
            // OTTIMIZZAZIONE: Esecuzione parallela dei tre task di classificazione
            // Riduce latency totale da somma a max(task1, task2, task3)
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
