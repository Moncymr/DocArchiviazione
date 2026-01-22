# RAG Improvements Implementation Summary

This implementation adds advanced RAG (Retrieval Augmented Generation) capabilities to the DocArchiviazione system, including semantic chunking, embedding fine-tuning, and comprehensive retrieval metrics.

## 1. Semantic Chunking with Rich Metadata

### New Features
- **Multiple Chunking Strategies**:
  - `SlidingWindow`: Simple overlapping chunks
  - `Semantic`: Paragraph and sentence-aware chunking
  - `Sentence`: Sentence-boundary chunking
  - `Paragraph`: Paragraph-based chunking
  - `Section`: Section and header-based chunking
  - `Adaptive`: Automatically selects best strategy based on content

- **Document Structure Detection**:
  - Markdown support (headers, code blocks, lists, tables)
  - HTML parsing and structure extraction
  - Plain text with intelligent boundary detection

- **Rich Metadata Extraction**:
  - Title/heading extraction from section headers
  - Section path hierarchy (e.g., "Chapter 1 > Section 1.1")
  - Automatic keyword extraction (TF-based, stop-word filtering)
  - Chunk type classification (Header, Paragraph, Code, Table, ListItem)
  - Importance scoring based on position, keywords, and length

### Enhanced DocumentChunk Model
```csharp
public class DocumentChunk
{
    // Existing fields...
    
    // New metadata fields:
    public string? Title { get; set; }
    public string? SectionPath { get; set; }
    public string? ChunkType { get; set; }
    public string? KeywordsJson { get; set; }
    public double? ImportanceScore { get; set; }
    public string? Language { get; set; }
    public string? MetadataJson { get; set; }
}
```

### API Endpoints
- `POST /api/semanticchunking/chunk-text`: Chunk text with specified strategy
- `POST /api/semanticchunking/chunk-with-detection`: Auto-detect structure and chunk
- `POST /api/semanticchunking/extract-metadata`: Extract metadata from chunk
- `POST /api/semanticchunking/calculate-importance`: Calculate importance score
- `GET /api/semanticchunking/strategies`: List available strategies

## 2. Embedding Fine-Tuning

### New Features
- **Training Data Generation**:
  - Automatic creation of positive pairs (similar documents)
  - Automatic creation of negative pairs (dissimilar documents)
  - Category-based document grouping
  - Quality scoring for training pairs

- **Fine-Tuning Job Management**:
  - Job creation and tracking
  - Status monitoring
  - Model versioning and activation
  - Export training data in provider-specific formats

### New Models
```csharp
public class EmbeddingTrainingExample
{
    public string InputText { get; set; }
    public string PositiveExample { get; set; }
    public string? NegativeExample { get; set; }
    public string? Domain { get; set; }
    public double? QualityScore { get; set; }
}

public class FineTuningJob
{
    public string Name { get; set; }
    public string BaseModel { get; set; }
    public string Provider { get; set; }
    public string Status { get; set; }
    public string? FineTunedModelId { get; set; }
    // ... other fields
}

public class FineTunedModel
{
    public string ModelId { get; set; }
    public string DisplayName { get; set; }
    public bool IsActive { get; set; }
    public string? PerformanceMetricsJson { get; set; }
    // ... other fields
}
```

### API Endpoints
- `POST /api/finetuning/generate-training-examples`: Generate training pairs
- `POST /api/finetuning/create-job`: Create fine-tuning job
- `GET /api/finetuning/job-status/{jobId}`: Get job status
- `GET /api/finetuning/models`: List fine-tuned models
- `POST /api/finetuning/activate-model/{modelId}`: Activate a model
- `POST /api/finetuning/export-training-data`: Export training data

**Note**: Provider-specific fine-tuning implementation (OpenAI API calls) is a placeholder and requires completion.

## 3. Retrieval Metrics and Evaluation

### Implemented Metrics
- **MRR (Mean Reciprocal Rank)**: Measures how quickly the first relevant result appears
- **NDCG (Normalized Discounted Cumulative Gain)**: Evaluates ranking quality with graded relevance
  - NDCG@5 and NDCG@10 calculated
- **MAP (Mean Average Precision)**: Average of precision values at relevant document positions
- **Precision@K**: Fraction of relevant documents in top-K results
- **Recall@K**: Fraction of all relevant documents found in top-K

### Evaluation Features
- **Evaluation Datasets**: Store queries with ground truth relevance
- **Historical Tracking**: Track metric improvements over time
- **A/B Comparison**: Compare two evaluation runs
- **Per-Query Analysis**: Detailed metrics for each query

### New Models
```csharp
public class RetrievalEvaluationQuery
{
    public string QueryText { get; set; }
    public string RelevantDocumentIdsJson { get; set; }
    public string? RelevanceScoresJson { get; set; }
    public string? Domain { get; set; }
    public string? DifficultyLevel { get; set; }
}

public class RetrievalEvaluationResult
{
    public string EvaluationName { get; set; }
    public double MRRScore { get; set; }
    public double NDCG_at_5 { get; set; }
    public double NDCG_at_10 { get; set; }
    public double Precision_at_1 { get; set; }
    public double Precision_at_5 { get; set; }
    public double Recall_at_5 { get; set; }
    public double Recall_at_10 { get; set; }
    public double MAP { get; set; }
    // ... other fields
}
```

