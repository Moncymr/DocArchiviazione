using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocN.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMetadataExtractionProvider : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GroqApiKey",
                table: "AIConfigurations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GroqChatModel",
                table: "AIConfigurations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GroqEndpoint",
                table: "AIConfigurations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MetadataExtractionProvider",
                table: "AIConfigurations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OllamaChatModel",
                table: "AIConfigurations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OllamaEmbeddingModel",
                table: "AIConfigurations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OllamaEndpoint",
                table: "AIConfigurations",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GroqApiKey",
                table: "AIConfigurations");

            migrationBuilder.DropColumn(
                name: "GroqChatModel",
                table: "AIConfigurations");

            migrationBuilder.DropColumn(
                name: "GroqEndpoint",
                table: "AIConfigurations");

            migrationBuilder.DropColumn(
                name: "MetadataExtractionProvider",
                table: "AIConfigurations");

            migrationBuilder.DropColumn(
                name: "OllamaChatModel",
                table: "AIConfigurations");

            migrationBuilder.DropColumn(
                name: "OllamaEmbeddingModel",
                table: "AIConfigurations");

            migrationBuilder.DropColumn(
                name: "OllamaEndpoint",
                table: "AIConfigurations");
        }
    }
}
