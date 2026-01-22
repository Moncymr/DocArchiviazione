using DocN.Core.Interfaces;
using DocN.Data.Models;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text;

namespace DocN.Data.Services;

/// <summary>
/// Service for fine-tuning and adapting embedding models to domain-specific documents
/// </summary>
/// <remarks>
/// Provides functionality for:
/// 1. Preparing training data from domain documents
/// 2. Generating contrastive pairs for similarity learning
/// 3. Evaluating embedding model quality
/// 4. Creating domain-specific adapters
/// 
/// This service helps improve embedding quality for specialized domains by:
/// - Learning domain-specific terminology and patterns
/// - Adapting similarity metrics to domain relevance
/// - Creating lightweight adapters that enhance base models
/// </remarks>
public class EmbeddingFineTuningService : IEmbeddingFineTuningService
{
    private readonly ILogger<EmbeddingFineTuningService> _logger;
    private readonly ApplicationDbContext _context;
    private readonly IEmbeddingService _embeddingService;
    private readonly IChunkingService _chunkingService;

    public EmbeddingFineTuningService(
        ILogger<EmbeddingFineTuningService> logger,
        ApplicationDbContext context,
        IEmbeddingService embeddingService,
        IChunkingService chunkingService)
    {
        _logger = logger;
        _context = context;
        _embeddingService = embeddingService;
        _chunkingService = chunkingService;
    }

