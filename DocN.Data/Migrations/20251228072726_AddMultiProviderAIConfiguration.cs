using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocN.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiProviderAIConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AzureOpenAIChatModel",
                table: "AIConfigurations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AzureOpenAIEmbeddingModel",
                table: "AIConfigurations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChatModelName",
                table: "AIConfigurations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ChatProvider",
                table: "AIConfigurations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ChunkOverlap",
                table: "AIConfigurations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ChunkSize",
                table: "AIConfigurations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "EmbeddingModelName",
                table: "AIConfigurations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmbeddingsProvider",
                table: "AIConfigurations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EnableChunking",
                table: "AIConfigurations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnableFallback",
                table: "AIConfigurations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "GeminiApiKey",
                table: "AIConfigurations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GeminiChatModel",
                table: "AIConfigurations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GeminiEmbeddingModel",
                table: "AIConfigurations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OpenAIApiKey",
                table: "AIConfigurations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OpenAIChatModel",
                table: "AIConfigurations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OpenAIEmbeddingModel",
                table: "AIConfigurations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderApiKey",
                table: "AIConfigurations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderEndpoint",
                table: "AIConfigurations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProviderType",
                table: "AIConfigurations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RAGProvider",
                table: "AIConfigurations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TagExtractionProvider",
                table: "AIConfigurations",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AzureOpenAIChatModel",
                table: "AIConfigurations");

            migrationBuilder.DropColumn(
                name: "AzureOpenAIEmbeddingModel",
                table: "AIConfigurations");

            migrationBuilder.DropColumn(
                name: "ChatModelName",
                table: "AIConfigurations");

            migrationBuilder.DropColumn(
                name: "ChatProvider",
                table: "AIConfigurations");

            migrationBuilder.DropColumn(
                name: "ChunkOverlap",
                table: "AIConfigurations");

            migrationBuilder.DropColumn(
                name: "ChunkSize",
                table: "AIConfigurations");

            migrationBuilder.DropColumn(
                name: "EmbeddingModelName",
                table: "AIConfigurations");

            migrationBuilder.DropColumn(
                name: "EmbeddingsProvider",
                table: "AIConfigurations");

            migrationBuilder.DropColumn(
                name: "EnableChunking",
                table: "AIConfigurations");

            migrationBuilder.DropColumn(
                name: "EnableFallback",
                table: "AIConfigurations");

            migrationBuilder.DropColumn(
                name: "GeminiApiKey",
                table: "AIConfigurations");

            migrationBuilder.DropColumn(
                name: "GeminiChatModel",
                table: "AIConfigurations");

            migrationBuilder.DropColumn(
                name: "GeminiEmbeddingModel",
                table: "AIConfigurations");

            migrationBuilder.DropColumn(
                name: "OpenAIApiKey",
                table: "AIConfigurations");

            migrationBuilder.DropColumn(
                name: "OpenAIChatModel",
                table: "AIConfigurations");

            migrationBuilder.DropColumn(
                name: "OpenAIEmbeddingModel",
                table: "AIConfigurations");

            migrationBuilder.DropColumn(
                name: "ProviderApiKey",
                table: "AIConfigurations");

            migrationBuilder.DropColumn(
                name: "ProviderEndpoint",
                table: "AIConfigurations");

            migrationBuilder.DropColumn(
                name: "ProviderType",
                table: "AIConfigurations");

            migrationBuilder.DropColumn(
                name: "RAGProvider",
                table: "AIConfigurations");

            migrationBuilder.DropColumn(
                name: "TagExtractionProvider",
                table: "AIConfigurations");
        }
    }
}
