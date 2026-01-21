using DocN.Data;
using DocN.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace DocN.Server.Services;

public class DatabaseSeeder
{
    private readonly DocArcContext _context;
    private readonly ApplicationDbContext _appContext;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(DocArcContext context, ApplicationDbContext appContext, ILogger<DatabaseSeeder> logger)
    {
        _context = context;
        _appContext = appContext;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        try
        {
            // Seed AI Configuration first
            await SeedAIConfigurationAsync();
            
            // Check if we already have data
            if (_context.Documents.Any())
            {
                _logger.LogInformation("Database already has documents, skipping seeding");
                return;
            }

            // Add sample documents - some with vectors, some without
            var documents = new List<Document>
            {
                new Document
                {
                    FileName = "Project_Requirements.pdf",
                    FilePath = "/documents/Project_Requirements.pdf",
                    ContentType = "application/pdf",
                    FileSize = 1024000,
                    ExtractedText = "This document contains the project requirements for DocN system...",
                    SuggestedCategory = "Requirements",
                    ActualCategory = "Requirements",
                    EmbeddingVector = null, // Simulated vector would go here
                    UploadedAt = DateTime.UtcNow.AddDays(-5),
                    Visibility = DocumentVisibility.Private
                },
                new Document
                {
                    FileName = "Technical_Specification.docx",
                    FilePath = "/documents/Technical_Specification.docx",
                    ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    FileSize = 512000,
                    ExtractedText = "Technical specifications for the DocN architecture...",
                    SuggestedCategory = "Technical",
                    ActualCategory = "Technical",
                    EmbeddingVector = null, // NO VECTOR - this should still be shown!
                    UploadedAt = DateTime.UtcNow.AddDays(-4),
                    Visibility = DocumentVisibility.Private
                },
                new Document
                {
                    FileName = "User_Guide.pdf",
                    FilePath = "/documents/User_Guide.pdf",
                    ContentType = "application/pdf",
                    FileSize = 768000,
                    ExtractedText = "User guide for the DocN application...",
                    SuggestedCategory = "Documentation",
                    ActualCategory = "Documentation",
                    EmbeddingVector = null, // NO VECTOR - this should still be shown!
                    UploadedAt = DateTime.UtcNow.AddDays(-3),
                    Visibility = DocumentVisibility.Private
                },
                new Document
                {
                    FileName = "Meeting_Notes_2025.txt",
                    FilePath = "/documents/Meeting_Notes_2025.txt",
                    ContentType = "text/plain",
                    FileSize = 102400,
                    ExtractedText = "Meeting notes from December 2025...",
                    SuggestedCategory = "Notes",
                    ActualCategory = "Notes",
                    EmbeddingVector = null, // Simulated vector would go here
                    UploadedAt = DateTime.UtcNow.AddDays(-2),
                    Visibility = DocumentVisibility.Private
                },
                new Document
                {
                    FileName = "Budget_Report.xlsx",
                    FilePath = "/documents/Budget_Report.xlsx",
                    ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    FileSize = 256000,
                    ExtractedText = "Budget report for Q4 2025...",
                    SuggestedCategory = "Financial",
                    ActualCategory = "Financial",
                    EmbeddingVector = null, // NO VECTOR - this should still be shown!
                    UploadedAt = DateTime.UtcNow.AddDays(-1),
                    Visibility = DocumentVisibility.Private
                }
            };

            _context.Documents.AddRange(documents);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Seeded {Count} sample documents (some with vectors, some without)", documents.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding database");
        }
    }

    private async Task SeedAIConfigurationAsync()
    {
        try
        {
            // Check if we already have an AI configuration
            if (await _appContext.AIConfigurations.AnyAsync())
            {
                _logger.LogInformation("AI Configuration already exists, skipping seeding");
                return;
            }

            // Default provider type for all services
            const AIProviderType defaultProvider = AIProviderType.Gemini;

            // Create a default AI configuration that needs to be configured by the user
            var defaultConfig = new AIConfiguration
            {
                ConfigurationName = "Default Configuration",
                IsActive = true,
                
                // Set default providers (will need API keys to be configured)
                ChatProvider = defaultProvider,
                EmbeddingsProvider = defaultProvider,
                TagExtractionProvider = defaultProvider,
                RAGProvider = defaultProvider,
                
                // Default models
                GeminiChatModel = "gemini-1.5-flash",
                GeminiEmbeddingModel = "text-embedding-004",
                OpenAIChatModel = "gpt-4",
                OpenAIEmbeddingModel = "text-embedding-ada-002",
                
                // RAG settings
                MaxDocumentsToRetrieve = 5,
                SimilarityThreshold = 0.7,
                MaxTokensForContext = 4000,
                
                // Chunking settings
                EnableChunking = true,
                ChunkSize = 1000,
                ChunkOverlap = 200,
                
                // Enable fallback
                EnableFallback = true,
                
                CreatedAt = DateTime.UtcNow
            };

            _appContext.AIConfigurations.Add(defaultConfig);
            await _appContext.SaveChangesAsync();

            _logger.LogInformation("Created default AI configuration. Please configure API keys via the application.");
            _logger.LogWarning("IMPORTANT: The AI configuration has been created but API keys need to be configured!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding AI configuration");
        }
    }
}
