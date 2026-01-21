namespace DocN.Data.Models;

// For dashboard analytics
public class DocumentStatistics
{
    public int TotalDocuments { get; set; }
    public long TotalStorageBytes { get; set; }
    public int DocumentsUploadedToday { get; set; }
    public int DocumentsUploadedThisWeek { get; set; }
    public int DocumentsUploadedThisMonth { get; set; }
    public Dictionary<string, int> DocumentsByCategory { get; set; } = new();
    public Dictionary<string, int> DocumentsByType { get; set; } = new();
    public List<TopDocument> MostAccessedDocuments { get; set; } = new();
    public List<CategoryOptimization> OptimizationSuggestions { get; set; } = new();
    
    // Embedding Queue Statistics
    public int PendingEmbeddingsCount { get; set; }
    public int ProcessingEmbeddingsCount { get; set; }
    public int CompletedEmbeddingsCount { get; set; }
    public int PendingChunksCount { get; set; }
    public double EstimatedProcessingTimeMinutes { get; set; }
    public DateTime? LastBatchProcessingTime { get; set; }
}

public class TopDocument
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public int AccessCount { get; set; }
    public DateTime LastAccessedAt { get; set; }
}

public class CategoryOptimization
{
    public string Category { get; set; } = string.Empty;
    public int DocumentCount { get; set; }
    public string Suggestion { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}
