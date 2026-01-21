using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using DocN.Data.Models;

namespace DocN.Data;

public class DocArcContext : DbContext
{
    public DocArcContext(DbContextOptions<DocArcContext> options) : base(options)
    {
    }

    public DbSet<Document> Documents { get; set; } = null!;
    public DbSet<DocumentChunk> DocumentChunks { get; set; } = null!;
    public DbSet<SimilarDocument> SimilarDocuments { get; set; } = null!;
    public DbSet<LogEntry> LogEntries { get; set; } = null!;
    public DbSet<DocumentConnector> DocumentConnectors { get; set; } = null!;
    public DbSet<IngestionSchedule> IngestionSchedules { get; set; } = null!;
    public DbSet<IngestionLog> IngestionLogs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Document>(entity =>
        {
            entity.ToTable("Documents");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.FilePath).IsRequired().HasMaxLength(500);
            entity.Property(e => e.ContentType).IsRequired();
            entity.Property(e => e.FileSize).IsRequired();
            entity.Property(e => e.ExtractedText).IsRequired();
            
            // Category fields
            entity.Property(e => e.SuggestedCategory);
            entity.Property(e => e.CategoryReasoning).HasMaxLength(2000);
            entity.Property(e => e.ActualCategory);
            
            // AI Tag Analysis Results
            entity.Property(e => e.AITagsJson).HasColumnType("nvarchar(max)");
            entity.Property(e => e.AIAnalysisDate).IsRequired(false);
            
            // Document metadata from processing
            entity.Property(e => e.PageCount).IsRequired(false);
            entity.Property(e => e.DetectedLanguage).IsRequired(false);
            entity.Property(e => e.ProcessingStatus).IsRequired(false);
            entity.Property(e => e.ProcessingError).IsRequired(false);
            
            // User notes
            entity.Property(e => e.Notes).IsRequired(false);
            
            // Visibility management
            entity.Property(e => e.Visibility).IsRequired();
            
            // Connector Source Tracking
            entity.Property(e => e.SourceConnectorId).IsRequired(false);
            entity.Property(e => e.SourceFilePath).HasMaxLength(1000).IsRequired(false);
            entity.Property(e => e.SourceFileHash).HasMaxLength(64).IsRequired(false);
            entity.Property(e => e.SourceLastModified).IsRequired(false);
            
            // Index on SourceConnectorId for performance
            entity.HasIndex(e => e.SourceConnectorId);
            
            // Ignore the legacy calculated property - use dual vector fields instead
            entity.Ignore(e => e.EmbeddingVector);
            
            // Configure dual vector fields - using varbinary(max) for EF Core 10 compatibility
            // EF Core 10 doesn't support SQL Server 2025's native VECTOR type yet
            // Need converters to serialize float[] to binary format
            var vector768Property = entity.Property(e => e.EmbeddingVector768)
                .HasColumnType("varbinary(max)")
                .HasConversion(GetFloatArrayToBinaryConverter())
                .IsRequired(false);
            vector768Property.Metadata.SetValueComparer(GetFloatArrayValueComparer());
            
            var vector1536Property = entity.Property(e => e.EmbeddingVector1536)
                .HasColumnType("varbinary(max)")
                .HasConversion(GetFloatArrayToBinaryConverter())
                .IsRequired(false);
            vector1536Property.Metadata.SetValueComparer(GetFloatArrayValueComparer());
            
            // Metadata
            entity.Property(e => e.UploadedAt).IsRequired();
            entity.Property(e => e.LastAccessedAt).IsRequired(false);
            entity.Property(e => e.AccessCount).IsRequired();
            
            // Owner
            entity.Property(e => e.OwnerId).IsRequired(false);
            
            // Multi-tenant support
            // TenantId is a foreign key but Tenant entity is in ApplicationDbContext
            // Ignore the navigation property to avoid cross-context relationship issues
            entity.Ignore(e => e.Tenant);
            entity.Ignore(e => e.Owner);  // ApplicationUser is also in ApplicationDbContext
            entity.Property(e => e.TenantId).IsRequired(false);
            
            // Ignore cross-context collection navigation properties
            // These entities (DocumentShare, DocumentGroupShare, DocumentTag) are in ApplicationDbContext
            entity.Ignore(e => e.Shares);
            entity.Ignore(e => e.GroupShares);
            entity.Ignore(e => e.Tags);
        });

        // DocumentChunk configuration
        modelBuilder.Entity<DocumentChunk>(entity =>
        {
            entity.ToTable("DocumentChunks");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ChunkText).IsRequired();
            
            // Ignore the legacy calculated property - use dual vector fields instead
            entity.Ignore(e => e.ChunkEmbedding);
            
            // Configure dual vector fields for chunk embeddings - using varbinary(max) for EF Core 10 compatibility
            // EF Core 10 doesn't support SQL Server 2025's native VECTOR type yet
            // Need converters to serialize float[] to binary format
            var chunkVector768Property = entity.Property(e => e.ChunkEmbedding768)
                .HasColumnType("varbinary(max)")
                .HasConversion(GetFloatArrayToBinaryConverter())
                .IsRequired(false);
            chunkVector768Property.Metadata.SetValueComparer(GetFloatArrayValueComparer());
            
            var chunkVector1536Property = entity.Property(e => e.ChunkEmbedding1536)
                .HasColumnType("varbinary(max)")
                .HasConversion(GetFloatArrayToBinaryConverter())
                .IsRequired(false);
            chunkVector1536Property.Metadata.SetValueComparer(GetFloatArrayValueComparer());
            
            entity.Property(e => e.TokenCount).IsRequired(false);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.StartPosition).IsRequired();
            entity.Property(e => e.EndPosition).IsRequired();
            
            // Relationship with Document
            entity.HasOne(e => e.Document)
                .WithMany()
                .HasForeignKey(e => e.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // Indexes for performance
            entity.HasIndex(e => e.DocumentId);
            entity.HasIndex(e => new { e.DocumentId, e.ChunkIndex });
        });

        // SimilarDocument configuration
        modelBuilder.Entity<SimilarDocument>(entity =>
        {
            entity.ToTable("SimilarDocuments");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SimilarityScore).IsRequired();
            entity.Property(e => e.RelevantChunk).HasMaxLength(1000).IsRequired(false);
            entity.Property(e => e.ChunkIndex).IsRequired(false);
            entity.Property(e => e.AnalyzedAt).IsRequired();
            entity.Property(e => e.Rank).IsRequired();
            
            // Relationship with source document
            entity.HasOne(e => e.SourceDocument)
                .WithMany()
                .HasForeignKey(e => e.SourceDocumentId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // Relationship with similar document (no cascade to avoid cycles)
            entity.HasOne(e => e.SimilarDocumentRef)
                .WithMany()
                .HasForeignKey(e => e.SimilarDocumentId)
                .OnDelete(DeleteBehavior.Restrict);
            
            // Indexes for performance
            entity.HasIndex(e => e.SourceDocumentId);
            entity.HasIndex(e => new { e.SourceDocumentId, e.Rank });
            entity.HasIndex(e => new { e.SourceDocumentId, e.SimilarityScore });
        });

        // LogEntry configuration
        modelBuilder.Entity<LogEntry>(entity =>
        {
            entity.ToTable("LogEntries");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Timestamp).IsRequired();
            entity.Property(e => e.Level).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Category).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Message).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.Details).HasColumnType("nvarchar(max)").IsRequired(false);
            entity.Property(e => e.UserId).HasMaxLength(450).IsRequired(false);
            entity.Property(e => e.FileName).HasMaxLength(500).IsRequired(false);
            entity.Property(e => e.StackTrace).HasColumnType("nvarchar(max)").IsRequired(false);
            
            // Indexes for performance
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => new { e.Category, e.Timestamp });
            entity.HasIndex(e => new { e.UserId, e.Timestamp });
        });

        // DocumentConnector configuration
        modelBuilder.Entity<DocumentConnector>(entity =>
        {
            entity.ToTable("DocumentConnectors");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.Property(e => e.ConnectorType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Configuration).IsRequired().HasColumnType("nvarchar(max)");
            entity.Property(e => e.EncryptedCredentials).HasColumnType("nvarchar(max)").IsRequired(false);
            entity.Property(e => e.IsActive).IsRequired();
            entity.Property(e => e.LastConnectionTestResult).HasMaxLength(500).IsRequired(false);
            entity.Property(e => e.OwnerId).HasMaxLength(450).IsRequired(false);
            entity.Property(e => e.Description).HasMaxLength(1000).IsRequired(false);
            
            // TenantId is a foreign key but Tenant entity is in ApplicationDbContext
            // Ignore the navigation property to avoid cross-context relationship issues
            entity.Ignore(e => e.Tenant);
            entity.Property(e => e.TenantId).IsRequired(false);
            
            // Configure the collection navigation property to IngestionSchedules
            // This is the inverse of the relationship defined in IngestionSchedule configuration
            entity.HasMany(e => e.IngestionSchedules)
                .WithOne(schedule => schedule.Connector)
                .HasForeignKey(schedule => schedule.ConnectorId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // Indexes for performance
            entity.HasIndex(e => e.OwnerId);
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => new { e.ConnectorType, e.IsActive });
        });

        // IngestionSchedule configuration
        modelBuilder.Entity<IngestionSchedule>(entity =>
        {
            entity.ToTable("IngestionSchedules");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.Property(e => e.ScheduleType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.CronExpression).HasMaxLength(100).IsRequired(false);
            entity.Property(e => e.DefaultCategory).HasMaxLength(255).IsRequired(false);
            entity.Property(e => e.FilterConfiguration).HasColumnType("nvarchar(max)").IsRequired(false);
            entity.Property(e => e.LastExecutionStatus).HasMaxLength(50).IsRequired(false);
            entity.Property(e => e.OwnerId).HasMaxLength(450).IsRequired(false);
            entity.Property(e => e.Description).HasMaxLength(1000).IsRequired(false);
            
            // Relationship with DocumentConnector is configured on the DocumentConnector side
            // to avoid duplicate configuration and ensure proper collection mapping
            
            // Indexes for performance
            entity.HasIndex(e => e.ConnectorId);
            entity.HasIndex(e => new { e.IsEnabled, e.NextExecutionAt });
            entity.HasIndex(e => e.OwnerId);
        });

        // IngestionLog configuration
        modelBuilder.Entity<IngestionLog>(entity =>
        {
            entity.ToTable("IngestionLogs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ErrorMessage).HasColumnType("nvarchar(max)").IsRequired(false);
            entity.Property(e => e.DetailedLog).HasColumnType("nvarchar(max)").IsRequired(false);
            entity.Property(e => e.TriggeredByUserId).HasMaxLength(450).IsRequired(false);
            
            // Relationship with IngestionSchedule
            entity.HasOne(e => e.IngestionSchedule)
                .WithMany(s => s.IngestionLogs)
                .HasForeignKey(e => e.IngestionScheduleId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
            
            // Indexes for performance
            entity.HasIndex(e => e.IngestionScheduleId);
            entity.HasIndex(e => new { e.StartedAt, e.Status });
            entity.HasIndex(e => e.TriggeredByUserId);
        });
    }
    
    /// <summary>
    /// Shared value converter for float array to JSON string for VECTOR columns
    /// </summary>
    private static ValueConverter<float[]?, string?> GetVectorConverter()
    {
        return new ValueConverter<float[]?, string?>(
            v => v == null ? null : System.Text.Json.JsonSerializer.Serialize(v),
            v => v == null ? null : System.Text.Json.JsonSerializer.Deserialize<float[]>(v) ?? Array.Empty<float>()
        );
    }
    
    /// <summary>
    /// Value converter for float array to binary format for varbinary(max) columns
    /// Serializes float[] to byte[] for database storage
    /// </summary>
    private static ValueConverter<float[]?, byte[]?> GetFloatArrayToBinaryConverter()
    {
        return new ValueConverter<float[]?, byte[]?>(
            // Convert float[] to byte[] for database
            v => v == null ? null : ConvertFloatArrayToBytes(v),
            // Convert byte[] back to float[]
            v => v == null ? null : ConvertBytesToFloatArray(v)
        );
    }
    
    /// <summary>
    /// Helper method to convert float array to byte array
    /// </summary>
    private static byte[] ConvertFloatArrayToBytes(float[] floats)
    {
        byte[] bytes = new byte[floats.Length * sizeof(float)];
        Buffer.BlockCopy(floats, 0, bytes, 0, bytes.Length);
        return bytes;
    }
    
    /// <summary>
    /// Helper method to convert byte array to float array
    /// </summary>
    private static float[] ConvertBytesToFloatArray(byte[] bytes)
    {
        float[] floats = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);
        return floats;
    }
    
    /// <summary>
    /// Value comparer for float arrays to properly track changes
    /// </summary>
    private static Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<float[]?> GetFloatArrayValueComparer()
    {
        return new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<float[]?>(
            (c1, c2) => (c1 == null && c2 == null) || (c1 != null && c2 != null && c1.SequenceEqual(c2)),
            c => c == null ? 0 : c.GetHashCode(),
            c => c == null ? null : c.ToArray()
        );
    }
}
