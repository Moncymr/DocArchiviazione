using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.Logging;
using DocN.Core.Interfaces;

#pragma warning disable SKEXP0110 // Agents are experimental
#pragma warning disable SKEXP0001 // Experimental features

namespace DocN.Data.Services.Agents;

/// <summary>
/// Advanced multi-agent collaboration service using Microsoft Agent Framework
/// Implements ChatCompletionAgent and AgentGroupChat for complex RAG scenarios
/// </summary>
public class MultiAgentCollaborationService
{
    private readonly IKernelProvider _kernelProvider;
    private readonly ILogger<MultiAgentCollaborationService> _logger;
    private readonly IEmbeddingService _embeddingService;
    private readonly ISemanticRAGService _ragService;

    public MultiAgentCollaborationService(
        IKernelProvider kernelProvider,
        ILogger<MultiAgentCollaborationService> logger,
        IEmbeddingService embeddingService,
        ISemanticRAGService ragService)
    {
        _kernelProvider = kernelProvider ?? throw new ArgumentNullException(nameof(kernelProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _ragService = ragService ?? throw new ArgumentNullException(nameof(ragService));
    }

    /// <summary>
    /// Process a complex query using multi-agent collaboration
    /// Agents work together to: analyze query, retrieve documents, synthesize answer, and validate
    /// </summary>
    public async Task<MultiAgentResponse> ProcessComplexQueryAsync(
        string query,
        string userId,
        AgentCollaborationConfig? config = null)
    {
        config ??= new AgentCollaborationConfig();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        _logger.LogInformation("Starting multi-agent collaboration for query: {Query}", query);

        try
        {
            var kernel = await _kernelProvider.GetKernelAsync();

            // Create specialized agents
            var queryAnalyzerAgent = CreateQueryAnalyzerAgent(kernel);
            var retrievalAgent = CreateRetrievalAgent(kernel);
            var synthesisAgent = CreateSynthesisAgent(kernel);
            var validationAgent = CreateValidationAgent(kernel);

            // Create agent group chat for collaboration
            var chat = new AgentGroupChat(
                queryAnalyzerAgent,
                retrievalAgent,
                synthesisAgent,
                validationAgent)
            {
                ExecutionSettings = new AgentGroupChatSettings
                {
                    TerminationStrategy = new ApprovalTerminationStrategy
                    {
                        MaximumIterations = config.MaxIterations,
                        AutomaticApproval = true
                    }
                }
            };

            // Add initial user message
            chat.AddChatMessage(new ChatMessageContent(AuthorRole.User, query));

            // Execute agent collaboration
            var messages = new List<AgentMessage>();
            var finalAnswer = "";

            await foreach (var message in chat.InvokeAsync())
            {
                var contentPreview = message.Content != null && message.Content.Length > 100 
                    ? message.Content.Substring(0, 100) 
                    : message.Content ?? "";
                    
                _logger.LogInformation("Agent {Agent}: {Content}", 
                    message.AuthorName, 
                    contentPreview);

                messages.Add(new AgentMessage
                {
                    AgentName = message.AuthorName ?? "Unknown",
                    Content = message.Content ?? "",
                    Timestamp = DateTime.UtcNow
                });

                // The last message from synthesis agent is our final answer
                if (message.AuthorName == "SynthesisAgent" && !string.IsNullOrWhiteSpace(message.Content))
                {
                    finalAnswer = message.Content;
                }
            }

            stopwatch.Stop();

            return new MultiAgentResponse
            {
                Answer = finalAnswer,
                AgentMessages = messages,
                TotalTimeMs = stopwatch.ElapsedMilliseconds,
                Success = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in multi-agent collaboration");
            stopwatch.Stop();

            return new MultiAgentResponse
            {
                Answer = "An error occurred during multi-agent processing.",
                Success = false,
                ErrorMessage = ex.Message,
                TotalTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    /// <summary>
    /// Create a ChatCompletionAgent specialized in query analysis
    /// </summary>
    private ChatCompletionAgent CreateQueryAnalyzerAgent(Kernel kernel)
    {
        return new ChatCompletionAgent
        {
            Name = "QueryAnalyzerAgent",
            Instructions = @"You are a query analysis expert. Your role is to:
1. Analyze the user's question to understand intent
2. Identify key entities, concepts, and search terms
3. Suggest query expansions or reformulations if needed
4. Determine the best retrieval strategy
5. Pass your analysis to the RetrievalAgent

Be concise but thorough. Output your analysis in a structured format.",
            Kernel = kernel
        };
    }

    /// <summary>
    /// Create a ChatCompletionAgent specialized in document retrieval
    /// </summary>
    private ChatCompletionAgent CreateRetrievalAgent(Kernel kernel)
    {
        return new ChatCompletionAgent
        {
            Name = "RetrievalAgent",
            Instructions = @"You are a document retrieval specialist. Your role is to:
1. Use the query analysis from QueryAnalyzerAgent
2. Retrieve the most relevant documents/chunks
3. Rank and filter results by relevance
4. Provide top-ranked documents to SynthesisAgent
5. Include relevance scores and metadata

Focus on precision and relevance. Pass only the best documents forward.",
            Kernel = kernel
        };
    }

    /// <summary>
    /// Create a ChatCompletionAgent specialized in answer synthesis
    /// </summary>
    private ChatCompletionAgent CreateSynthesisAgent(Kernel kernel)
    {
        return new ChatCompletionAgent
        {
            Name = "SynthesisAgent",
            Instructions = @"You are an answer synthesis expert. Your role is to:
1. Review documents from RetrievalAgent
2. Generate a comprehensive answer based ONLY on the provided documents
3. Include citations to source documents
4. Ensure accuracy and relevance
5. Send your answer to ValidationAgent for review

Be accurate, cite sources, and stay grounded in the documents provided.",
            Kernel = kernel
        };
    }

    /// <summary>
    /// Create a ChatCompletionAgent specialized in answer validation
    /// </summary>
    private ChatCompletionAgent CreateValidationAgent(Kernel kernel)
    {
        return new ChatCompletionAgent
        {
            Name = "ValidationAgent",
            Instructions = @"You are an answer validation expert. Your role is to:
1. Review the answer from SynthesisAgent
2. Verify all claims are supported by source documents
3. Check for factual accuracy and consistency
4. Identify any unsupported or speculative statements
5. Either APPROVE the answer or request revision

Be critical but fair. Ensure quality control.",
            Kernel = kernel
        };
    }
}

/// <summary>
/// Configuration for agent collaboration
/// </summary>
public class AgentCollaborationConfig
{
    public int MaxIterations { get; set; } = 10;
    public bool EnableValidation { get; set; } = true;
    public double ConfidenceThreshold { get; set; } = 0.7;
}

/// <summary>
/// Response from multi-agent collaboration
/// </summary>
public class MultiAgentResponse
{
    public string Answer { get; set; } = string.Empty;
    public List<AgentMessage> AgentMessages { get; set; } = new();
    public long TotalTimeMs { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Message from an agent in the collaboration
/// </summary>
public class AgentMessage
{
    public string AgentName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Custom termination strategy for agent collaboration
/// </summary>
public class ApprovalTerminationStrategy : TerminationStrategy
{
    public new int MaximumIterations { get; set; } = 10;
    public bool AutomaticApproval { get; set; } = false;

    protected override Task<bool> ShouldAgentTerminateAsync(Agent agent, IReadOnlyList<ChatMessageContent> history, CancellationToken cancellationToken = default)
    {
        // Terminate if we've reached max iterations
        if (history.Count >= MaximumIterations * 4) // 4 agents
        {
            return Task.FromResult(true);
        }

        // Terminate if ValidationAgent approved
        if (AutomaticApproval && 
            history.LastOrDefault()?.AuthorName == "ValidationAgent" &&
            history.LastOrDefault()?.Content?.Contains("APPROVE", StringComparison.OrdinalIgnoreCase) == true)
        {
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }
}

#pragma warning restore SKEXP0110
#pragma warning restore SKEXP0001
