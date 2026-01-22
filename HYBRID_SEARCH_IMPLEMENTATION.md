# Hybrid Search Enhancement - Implementation Summary

## Overview
This implementation adds comprehensive hybrid search improvements to the DocArchiviazione document management system, combining BM25 keyword-based search with vector semantic search, along with advanced filtering, caching, and multi-hop query capabilities.

## Features Implemented

### 1. BM25 Scoring Algorithm
**Files:**
- `DocN.Core/Interfaces/IBM25Service.cs` - Interface definition
- `DocN.Data/Services/BM25Service.cs` - Implementation

**Description:**
- Implements Best Matching 25 (BM25), a probabilistic ranking function used in information retrieval
- Supports term frequency (TF) and inverse document frequency (IDF) calculations
- Configurable parameters: K1=1.5 (term frequency saturation), B=0.75 (length normalization)
- Provides better relevance scoring compared to simple keyword matching
- Updates document statistics dynamically for accurate IDF computation

**Usage:**
```csharp
var bm25Service = serviceProvider.GetService<IBM25Service>();
var score = bm25Service.CalculateScore(query, documentText);
```

### 2. Semantic Cache
**Files:**
- `DocN.Core/Interfaces/ISemanticCacheService.cs` - Interface definition
- `DocN.Data/Services/SemanticCacheService.cs` - Implementation

**Description:**
- Intelligent caching that matches similar queries, not just exact duplicates
- Uses embedding similarity to find semantically similar cached queries
- Configurable similarity threshold (default: 0.95)
- Tracks cache statistics including hit rate and semantic hit rate
- Memory-efficient with automatic cache eviction

**Usage:**
```csharp
// Cache lookup with similarity matching
var cachedResults = await semanticCache.GetCachedResultsAsync<List<SearchResult>>(
    query, queryEmbedding, similarityThreshold: 0.95);

// Cache storage
await semanticCache.SetCachedResultsAsync(query, queryEmbedding, results);
```

### 3. Multi-Hop Search
**Files:**
- `DocN.Core/Interfaces/IMultiHopSearchService.cs` - Interface definition
- `DocN.Data/Services/MultiHopSearchService.cs` - Implementation

**Description:**
- Decomposes complex queries into multiple simpler sub-queries
- Uses AI (via Semantic Kernel) to analyze and break down queries
- Executes searches in multiple steps (hops)
- Deduplicates and aggregates results across hops
- Provides detailed tracing of each hop for debugging

**Usage:**
```csharp
var multiHopService = serviceProvider.GetService<IMultiHopSearchService>();
var result = await multiHopService.SearchAsync(
    "Find technical documents about API security written after 2023", 
    maxHops: 3, 
    topKPerHop: 5);
```

### 4. Advanced Filters
**Enhanced SearchOptions class with:**
- `DateFrom` / `DateTo` - Filter documents by upload date range
- `DocumentType` - Filter by content type (PDF, DOCX, etc.)
- `Author` - Filter by document owner/author
- `VectorWeight` / `TextWeight` - Configure balance between semantic and keyword search
- `UseBM25` - Enable/disable BM25 scoring
- `UseSemanticCache` - Enable/disable semantic caching
- `EnableQueryExpansion` - Enable/disable query expansion with synonyms

### 5. Weighted Fusion
**Enhanced HybridSearchService with:**
- Configurable weight optimization between vector and text search
- Replaces fixed Reciprocal Rank Fusion (RRF) with weighted fusion
- Allows fine-tuning for different use cases:
  - Higher vector weight for semantic understanding
  - Higher text weight for exact keyword matching
- Normalized weights ensure consistent scoring

### 6. Query Expansion Integration
- Integrated existing `IQueryRewritingService` for synonym expansion
- Configurable via `EnableQueryExpansion` flag
- Automatically re-generates embeddings for expanded queries
- Graceful fallback if expansion fails

## Enhanced Components

### HybridSearchService Improvements
**File:** `DocN.Data/Services/HybridSearchService.cs`

**Key Changes:**
1. Added optional dependencies via constructor injection:
   - `IBM25Service` for BM25 scoring
   - `ISemanticCacheService` for intelligent caching
   - `IQueryRewritingService` for query expansion
   - `ILogger` for debugging

2. Optimized embedding generation to avoid duplication

3. Added comprehensive filtering to both vector and text search methods

4. Implemented weighted fusion algorithm as alternative to RRF

5. Added extensive logging for monitoring and debugging

### SearchController Updates
**File:** `DocN.Server/Controllers/SearchController.cs`

**Enhanced SearchRequest model with:**
- All new filter options
- Weight configuration parameters
- Feature toggles (BM25, semantic cache, query expansion)
- Backward compatible with existing API

### Service Registration
**File:** `DocN.Server/Program.cs`

**Added registrations:**
```csharp
builder.Services.AddScoped<DocN.Core.Interfaces.IBM25Service, BM25Service>();
builder.Services.AddScoped<DocN.Core.Interfaces.ISemanticCacheService, SemanticCacheService>();
builder.Services.AddScoped<DocN.Core.Interfaces.IMultiHopSearchService, MultiHopSearchService>();
```

## Technical Decisions

