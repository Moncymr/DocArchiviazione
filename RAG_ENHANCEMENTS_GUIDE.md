# RAG System Enhancements - Implementation Guide

This document describes the RAG (Retrieval-Augmented Generation) system enhancements implemented for improved document chunking, metadata extraction, retrieval quality metrics, and embedding fine-tuning.

## Overview

The following enhancements have been implemented:

1. **Semantic Chunking** - Advanced document chunking based on structure and semantics
2. **Rich Metadata** - Comprehensive metadata extraction for each chunk
3. **Retrieval Metrics** - Standard IR metrics (MRR, NDCG, Hit Rate, etc.)
4. **Embedding Fine-tuning** - Domain adaptation and model fine-tuning support

## 1. Semantic Chunking

### Features

The `ISemanticChunkingService` provides advanced chunking capabilities:

- **Structure-aware chunking**: Respects document hierarchy (headers, sections)
- **Semantic boundaries**: Splits at paragraph and sentence boundaries
- **Automatic metadata extraction**: Extracts titles, keywords, and section information

### Usage Example

```csharp
// Inject the service
public class DocumentProcessor
{
    private readonly ISemanticChunkingService _semanticChunker;
    
    public DocumentProcessor(ISemanticChunkingService semanticChunker)
    {
        _semanticChunker = semanticChunker;
    }
    
    public async Task ProcessDocument(Document document)
    {
        // Option 1: Semantic chunking (paragraph-aware)
        var semanticChunks = _semanticChunker.ChunkBySemantic(
            document.ExtractedText, 
            maxChunkSize: 1000, 
            minChunkSize: 200);
        
        // Option 2: Structure-based chunking (section-aware)
        var structuredChunks = _semanticChunker.ChunkByStructure(
            document.ExtractedText, 
            maxChunkSize: 1000);
        
        // Each chunk includes rich metadata
        foreach (var chunk in structuredChunks)
        {
            Console.WriteLine($"Section: {chunk.Metadata.Title}");
            Console.WriteLine($"Keywords: {string.Join(", ", chunk.Metadata.Keywords)}");
            Console.WriteLine($"Type: {chunk.ChunkType}");
            Console.WriteLine($"Header Level: {chunk.Metadata.HeaderLevel}");
        }
    }
}
```

### Integration with ChunkingService

The standard `ChunkingService` has been extended with a semantic variant:

```csharp
// Inject both services
private readonly IChunkingService _chunkingService;
private readonly ISemanticChunkingService _semanticChunker;

// Use semantic chunking to create DocumentChunk entities
var chunks = _chunkingService.ChunkDocumentSemantic(
    document, 
    _semanticChunker, 
    maxChunkSize: 1000);

// Save to database
await _context.DocumentChunks.AddRangeAsync(chunks);
await _context.SaveChangesAsync();
```

## 2. Rich Metadata

### Database Schema

The `DocumentChunk` model has been extended with the following fields:

```csharp
public class DocumentChunk
{
    // Existing fields...
    
    // Rich metadata fields
    public string? SectionTitle { get; set; }           // Section title
    public string? SectionPath { get; set; }            // Hierarchy (e.g., "1.2.3")
    public string? KeywordsJson { get; set; }           // JSON array of keywords
    public string? DocumentType { get; set; }           // Type hint (technical, legal, etc.)
    public int HeaderLevel { get; set; }                // 0-6 for H1-H6
    public string ChunkType { get; set; }               // paragraph, section, sentence
    public bool IsListItem { get; set; }                // Is this part of a list?
    public string? CustomMetadataJson { get; set; }     // Additional metadata
}
```

### Migration

A migration has been created to add these fields to the database:

```bash
# The migration is included in the codebase
# File: 20260122091800_AddChunkRichMetadata.cs
```

### Querying with Metadata

```csharp
// Find chunks by section
var sectionChunks = await _context.DocumentChunks
    .Where(c => c.SectionTitle == "Introduction")
    .ToListAsync();

// Find chunks by type
var paragraphChunks = await _context.DocumentChunks
    .Where(c => c.ChunkType == "paragraph")
    .ToListAsync();

// Find header chunks
var headers = await _context.DocumentChunks
    .Where(c => c.HeaderLevel > 0)
    .OrderBy(c => c.HeaderLevel)
    .ToListAsync();
```

