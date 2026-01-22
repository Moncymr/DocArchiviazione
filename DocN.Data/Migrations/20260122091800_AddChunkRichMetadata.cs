using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocN.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddChunkRichMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SectionTitle",
                table: "DocumentChunks",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SectionPath",
                table: "DocumentChunks",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KeywordsJson",
                table: "DocumentChunks",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DocumentType",
                table: "DocumentChunks",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HeaderLevel",
                table: "DocumentChunks",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ChunkType",
                table: "DocumentChunks",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "paragraph");

            migrationBuilder.AddColumn<bool>(
                name: "IsListItem",
                table: "DocumentChunks",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CustomMetadataJson",
                table: "DocumentChunks",
                type: "nvarchar(max)",
                nullable: true);

            // Add indexes for better query performance
            migrationBuilder.CreateIndex(
                name: "IX_DocumentChunks_SectionTitle",
                table: "DocumentChunks",
                column: "SectionTitle");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentChunks_DocumentType",
                table: "DocumentChunks",
                column: "DocumentType");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentChunks_ChunkType",
                table: "DocumentChunks",
                column: "ChunkType");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentChunks_HeaderLevel",
                table: "DocumentChunks",
                column: "HeaderLevel");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DocumentChunks_SectionTitle",
                table: "DocumentChunks");

            migrationBuilder.DropIndex(
                name: "IX_DocumentChunks_DocumentType",
                table: "DocumentChunks");

            migrationBuilder.DropIndex(
                name: "IX_DocumentChunks_ChunkType",
                table: "DocumentChunks");

            migrationBuilder.DropIndex(
                name: "IX_DocumentChunks_HeaderLevel",
                table: "DocumentChunks");

            migrationBuilder.DropColumn(
                name: "SectionTitle",
                table: "DocumentChunks");

            migrationBuilder.DropColumn(
                name: "SectionPath",
                table: "DocumentChunks");

            migrationBuilder.DropColumn(
                name: "KeywordsJson",
                table: "DocumentChunks");

            migrationBuilder.DropColumn(
                name: "DocumentType",
                table: "DocumentChunks");

            migrationBuilder.DropColumn(
                name: "HeaderLevel",
                table: "DocumentChunks");

            migrationBuilder.DropColumn(
                name: "ChunkType",
                table: "DocumentChunks");

            migrationBuilder.DropColumn(
                name: "IsListItem",
                table: "DocumentChunks");

            migrationBuilder.DropColumn(
                name: "CustomMetadataJson",
                table: "DocumentChunks");
        }
    }
}
