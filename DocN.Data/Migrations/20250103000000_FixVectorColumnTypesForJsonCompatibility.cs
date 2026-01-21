using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocN.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixVectorColumnTypesForJsonCompatibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // This migration ensures columns can accept JSON format data for VECTOR compatibility
            // If columns are already VECTOR(1536), no changes are needed - they accept JSON format
            // If columns are varbinary(max), they need to be changed to nvarchar(max) to accept JSON
            
            // Note: This migration is safe to run even if columns are already VECTOR(1536)
            // SQL Server will accept JSON string data in VECTOR columns
            
            // Only alter if the column is NOT already a VECTOR type
            // Since we can't easily check column type in migration, we use a conditional approach
            migrationBuilder.Sql(@"
                -- Check and alter Documents.EmbeddingVector if needed
                IF EXISTS (
                    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_NAME = 'Documents' 
                    AND COLUMN_NAME = 'EmbeddingVector' 
                    AND DATA_TYPE = 'varbinary'
                )
                BEGIN
                    ALTER TABLE Documents ALTER COLUMN EmbeddingVector NVARCHAR(MAX) NULL;
                END
            ");

            migrationBuilder.Sql(@"
                -- Check and alter DocumentChunks.ChunkEmbedding if needed
                IF EXISTS (
                    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_NAME = 'DocumentChunks' 
                    AND COLUMN_NAME = 'ChunkEmbedding' 
                    AND DATA_TYPE = 'varbinary'
                )
                BEGIN
                    ALTER TABLE DocumentChunks ALTER COLUMN ChunkEmbedding NVARCHAR(MAX) NULL;
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert to varbinary(max) if needed
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_NAME = 'Documents' 
                    AND COLUMN_NAME = 'EmbeddingVector' 
                    AND DATA_TYPE = 'nvarchar'
                )
                BEGIN
                    ALTER TABLE Documents ALTER COLUMN EmbeddingVector VARBINARY(MAX) NULL;
                END
            ");

            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_NAME = 'DocumentChunks' 
                    AND COLUMN_NAME = 'ChunkEmbedding' 
                    AND DATA_TYPE = 'nvarchar'
                )
                BEGIN
                    ALTER TABLE DocumentChunks ALTER COLUMN ChunkEmbedding VARBINARY(MAX) NULL;
                END
            ");
        }
    }
}