## 3. Retrieval Metrics

### Available Metrics

The `IRetrievalMetricsService` implements standard Information Retrieval metrics:

- **MRR (Mean Reciprocal Rank)**: Position of first relevant document
- **NDCG (Normalized Discounted Cumulative Gain)**: Ranking quality with position discount
- **Hit Rate**: Presence of at least one relevant document in top K
- **Precision@K**: Fraction of relevant documents in top K
- **Recall@K**: Fraction of relevant documents retrieved

### Usage Example

```csharp
public class RetrievalEvaluator
{
    private readonly IRetrievalMetricsService _metricsService;
    
    public async Task EvaluateRetrieval()
    {
        // Evaluate on golden dataset
        var result = await _metricsService.EvaluateRetrievalQualityAsync(
            datasetId: "test_dataset_2024");
        
        Console.WriteLine($"MRR: {result.MRR:F3}");
        Console.WriteLine($"NDCG@10: {result.NDCG:F3}");
        Console.WriteLine($"Hit Rate@10: {result.HitRate:F3}");
        Console.WriteLine($"Precision@10: {result.PrecisionAtK:F3}");
        Console.WriteLine($"Recall@10: {result.RecallAtK:F3}");
        
        // Per-query breakdown
        foreach (var (queryId, metrics) in result.PerQueryMetrics)
        {
            Console.WriteLine($"Query: {metrics.Query}");
            Console.WriteLine($"  MRR: {metrics.MRR:F3}");
            Console.WriteLine($"  NDCG: {metrics.NDCG:F3}");
        }
    }
}
```

### A/B Testing

Compare two retrieval configurations:

```csharp
var abTest = await _metricsService.CompareRetrievalConfigurationsAsync(
    configurationA: "baseline",
    configurationB: "semantic_chunking",
    datasetId: "test_dataset_2024");

Console.WriteLine($"Winner: {abTest.Winner}");
Console.WriteLine($"MRR Improvement: {abTest.ImprovementPercentages["MRR"]:F1}%");
Console.WriteLine($"Statistically Significant: {abTest.IsStatisticallySignificant}");
```

## 4. Embedding Fine-tuning

### Features

The `IEmbeddingFineTuningService` provides:

- **Training data preparation**: Create query-document pairs from domain documents
- **Contrastive pair generation**: Generate positive/negative examples for similarity learning
- **Model evaluation**: Assess embedding quality with standard metrics
- **Domain adaptation**: Create lightweight adapters for base models

### Training Data Preparation

```csharp
public class EmbeddingTrainer
{
    private readonly IEmbeddingFineTuningService _fineTuningService;
    
    public async Task PrepareTrainingData()
    {
        // Select domain documents
        var documentIds = await _context.Documents
            .Where(d => d.ActualCategory == "technical")
            .Select(d => d.Id)
            .ToListAsync();
        
        // Prepare training data
        var result = await _fineTuningService.PrepareTrainingDataAsync(
            documentIds: documentIds,
            outputPath: "/path/to/training_data.jsonl");
        
        Console.WriteLine($"Created {result.TotalExamples} training examples");
        Console.WriteLine($"Format: {result.Format}");
        Console.WriteLine($"Output: {result.OutputPath}");
    }
}
```

### Contrastive Pair Generation

```csharp
// Generate contrastive pairs for similarity learning
var pairs = await _fineTuningService.GenerateContrastivePairsAsync(
    documentIds: documentIds,
    numPairs: 1000);

// Each pair has anchor, positive, and negative examples
foreach (var pair in pairs.Take(5))
{
    Console.WriteLine($"Anchor: {pair.Anchor.Substring(0, 50)}...");
    Console.WriteLine($"Positive: {pair.Positive.Substring(0, 50)}...");
    Console.WriteLine($"Negative: {pair.Negative.Substring(0, 50)}...");
    Console.WriteLine($"Category: {pair.Metadata["anchor_category"]}");
    Console.WriteLine();
}
```

### Model Evaluation