### API Endpoints
- `POST /api/retrievalmetrics/evaluate`: Run full evaluation
- `GET /api/retrievalmetrics/history`: Get evaluation history
- `GET /api/retrievalmetrics/compare`: Compare two evaluations
- `POST /api/retrievalmetrics/calculate-mrr`: Calculate MRR for results
- `POST /api/retrievalmetrics/calculate-ndcg`: Calculate NDCG for results

## 4. Integration and Architecture

### Service Registration
All new services are registered in the DI container:
```csharp
builder.Services.AddScoped<ISemanticChunkingService, SemanticChunkingService>();
builder.Services.AddScoped<IFineTuningService, FineTuningService>();
builder.Services.AddScoped<IRetrievalMetricsService, RetrievalMetricsService>();
```

### Database Models
New DbSets added to ApplicationDbContext:
- `EmbeddingTrainingExamples`
- `FineTuningJobs`
- `FineTunedModels`
- `RetrievalEvaluationQueries`
- `RetrievalEvaluationResults`
- `QueryEvaluationDetails`

## 5. Usage Examples

### Example 1: Semantic Chunking
```csharp
var options = new ChunkingOptions
{
    MaxChunkSize = 1000,
    Overlap = 200,
    ExtractKeywords = true,
    DetectSections = true
};

var chunks = await chunkingService.ChunkDocumentSemanticAsync(
    documentText,
    ChunkingStrategy.Semantic,
    options
);

// Each chunk has: Text, Title, SectionPath, Keywords, ImportanceScore
```

### Example 2: Generate Training Data
```csharp
var examples = await fineTuningService.GenerateTrainingExamplesAsync(
    documentIds: new[] { 1, 2, 3, 4, 5 },
    maxExamples: 1000
);

// Each example has: InputText, PositiveExample, NegativeExample
```

### Example 3: Run Retrieval Evaluation
```csharp
var queries = new List<EvaluationQuery>
{
    new() 
    { 
        QueryId = 1,
        QueryText = "What is semantic search?",
        RelevantDocumentIds = new List<int> { 10, 15, 20 },
        RelevanceScores = new Dictionary<int, double> 
        { 
            { 10, 1.0 }, 
            { 15, 0.8 }, 
            { 20, 0.6 } 
        }
    }
};

var result = await metricsService.EvaluateRetrievalAsync(
    "Baseline Evaluation",
    queries,
    "v1.0",
    "semantic"
);

// Result contains: MRR, NDCG@5, NDCG@10, MAP, Precision@K, Recall@K
```

## 6. Known Limitations and Future Work

### Current Limitations
1. **Fine-Tuning**: Provider-specific API integration not implemented (placeholder only)
2. **Similarity Scores**: SearchSimilarDocumentsAsync doesn't return actual similarity scores
3. **Database Migration**: Requires manual migration creation for new models
4. **Language Support**: Keyword extraction optimized for English

### Recommended Improvements
1. Implement OpenAI/Cohere fine-tuning API integration
2. Enhance SearchSimilarDocumentsAsync to return confidence scores
3. Add multi-language support for keyword extraction
4. Implement chunk-level (not just document-level) retrieval metrics
5. Add automated evaluation scheduling
6. Create UI dashboard for metrics visualization

## 7. Testing Strategy

### Recommended Tests
1. **Unit Tests**:
   - Chunking strategies with various document types
   - Metric calculations with known inputs
   - Metadata extraction accuracy

2. **Integration Tests**:
   - End-to-end evaluation pipeline
   - Training data generation quality
   - API endpoint responses

3. **Performance Tests**:
   - Chunking speed for large documents
   - Metric calculation efficiency
   - Database query performance

## 8. Security Considerations

- All API endpoints require authentication (`[Authorize]` attribute)
- Input validation for chunking parameters
- SQL injection protection via Entity Framework
- No sensitive data exposure in error messages
- Audit logging for fine-tuning jobs and evaluations

## 9. Monitoring and Observability

### Recommended Metrics to Track
- Chunking performance (time per document)
- Evaluation run duration
- Fine-tuning job success rate
- Average retrieval metrics scores
- API endpoint response times

### Logging
- All services use ILogger for structured logging
- Log levels: Information, Warning, Error
- Key operations logged with context

## 10. Documentation

### API Documentation
All endpoints are documented with:
- XML comments
- Request/response models
- Example usage
- Known limitations

### Code Documentation
- Interfaces fully documented
- Complex algorithms explained
- TODO comments for future work

## Conclusion

This implementation provides a solid foundation for advanced RAG capabilities. The modular design allows for incremental improvements and easy integration with existing document processing workflows. The comprehensive metrics system enables data-driven optimization of retrieval quality.