### 1. Optional Dependencies
All new services are optional in `HybridSearchService` constructor, ensuring:
- Backward compatibility with existing code
- Graceful degradation if services are not registered
- Flexibility in deployment scenarios

### 2. Italian Comments and Documentation
Following the existing codebase pattern, Italian is used for:
- User-facing API documentation
- XML comments in controllers
- Code comments remain in English for international collaboration

### 3. Performance Optimizations
- Single embedding generation per query (avoid duplication)
- Candidate limiting in vector search (10x multiplier, min 100)
- Memory-efficient semantic cache with automatic eviction
- BM25 statistics updated lazily (only when needed)

### 4. Error Handling
- Comprehensive try-catch blocks
- Fallback mechanisms (e.g., text-only search if embedding fails)
- Graceful degradation (e.g., original query if expansion fails)
- Extensive logging for troubleshooting

## Usage Examples

### Basic Hybrid Search with Advanced Filters
```csharp
var request = new SearchRequest
{
    Query = "sicurezza API",
    TopK = 10,
    MinSimilarity = 0.7,
    DateFrom = new DateTime(2023, 1, 1),
    DateTo = DateTime.UtcNow,
    DocumentType = "application/pdf",
    VectorWeight = 0.6,
    TextWeight = 0.4,
    UseBM25 = true,
    UseSemanticCache = true
};

var results = await searchService.SearchAsync(request.Query, options);
```

### Multi-Hop Complex Query
```csharp
var result = await multiHopService.SearchAsync(
    "Trova documenti tecnici sulla sicurezza delle API pubblicati dopo gennaio 2023 che menzionano OAuth o JWT",
    maxHops: 3,
    topKPerHop: 5
);

// Access intermediate steps
foreach (var hop in result.Hops)
{
    Console.WriteLine($"Hop {hop.HopNumber}: {hop.SubQuery}");
    Console.WriteLine($"Reasoning: {hop.Reasoning}");
    Console.WriteLine($"Results: {hop.Results.Count}");
}
```

### Weighted Search Configuration
```csharp
// Prioritize semantic understanding
var semanticOptions = new SearchOptions
{
    VectorWeight = 0.8,
    TextWeight = 0.2,
    UseBM25 = true
};

// Prioritize exact keyword matching
var keywordOptions = new SearchOptions
{
    VectorWeight = 0.2,
    TextWeight = 0.8,
    UseBM25 = true
};
```

## Testing Recommendations

### Unit Tests
1. BM25 scoring with known document collections
2. Semantic cache hit/miss scenarios
3. Multi-hop query decomposition
4. Filter application correctness
5. Weighted fusion score calculation

### Integration Tests
1. End-to-end search with all filters
2. Cache effectiveness over multiple queries
3. Multi-hop search with real documents
4. Performance testing with large document sets

### Performance Tests
1. Embedding generation optimization
2. BM25 calculation overhead
3. Semantic cache lookup speed
4. Memory usage with cache

## Configuration

### Default Values
- Vector Weight: 0.5
- Text Weight: 0.5
- Min Similarity: 0.3
- Top K: 10
- BM25 Enabled: true
- Semantic Cache Enabled: true
- Query Expansion Enabled: false
- Semantic Cache Similarity: 0.95
- BM25 K1: 1.5
- BM25 B: 0.75

### Tuning Recommendations
- **Academic/Research**: Higher vector weight (0.7-0.8) for semantic understanding
- **Legal/Compliance**: Higher text weight (0.7-0.8) for exact terms
- **General Use**: Balanced weights (0.5/0.5)
- **Low Latency**: Disable query expansion
- **High Accuracy**: Enable all features

## Security Considerations

### Input Validation
- Query length limits in controller
- Parameter validation for weights (0-1 range)
- Date range validation
- SQL injection prevention via EF Core parameterized queries

### Data Privacy
- Query truncation in logs (max 50 characters)
- No sensitive data in cache keys
- Proper authorization checks via existing filters

### Performance
- Candidate limiting prevents excessive memory usage
- Cache size monitoring recommended
- Query expansion timeout protection

## Future Enhancements

1. **Distributed Cache**: Support Redis for multi-instance deployments
2. **A/B Testing**: Built-in framework for testing weight configurations
3. **Learning to Rank**: ML-based weight optimization
4. **Query Analytics**: Track query patterns and cache effectiveness
5. **Custom BM25 Parameters**: Per-collection tuning
6. **Async Batch Processing**: Parallel multi-hop execution

## Migration Notes

### Backward Compatibility
- All new features are optional
- Existing search APIs work unchanged
- Default weights maintain similar behavior to RRF
- No database schema changes required

### Deployment Steps
1. Deploy new code with service registrations
2. Monitor logs for any DI issues
3. Gradually enable new features via configuration
4. Test with production traffic
5. Tune weights based on user feedback

## Conclusion

This implementation provides a comprehensive hybrid search solution that:
- Combines best-of-breed techniques (BM25 + Vector Search)
- Offers flexibility through configurable weights and filters
- Improves performance via semantic caching
- Handles complex queries through multi-hop search
- Maintains backward compatibility
- Follows existing codebase patterns

The modular design allows gradual adoption and easy testing of individual components.
