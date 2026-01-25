using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using DocN.Data.Models;

namespace DocN.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Tenant> Tenants { get; set; } = null!;
    public DbSet<Document> Documents { get; set; } = null!;
    public DbSet<DocumentChunk> DocumentChunks { get; set; } = null!;
    public DbSet<DocumentShare> DocumentShares { get; set; } = null!;
    public DbSet<DocumentGroupShare> DocumentGroupShares { get; set; } = null!;
    public DbSet<DocumentTag> DocumentTags { get; set; } = null!;
    public DbSet<SimilarDocument> SimilarDocuments { get; set; } = null!;
    public DbSet<UserGroup> UserGroups { get; set; } = null!;
    public DbSet<UserGroupMember> UserGroupMembers { get; set; } = null!;
    public DbSet<AIConfiguration> AIConfigurations { get; set; } = null!;
    
    // Conversazioni e messaggi per RAG
    public DbSet<Conversation> Conversations { get; set; } = null!;
    public DbSet<Message> Messages { get; set; } = null!;
    
    // Agent configuration and templates
    public DbSet<AgentConfiguration> AgentConfigurations { get; set; } = null!;
    public DbSet<AgentTemplate> AgentTemplates { get; set; } = null!;
    public DbSet<AgentUsageLog> AgentUsageLogs { get; set; } = null!;
    
    // Audit logs for GDPR/SOC2 compliance
    public DbSet<AuditLog> AuditLogs { get; set; }
    
    // Application logs for debugging and monitoring
    public DbSet<LogEntry> LogEntries { get; set; }
    
    // Golden datasets for RAG quality regression testing
    public DbSet<GoldenDataset> GoldenDatasets { get; set; } = null!;
    public DbSet<GoldenDatasetSample> GoldenDatasetSamples { get; set; } = null!;
    public DbSet<GoldenDatasetEvaluationRecord> GoldenDatasetEvaluationRecords { get; set; } = null!;
    
    // Dashboard and personalization
    public DbSet<DashboardWidget> DashboardWidgets { get; set; } = null!;
    public DbSet<SavedSearch> SavedSearches { get; set; } = null!;
    public DbSet<UserActivity> UserActivities { get; set; } = null!;
    
    // Response feedback for RAG quality improvement
    public DbSet<ResponseFeedback> ResponseFeedbacks { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Tenant configuration
        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.HasIndex(e => e.Name);
        });

        // ApplicationUser - Tenant relationship
        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.HasOne(e => e.Tenant)
                .WithMany(t => t.Users)
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.SetNull);
            
            entity.HasIndex(e => e.TenantId);
        });

        // Document configuration
        modelBuilder.Entity<Document>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.FilePath).IsRequired().HasMaxLength(500);
            entity.Property(e => e.ExtractedText).HasMaxLength(int.MaxValue);
            entity.Property(e => e.CategoryReasoning).HasMaxLength(2000);
            
            // AI Tags JSON field
            entity.Property(e => e.AITagsJson).HasColumnType("nvarchar(max)");
            
            // Configure vector columns as nvarchar(max) with JSON serialization
            // SQL Server 2025 VECTOR type expects JSON array format: [0.1, 0.2, 0.3, ...]
            // Using nvarchar(max) with JSON serialization is compatible with VECTOR type
            
            // 768-dimensional vector for Gemini and similar providers
            var vector768Property = entity.Property(e => e.EmbeddingVector768)
                .HasColumnType("nvarchar(max)")
                .HasConversion(GetFloatArrayToJsonConverter())
                .IsRequired(false);
            vector768Property.Metadata.SetValueComparer(new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<float[]?>(
                (c1, c2) => (c1 == null && c2 == null) || (c1 != null && c2 != null && c1.SequenceEqual(c2)),
                c => c == null ? 0 : c.GetHashCode(),
                c => c == null ? null : c.ToArray()));
            
            // 1536-dimensional vector for OpenAI and similar providers
            var vector1536Property = entity.Property(e => e.EmbeddingVector1536)
                .HasColumnType("nvarchar(max)")
                .HasConversion(GetFloatArrayToJsonConverter())
                .IsRequired(false);
            vector1536Property.Metadata.SetValueComparer(new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<float[]?>(
                (c1, c2) => (c1 == null && c2 == null) || (c1 != null && c2 != null && c1.SequenceEqual(c2)),
                c => c == null ? 0 : c.GetHashCode(),
                c => c == null ? null : c.ToArray()));
            
            // Configure EmbeddingDimension to track which field is used
            entity.Property(e => e.EmbeddingDimension)
                .IsRequired(false);
            
            // Ignore the calculated property EmbeddingVector (it's not mapped to database)
            entity.Ignore(e => e.EmbeddingVector);
            
            // Index for performance with large number of documents
            entity.HasIndex(e => e.OwnerId);
            entity.HasIndex(e => e.UploadedAt);
            entity.HasIndex(e => e.Visibility);
            entity.HasIndex(e => e.SuggestedCategory);
            entity.HasIndex(e => e.ActualCategory);
            entity.HasIndex(e => e.TenantId);
            
            // Relationship with owner (optional - documents can exist without owner)
            entity.HasOne(e => e.Owner)
                .WithMany(u => u.Documents)
                .HasForeignKey(e => e.OwnerId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
            
            // Relationship with tenant
            entity.HasOne(e => e.Tenant)
                .WithMany(t => t.Documents)
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // DocumentShare configuration
        modelBuilder.Entity<DocumentShare>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            entity.HasOne(e => e.Document)
                .WithMany(d => d.Shares)
                .HasForeignKey(e => e.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.SharedWithUser)
                .WithMany(u => u.SharedDocuments)
                .HasForeignKey(e => e.SharedWithUserId)
                .OnDelete(DeleteBehavior.Restrict);
            
            // Index for performance
            entity.HasIndex(e => e.SharedWithUserId);
        });

        // UserGroup configuration
        modelBuilder.Entity<UserGroup>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            
            entity.HasOne(e => e.Owner)
                .WithMany(u => u.OwnedGroups)
                .HasForeignKey(e => e.OwnerId)
                .OnDelete(DeleteBehavior.SetNull);
            
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.SetNull);
            
            // Index for performance
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.TenantId);
        });

        // UserGroupMember configuration
        modelBuilder.Entity<UserGroupMember>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            entity.HasOne(e => e.Group)
                .WithMany(g => g.Members)
                .HasForeignKey(e => e.GroupId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.User)
                .WithMany(u => u.GroupMemberships)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // Ensure unique membership (one user can't be in the same group twice)
            entity.HasIndex(e => new { e.GroupId, e.UserId }).IsUnique();
            entity.HasIndex(e => e.UserId);
        });

        // DocumentGroupShare configuration
        modelBuilder.Entity<DocumentGroupShare>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            entity.HasOne(e => e.Document)
                .WithMany(d => d.GroupShares)
                .HasForeignKey(e => e.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.Group)
                .WithMany(g => g.DocumentShares)
                .HasForeignKey(e => e.GroupId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // Ensure unique share (one document can't be shared with the same group twice)
            entity.HasIndex(e => new { e.DocumentId, e.GroupId }).IsUnique();
            entity.HasIndex(e => e.GroupId);
        });

        // DocumentTag configuration
        modelBuilder.Entity<DocumentTag>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
            
            entity.HasOne(e => e.Document)
                .WithMany(d => d.Tags)
                .HasForeignKey(e => e.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // Index for searching by tags
            entity.HasIndex(e => e.Name);
        });

        // AIConfiguration
        modelBuilder.Entity<AIConfiguration>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ConfigurationName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.AzureOpenAIKey).HasMaxLength(500);
            entity.Property(e => e.SystemPrompt).HasMaxLength(2000);
        });

        // Conversation configuration
        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.Tags).HasMaxLength(500);
            
            // Relazione con User
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // Indici per performance
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.LastMessageAt);
            entity.HasIndex(e => new { e.UserId, e.IsArchived, e.LastMessageAt });
        });

        // Message configuration
        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Role).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Content).IsRequired();
            
            // Relazione con Conversation
            entity.HasOne(e => e.Conversation)
                .WithMany(c => c.Messages)
                .HasForeignKey(e => e.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // Configura ReferencedDocumentIdsJson come stringa JSON
            // La proprietà ReferencedDocumentIds usa [NotMapped] e gestisce la serializzazione/deserializzazione
            entity.Property(e => e.ReferencedDocumentIdsJson)
                .HasColumnName("ReferencedDocumentIds")
                .HasColumnType("nvarchar(max)")
                .IsRequired(false);            
            // Indici per performance
            entity.HasIndex(e => e.ConversationId);
            entity.HasIndex(e => e.Timestamp);
        });

        // DocumentChunk configuration
        modelBuilder.Entity<DocumentChunk>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ChunkText).IsRequired();
            
            // Configure vector columns for chunk embeddings as nvarchar(max) with JSON serialization
            // SQL Server 2025 VECTOR type expects JSON array format: [0.1, 0.2, 0.3, ...]
            // Using nvarchar(max) with JSON serialization is compatible with VECTOR type
            
            // 768-dimensional vector for Gemini and similar providers
            var chunkVector768Property = entity.Property(e => e.ChunkEmbedding768)
                .HasColumnType("nvarchar(max)")
                .HasConversion(GetFloatArrayToJsonConverter())
                .IsRequired(false);
            chunkVector768Property.Metadata.SetValueComparer(new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<float[]?>(
                (c1, c2) => (c1 == null && c2 == null) || (c1 != null && c2 != null && c1.SequenceEqual(c2)),
                c => c == null ? 0 : c.GetHashCode(),
                c => c == null ? null : c.ToArray()));
            
            // 1536-dimensional vector for OpenAI and similar providers
            var chunkVector1536Property = entity.Property(e => e.ChunkEmbedding1536)
                .HasColumnType("nvarchar(max)")
                .HasConversion(GetFloatArrayToJsonConverter())
                .IsRequired(false);
            chunkVector1536Property.Metadata.SetValueComparer(new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<float[]?>(
                (c1, c2) => (c1 == null && c2 == null) || (c1 != null && c2 != null && c1.SequenceEqual(c2)),
                c => c == null ? 0 : c.GetHashCode(),
                c => c == null ? null : c.ToArray()));
            
            // Configure EmbeddingDimension to track which field is used
            entity.Property(e => e.EmbeddingDimension)
                .IsRequired(false);
            
            // Ignore the calculated property ChunkEmbedding (it's not mapped to database)
            entity.Ignore(e => e.ChunkEmbedding);
            
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
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SimilarityScore).IsRequired();
            entity.Property(e => e.RelevantChunk).HasMaxLength(1000);
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

        // AgentConfiguration configuration
        modelBuilder.Entity<AgentConfiguration>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.SystemPrompt).IsRequired();
            entity.Property(e => e.CustomInstructions).HasMaxLength(2000);
            entity.Property(e => e.CategoryFilter).HasMaxLength(1000);
            entity.Property(e => e.TagFilter).HasMaxLength(1000);
            
            // Relationships
            entity.HasOne(e => e.Owner)
                .WithMany()
                .HasForeignKey(e => e.OwnerId)
                .OnDelete(DeleteBehavior.SetNull);
            
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.SetNull);
            
            entity.HasOne(e => e.Template)
                .WithMany(t => t.Agents)
                .HasForeignKey(e => e.TemplateId)
                .OnDelete(DeleteBehavior.SetNull);
            
            // Indexes
            entity.HasIndex(e => e.OwnerId);
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => e.AgentType);
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => new { e.TenantId, e.IsActive });
        });

        // AgentTemplate configuration
        modelBuilder.Entity<AgentTemplate>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Category).HasMaxLength(100);
            entity.Property(e => e.DefaultSystemPrompt).IsRequired();
            entity.Property(e => e.DefaultParametersJson).HasMaxLength(4000);
            
            // Relationships
            entity.HasOne(e => e.Owner)
                .WithMany()
                .HasForeignKey(e => e.OwnerId)
                .OnDelete(DeleteBehavior.SetNull);
            
            // Indexes
            entity.HasIndex(e => e.Category);
            entity.HasIndex(e => e.AgentType);
            entity.HasIndex(e => e.IsBuiltIn);
            entity.HasIndex(e => e.IsActive);
        });

        // AgentUsageLog configuration
        modelBuilder.Entity<AgentUsageLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Query).IsRequired();
            entity.Property(e => e.Response).HasMaxLength(int.MaxValue);
            
            // Relationships
            entity.HasOne(e => e.AgentConfiguration)
                .WithMany()
                .HasForeignKey(e => e.AgentConfigurationId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.SetNull);
            
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.SetNull);
            
            // Indexes for analytics
            entity.HasIndex(e => e.AgentConfigurationId);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => new { e.AgentConfigurationId, e.CreatedAt });
        });

        // AuditLog configuration
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Action).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ResourceType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ResourceId).HasMaxLength(100);
            entity.Property(e => e.Username).HasMaxLength(256);
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.Property(e => e.UserAgent).HasMaxLength(500);
            entity.Property(e => e.Severity).HasMaxLength(20);
            entity.Property(e => e.ErrorMessage).HasMaxLength(1000);
            entity.Property(e => e.Details).HasColumnType("nvarchar(max)");
            
            // Relationships
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.SetNull);
            
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.SetNull);
            
            // Indexes for querying and compliance reporting
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Action);
            entity.HasIndex(e => e.ResourceType);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => new { e.UserId, e.Timestamp });
            entity.HasIndex(e => new { e.Action, e.Timestamp });
            entity.HasIndex(e => new { e.ResourceType, e.ResourceId });
        });
        
        // LogEntry configuration
        modelBuilder.Entity<LogEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Timestamp).IsRequired();
            entity.Property(e => e.Level).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Category).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Message).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.Details).HasColumnType("nvarchar(max)");
            entity.Property(e => e.UserId).HasMaxLength(450);
            entity.Property(e => e.FileName).HasMaxLength(500);
            entity.Property(e => e.StackTrace).HasColumnType("nvarchar(max)");
            
            // Indexes for efficient querying
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => new { e.Category, e.Timestamp });
            entity.HasIndex(e => new { e.UserId, e.Timestamp });
        });
        
        // GoldenDataset configuration
        modelBuilder.Entity<GoldenDataset>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DatasetId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Version).IsRequired().HasMaxLength(20);
            entity.Property(e => e.CreatedBy).HasMaxLength(256);
            entity.Property(e => e.MetadataJson).HasColumnType("nvarchar(max)");
            
            // Unique constraint on DatasetId
            entity.HasIndex(e => e.DatasetId).IsUnique();
            
            // Relationship with tenant
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.SetNull);
            
            // Indexes
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => new { e.TenantId, e.IsActive });
        });
        
        // GoldenDatasetSample configuration
        modelBuilder.Entity<GoldenDatasetSample>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Query).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.GroundTruth).IsRequired().HasMaxLength(4000);
            entity.Property(e => e.RelevantDocumentIdsJson).HasColumnType("nvarchar(max)");
            entity.Property(e => e.ExpectedResponse).HasMaxLength(4000);
            entity.Property(e => e.Category).HasMaxLength(100);
            entity.Property(e => e.DifficultyLevel).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.CreatedBy).HasMaxLength(256);
            
            // Relationship with GoldenDataset
            entity.HasOne(e => e.GoldenDataset)
                .WithMany(d => d.Samples)
                .HasForeignKey(e => e.GoldenDatasetId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // Indexes
            entity.HasIndex(e => e.GoldenDatasetId);
            entity.HasIndex(e => e.Category);
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => new { e.GoldenDatasetId, e.IsActive });
        });
        
        // GoldenDatasetEvaluationRecord configuration
        modelBuilder.Entity<GoldenDatasetEvaluationRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ConfigurationId).HasMaxLength(100);
            entity.Property(e => e.DetailedResultsJson).HasColumnType("nvarchar(max)");
            entity.Property(e => e.FailedSampleIdsJson).HasColumnType("nvarchar(max)");
            entity.Property(e => e.Status).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Notes).HasMaxLength(2000);
            
            // Relationship with GoldenDataset
            entity.HasOne(e => e.GoldenDataset)
                .WithMany(d => d.EvaluationRecords)
                .HasForeignKey(e => e.GoldenDatasetId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // Relationship with tenant
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.SetNull);
            
            // Indexes
            entity.HasIndex(e => e.GoldenDatasetId);
            entity.HasIndex(e => e.EvaluatedAt);
            entity.HasIndex(e => e.ConfigurationId);
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => new { e.GoldenDatasetId, e.EvaluatedAt });
        });
        
        // DashboardWidget configuration
        modelBuilder.Entity<DashboardWidget>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.WidgetType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Configuration).HasColumnType("nvarchar(max)");
            
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.UserId, e.Position });
        });
        
        // SavedSearch configuration
        modelBuilder.Entity<SavedSearch>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Query).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.Filters).HasColumnType("nvarchar(max)");
            entity.Property(e => e.SearchType).IsRequired().HasMaxLength(20);
            
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.UserId, e.LastUsedAt });
        });
        
        // UserActivity configuration
        modelBuilder.Entity<UserActivity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ActivityType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Description).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Metadata).HasColumnType("nvarchar(max)");
            
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(e => e.Document)
                .WithMany()
                .HasForeignKey(e => e.DocumentId)
                .OnDelete(DeleteBehavior.SetNull);
            
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => new { e.UserId, e.CreatedAt });
        });
    }
    
    // Helper methods for VECTOR type conversion using JSON serialization
    // SQL Server VECTOR type expects JSON array format: [0.1, 0.2, 0.3, ...]
    private static ValueConverter<float[]?, string?> GetFloatArrayToJsonConverter()
    {
        return new ValueConverter<float[]?, string?>(
            v => v == null ? null : System.Text.Json.JsonSerializer.Serialize(v),
            v => v == null ? null : System.Text.Json.JsonSerializer.Deserialize<float[]>(v) ?? Array.Empty<float>()
        );
    }
}