    /// <summary>
    /// Prepare training data from domain documents
    /// Creates query-document pairs for fine-tuning
    /// </summary>
    public async Task<TrainingDataResult> PrepareTrainingDataAsync(
        List<int> documentIds,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        var result = new TrainingDataResult
        {
            OutputPath = outputPath,
            Format = "jsonl"
        };

        try
        {
            _logger.LogInformation("Preparing training data from {DocCount} documents", documentIds.Count);

            var documents = await _context.Documents
                .Where(d => documentIds.Contains(d.Id))
                .Include(d => d.Tags)
                .ToListAsync(cancellationToken);

            if (documents.Count == 0)
            {
                throw new InvalidOperationException("No documents found with the specified IDs");
            }

            var trainingExamples = new List<TrainingExample>();

            foreach (var doc in documents)
            {
                // Generate synthetic queries from document content
                var queries = GenerateSyntheticQueries(doc);
                
                // Chunk document for granular training
                var chunks = _chunkingService.ChunkText(doc.ExtractedText, 500, 50);
                
                foreach (var query in queries)
                {
                    // Find most relevant chunk for this query
                    var relevantChunk = FindMostRelevantChunk(query, chunks);
                    
                    if (relevantChunk != null)
                    {
                        trainingExamples.Add(new TrainingExample
                        {
                            Query = query,
                            PositiveDocument = relevantChunk,
                            DocumentId = doc.Id,
                            Category = doc.ActualCategory ?? "general"
                        });
                    }
                }
            }

            // Write to JSONL file
            await WriteTrainingDataAsync(trainingExamples, outputPath, cancellationToken);

            result.TotalExamples = trainingExamples.Count;
            result.Statistics["total_documents"] = documents.Count;
            result.Statistics["examples_per_document"] = trainingExamples.Count / Math.Max(1, documents.Count);
            result.Statistics["unique_categories"] = documents.Select(d => d.ActualCategory).Distinct().Count();

            _logger.LogInformation(
                "Training data prepared: {Examples} examples from {Docs} documents",
                result.TotalExamples, documents.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error preparing training data");
            result.Warnings.Add($"Error: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Generate contrastive pairs for similarity learning
    /// </summary>
    public async Task<List<ContrastivePair>> GenerateContrastivePairsAsync(
        List<int> documentIds,
        int numPairs = 1000,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating {NumPairs} contrastive pairs", numPairs);

        var documents = await _context.Documents
            .Where(d => documentIds.Contains(d.Id))
            .ToListAsync(cancellationToken);

        if (documents.Count < 2)
        {
            throw new InvalidOperationException("Need at least 2 documents to generate contrastive pairs");
        }

        var pairs = new List<ContrastivePair>();
        var random = new Random(42); // Fixed seed for reproducibility

        for (int i = 0; i < numPairs; i++)
        {
            // Pick random anchor document
            var anchorDoc = documents[random.Next(documents.Count)];
            var anchorChunks = _chunkingService.ChunkText(anchorDoc.ExtractedText, 300, 30);
            
            if (anchorChunks.Count == 0) continue;
            
            var anchor = anchorChunks[random.Next(anchorChunks.Count)];

            // Find positive example (from same document or same category)
            var positiveDoc = documents
                .Where(d => d.Id == anchorDoc.Id || d.ActualCategory == anchorDoc.ActualCategory)
                .OrderBy(d => random.Next())
                .FirstOrDefault();

            if (positiveDoc != null)
            {
                var positiveChunks = _chunkingService.ChunkText(positiveDoc.ExtractedText, 300, 30);
                if (positiveChunks.Count > 0)
                {
                    var positive = positiveChunks[random.Next(positiveChunks.Count)];

                    // Find negative example (from different category)
                    var negativeDoc = documents
                        .Where(d => d.ActualCategory != anchorDoc.ActualCategory)
                        .OrderBy(d => random.Next())
                        .FirstOrDefault();

                    if (negativeDoc != null)
                    {
                        var negativeChunks = _chunkingService.ChunkText(negativeDoc.ExtractedText, 300, 30);
                        if (negativeChunks.Count > 0)
                        {
                            var negative = negativeChunks[random.Next(negativeChunks.Count)];

                            pairs.Add(new ContrastivePair
                            {
                                Anchor = anchor,
                                Positive = positive,
                                Negative = negative,
                                Metadata = new Dictionary<string, string>
                                {
                                    ["anchor_doc_id"] = anchorDoc.Id.ToString(),
                                    ["anchor_category"] = anchorDoc.ActualCategory ?? "unknown",
                                    ["positive_doc_id"] = positiveDoc.Id.ToString(),
                                    ["negative_doc_id"] = negativeDoc.Id.ToString()
                                }
                            });
                        }
                    }
                }
            }
        }

        _logger.LogInformation("Generated {Count} contrastive pairs", pairs.Count);
        return pairs;
    }

    /// <summary>
    /// Evaluate embedding model quality on test set
    /// </summary>
    public async Task<EmbeddingEvaluationResult> EvaluateEmbeddingModelAsync(
        string testDatasetId,
        string modelName,
        CancellationToken cancellationToken = default)
    {
        var result = new EmbeddingEvaluationResult
        {
            ModelName = modelName,
            EvaluatedAt = DateTime.UtcNow
        };

        try
        {
            _logger.LogInformation("Evaluating embedding model: {Model} on dataset: {Dataset}",
                modelName, testDatasetId);

            // Load test documents
            var testDocs = await _context.Documents
                .Take(100) // Limit for evaluation
                .ToListAsync(cancellationToken);

            if (testDocs.Count == 0)
            {
                _logger.LogWarning("No test documents found");
                return result;
            }

            var similarities = new List<double>();
            var retrievalCorrect = 0;
            var totalTests = 0;

            // Evaluate on document pairs
            for (int i = 0; i < Math.Min(50, testDocs.Count - 1); i++)
            {
                var doc1 = testDocs[i];
                var doc2 = testDocs[i + 1];

                // Get embeddings (this would use the specified model in production)
                var embedding1 = await _embeddingService.GenerateEmbeddingAsync(
                    doc1.ExtractedText.Substring(0, Math.Min(500, doc1.ExtractedText.Length)));
                var embedding2 = await _embeddingService.GenerateEmbeddingAsync(
                    doc2.ExtractedText.Substring(0, Math.Min(500, doc2.ExtractedText.Length)));

                if (embedding1 != null && embedding2 != null)
                {
                    var similarity = CosineSimilarity(embedding1, embedding2);
                    similarities.Add(similarity);

                    // Check if same category
                    if (doc1.ActualCategory == doc2.ActualCategory && similarity > 0.7)
                    {
                        retrievalCorrect++;
                    }
                    totalTests++;

                    if (result.EmbeddingDimension == 0)
                    {
                        result.EmbeddingDimension = embedding1.Length;
                    }
                }
            }

            result.AverageSimilarityScore = similarities.Any() ? similarities.Average() : 0.0;
            result.RetrievalAccuracy = totalTests > 0 ? (double)retrievalCorrect / totalTests : 0.0;
            result.MAP = result.RetrievalAccuracy; // Simplified

            _logger.LogInformation(
                "Evaluation complete - Similarity: {Sim:F3}, Accuracy: {Acc:F3}",
                result.AverageSimilarityScore, result.RetrievalAccuracy);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating embedding model");
        }

        return result;
    }

    /// <summary>
    /// Create domain-specific embedding adapter
    /// </summary>
    public async Task<EmbeddingAdapterConfig> CreateDomainAdapterAsync(
        string baseModelName,
        List<int> domainDocumentIds,
        CancellationToken cancellationToken = default)
    {
        var config = new EmbeddingAdapterConfig
        {
            BaseModel = baseModelName,
            AdapterName = $"{baseModelName}_domain_{DateTime.UtcNow:yyyyMMdd}",
            CreatedAt = DateTime.UtcNow,
            TrainingDocumentCount = domainDocumentIds.Count
        };

        try
        {
            _logger.LogInformation("Creating domain adapter for model: {Model}", baseModelName);

            var documents = await _context.Documents
                .Where(d => domainDocumentIds.Contains(d.Id))
                .ToListAsync(cancellationToken);

            // Extract domain vocabulary (most important terms)
            var domainTerms = ExtractDomainVocabulary(documents, topN: 100);
            
            // Store term embeddings (simplified - in production, compute actual embeddings)
            foreach (var term in domainTerms)
            {
                // Placeholder: in production, generate actual embeddings for these terms
                config.DomainVocabulary[term] = new float[768]; // Placeholder
                config.WeightAdjustments[term] = 1.5; // Boost domain-specific terms
            }

            config.Metadata["document_count"] = documents.Count.ToString();
            config.Metadata["unique_categories"] = documents.Select(d => d.ActualCategory).Distinct().Count().ToString();
            config.Metadata["vocabulary_size"] = config.DomainVocabulary.Count.ToString();

            _logger.LogInformation(
                "Domain adapter created with {VocabSize} terms from {DocCount} documents",
                config.DomainVocabulary.Count, documents.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating domain adapter");
        }

        return config;
    }

    private List<string> GenerateSyntheticQueries(Document doc)
    {
        var queries = new List<string>();

        // Extract title-like lines as queries
        var lines = doc.ExtractedText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines.Take(10))
        {
            if (line.Length > 20 && line.Length < 200 && !line.Contains("  "))
            {
                queries.Add(line.Trim());
            }
        }

        // Generate question-based queries from category
        if (!string.IsNullOrEmpty(doc.ActualCategory))
        {
            queries.Add($"What is {doc.ActualCategory}?");
            queries.Add($"Information about {doc.ActualCategory}");
            queries.Add($"Find documents related to {doc.ActualCategory}");
        }

        return queries.Take(5).ToList(); // Limit queries per document
    }

    private string? FindMostRelevantChunk(string query, List<string> chunks)
    {
        // Simple relevance: find chunk with most query words
        var queryWords = query.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        var bestChunk = chunks
            .Select(chunk => new
            {
                Chunk = chunk,
                Score = queryWords.Count(word => chunk.ToLower().Contains(word))
            })
            .OrderByDescending(x => x.Score)
            .FirstOrDefault();

        return bestChunk?.Chunk;
    }

    private List<string> ExtractDomainVocabulary(List<Document> documents, int topN = 100)
    {
        var wordFreq = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var doc in documents)
        {
            var words = doc.ExtractedText
                .ToLower()
                .Split(new[] { ' ', '\n', '\t', ',', '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 4); // Filter short words

            foreach (var word in words)
            {
                wordFreq[word] = wordFreq.GetValueOrDefault(word, 0) + 1;
            }
        }

        return wordFreq
            .OrderByDescending(kvp => kvp.Value)
            .Take(topN)
            .Select(kvp => kvp.Key)
            .ToList();
    }

    private double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            return 0.0;

        double dotProduct = 0;
        double normA = 0;
        double normB = 0;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        if (normA == 0 || normB == 0)
            return 0.0;

        return dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }

    private async Task WriteTrainingDataAsync(
        List<TrainingExample> examples,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var writer = new StreamWriter(outputPath, false, Encoding.UTF8);
        
        foreach (var example in examples)
        {
            var json = JsonSerializer.Serialize(new
            {
                query = example.Query,
                positive = example.PositiveDocument,
                document_id = example.DocumentId,
                category = example.Category
            });
            
            await writer.WriteLineAsync(json);
        }
    }

    private class TrainingExample
    {
        public string Query { get; set; } = string.Empty;
        public string PositiveDocument { get; set; } = string.Empty;
        public int DocumentId { get; set; }
        public string Category { get; set; } = string.Empty;
    }
}