```csharp
// Evaluate embedding model quality
var evaluation = await _fineTuningService.EvaluateEmbeddingModelAsync(
    testDatasetId: "test_dataset",
    modelName: "text-embedding-ada-002");

Console.WriteLine($"Model: {evaluation.ModelName}");
Console.WriteLine($"Average Similarity: {evaluation.AverageSimilarityScore:F3}");
Console.WriteLine($"Retrieval Accuracy: {evaluation.RetrievalAccuracy:F3}");
Console.WriteLine($"MAP: {evaluation.MAP:F3}");
Console.WriteLine($"Dimension: {evaluation.EmbeddingDimension}");
```

### Domain Adapter Creation

```csharp
// Create domain-specific adapter
var adapter = await _fineTuningService.CreateDomainAdapterAsync(
    baseModelName: "text-embedding-ada-002",
    domainDocumentIds: documentIds);

Console.WriteLine($"Adapter: {adapter.AdapterName}");
Console.WriteLine($"Base Model: {adapter.BaseModel}");
Console.WriteLine($"Vocabulary Size: {adapter.DomainVocabulary.Count}");
Console.WriteLine($"Training Docs: {adapter.TrainingDocumentCount}");
```

## Best Practices

### Chunking Strategy Selection

1. **Use semantic chunking** when documents have clear structure (headers, sections)
2. **Use standard chunking** for unstructured text or when performance is critical
3. **Adjust chunk size** based on embedding model limits (typically 500-1000 characters)
4. **Set overlap** to 10-20% of chunk size to preserve context

### Metadata Utilization

1. **Filter by section** to improve retrieval precision
2. **Use keywords** for hybrid search (keyword + semantic)
3. **Filter by document type** to constrain search to relevant categories
4. **Leverage headers** to understand document structure

### Retrieval Metrics

1. **Use MRR** when first result matters most (e.g., QA systems)
2. **Use NDCG** when ranking quality matters (e.g., search results)
3. **Monitor Hit Rate** to ensure baseline retrieval is working
4. **Track Precision and Recall** for balanced evaluation

### Fine-tuning Workflow

1. **Collect domain documents** (100+ documents minimum)
2. **Generate training data** with diverse examples
3. **Create contrastive pairs** for similarity learning
4. **Evaluate baseline** before fine-tuning
5. **Fine-tune model** (external process, not in this codebase)
6. **Evaluate improvements** using retrieval metrics
7. **Consider domain adapters** for lightweight customization

## Configuration

### Service Registration

Services are registered in `Program.cs`:

```csharp
// Already registered automatically
builder.Services.AddScoped<ISemanticChunkingService, SemanticChunkingService>();
builder.Services.AddScoped<IRetrievalMetricsService, RetrievalMetricsService>();
builder.Services.AddScoped<IEmbeddingFineTuningService, EmbeddingFineTuningService>();
```

### Chunking Configuration

Configure chunking parameters in your code:

```csharp
// For technical documents (longer chunks)
var technicalChunks = _semanticChunker.ChunkByStructure(text, maxChunkSize: 1500);

// For general documents (standard chunks)
var generalChunks = _semanticChunker.ChunkBySemantic(text, maxChunkSize: 1000, minChunkSize: 200);

// For chat/Q&A (shorter chunks)
var chatChunks = _semanticChunker.ChunkBySemantic(text, maxChunkSize: 500, minChunkSize: 100);
```

## Performance Considerations

1. **Semantic chunking** is slightly slower than standard chunking due to structure analysis
2. **Metadata extraction** adds minimal overhead (<5% processing time)
3. **Retrieval metrics** can be computed offline on golden datasets
4. **Fine-tuning preparation** is a one-time cost per document collection

## Future Enhancements

Potential improvements for future iterations:

1. **Multi-language support** for semantic chunking
2. **Learned chunking boundaries** using ML models
3. **Automatic metadata enrichment** using LLMs
4. **Online learning** for embedding adaptation
5. **Real-time metrics** dashboard for monitoring

## References

- [RAGAS: Evaluation Framework for RAG](https://arxiv.org/abs/2309.15217)
- [Information Retrieval Metrics](https://en.wikipedia.org/wiki/Evaluation_measures_(information_retrieval))
- [Contrastive Learning for Text Embeddings](https://arxiv.org/abs/2004.04906)
- [Semantic Text Segmentation](https://arxiv.org/abs/2012.08866)
