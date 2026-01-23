using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocN.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVectorIndexOptimizations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQL Server 2025 Vector Optimization Strategy:
            // 1. Columnstore indexes for compressed storage and fast scans
            // 2. Filtered indexes to optimize common query patterns
            // 3. Covering indexes for frequently accessed columns alongside vectors
            
            // ===========================================================================
            // DOCUMENTS TABLE VECTOR OPTIMIZATIONS
            // ===========================================================================
            
            // Columnstore index for Document vectors with 768 dimensions
            // This provides optimal compression and scan performance for vector operations
            // Filtered to only include rows with non-null vectors to save space
            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED COLUMNSTORE INDEX IX_Documents_VectorColumnstore768
                ON Documents (EmbeddingVector768, Id, FileName, ActualCategory, OwnerId, TenantId)
                WHERE EmbeddingVector768 IS NOT NULL;
            ");

            // Columnstore index for Document vectors with 1536 dimensions
            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED COLUMNSTORE INDEX IX_Documents_VectorColumnstore1536
                ON Documents (EmbeddingVector1536, Id, FileName, ActualCategory, OwnerId, TenantId)
                WHERE EmbeddingVector1536 IS NOT NULL;
            ");
            
            // Composite index for filtering by owner + vector existence
            // Optimizes queries that filter by OwnerId before vector search
            migrationBuilder.CreateIndex(
                name: "IX_Documents_OwnerId_Vector768",
                table: "Documents",
                columns: new[] { "OwnerId", "Id" },
                filter: "[OwnerId] IS NOT NULL AND [EmbeddingVector768] IS NOT NULL");
                
            migrationBuilder.CreateIndex(
                name: "IX_Documents_OwnerId_Vector1536",
                table: "Documents",
                columns: new[] { "OwnerId", "Id" },
                filter: "[OwnerId] IS NOT NULL AND [EmbeddingVector1536] IS NOT NULL");
            
            // Composite index for filtering by tenant + vector existence
            // Optimizes multi-tenant deployments with tenant isolation
            migrationBuilder.CreateIndex(
                name: "IX_Documents_TenantId_Vector768",
                table: "Documents",
                columns: new[] { "TenantId", "Id" },
                filter: "[TenantId] IS NOT NULL AND [EmbeddingVector768] IS NOT NULL");
                
            migrationBuilder.CreateIndex(
                name: "IX_Documents_TenantId_Vector1536",
                table: "Documents",
                columns: new[] { "TenantId", "Id" },
                filter: "[TenantId] IS NOT NULL AND [EmbeddingVector1536] IS NOT NULL");

            // ===========================================================================
            // DOCUMENT CHUNKS TABLE VECTOR OPTIMIZATIONS
            // ===========================================================================
            
            // Columnstore index for DocumentChunk vectors with 768 dimensions
            // This is critical for chunk-level search performance at scale
            // Includes DocumentId and ChunkIndex for fast joining back to documents
            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED COLUMNSTORE INDEX IX_DocumentChunks_VectorColumnstore768
                ON DocumentChunks (ChunkEmbedding768, Id, DocumentId, ChunkIndex, ChunkText)
                WHERE ChunkEmbedding768 IS NOT NULL;
            ");

            // Columnstore index for DocumentChunk vectors with 1536 dimensions
            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED COLUMNSTORE INDEX IX_DocumentChunks_VectorColumnstore1536
                ON DocumentChunks (ChunkEmbedding1536, Id, DocumentId, ChunkIndex, ChunkText)
                WHERE ChunkEmbedding1536 IS NOT NULL;
            ");
            
            // Composite index for chunk search filtered by document owner
            // This joins DocumentChunks with Documents on DocumentId efficiently
            // Note: Uses DocumentId to join, actual owner filtering happens at query time
            migrationBuilder.CreateIndex(
                name: "IX_DocumentChunks_DocumentId_Vector768",
                table: "DocumentChunks",
                columns: new[] { "DocumentId", "Id", "ChunkIndex" },
                filter: "[ChunkEmbedding768] IS NOT NULL");
                
            migrationBuilder.CreateIndex(
                name: "IX_DocumentChunks_DocumentId_Vector1536",
                table: "DocumentChunks",
                columns: new[] { "DocumentId", "Id", "ChunkIndex" },
                filter: "[ChunkEmbedding1536] IS NOT NULL");

            // Index to optimize finding chunks with embeddings (for batch processing)
            migrationBuilder.CreateIndex(
                name: "IX_DocumentChunks_EmbeddingDimension",
                table: "DocumentChunks",
                columns: new[] { "EmbeddingDimension", "DocumentId", "ChunkIndex" },
                filter: "[EmbeddingDimension] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop all indexes in reverse order
            
            // DocumentChunks indexes
            migrationBuilder.DropIndex(
                name: "IX_DocumentChunks_EmbeddingDimension",
                table: "DocumentChunks");
            
            migrationBuilder.DropIndex(
                name: "IX_DocumentChunks_DocumentId_Vector1536",
                table: "DocumentChunks");
                
            migrationBuilder.DropIndex(
                name: "IX_DocumentChunks_DocumentId_Vector768",
                table: "DocumentChunks");
            
            migrationBuilder.Sql("DROP INDEX IF EXISTS IX_DocumentChunks_VectorColumnstore1536 ON DocumentChunks;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS IX_DocumentChunks_VectorColumnstore768 ON DocumentChunks;");
            
            // Documents indexes
            migrationBuilder.DropIndex(
                name: "IX_Documents_TenantId_Vector1536",
                table: "Documents");
                
            migrationBuilder.DropIndex(
                name: "IX_Documents_TenantId_Vector768",
                table: "Documents");
            
            migrationBuilder.DropIndex(
                name: "IX_Documents_OwnerId_Vector1536",
                table: "Documents");
                
            migrationBuilder.DropIndex(
                name: "IX_Documents_OwnerId_Vector768",
                table: "Documents");
            
            migrationBuilder.Sql("DROP INDEX IF EXISTS IX_Documents_VectorColumnstore1536 ON Documents;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS IX_Documents_VectorColumnstore768 ON Documents;");
        }
    }
}
