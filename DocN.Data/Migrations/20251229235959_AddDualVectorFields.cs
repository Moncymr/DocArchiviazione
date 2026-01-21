using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocN.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDualVectorFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add new VECTOR fields for 768 and 1536 dimensions
            migrationBuilder.AddColumn<string>(
                name: "EmbeddingVector768",
                table: "Documents",
                type: "VECTOR(768)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmbeddingVector1536",
                table: "Documents",
                type: "VECTOR(1536)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChunkEmbedding768",
                table: "DocumentChunks",
                type: "VECTOR(768)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChunkEmbedding1536",
                table: "DocumentChunks",
                type: "VECTOR(1536)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmbeddingVector768",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "EmbeddingVector1536",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ChunkEmbedding768",
                table: "DocumentChunks");

            migrationBuilder.DropColumn(
                name: "ChunkEmbedding1536",
                table: "DocumentChunks");
        }
    }
}
