using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocN.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSimilarDocumentsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SimilarDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceDocumentId = table.Column<int>(type: "int", nullable: false),
                    SimilarDocumentId = table.Column<int>(type: "int", nullable: false),
                    SimilarityScore = table.Column<double>(type: "float", nullable: false),
                    RelevantChunk = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ChunkIndex = table.Column<int>(type: "int", nullable: true),
                    AnalyzedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Rank = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SimilarDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SimilarDocuments_Documents_SimilarDocumentId",
                        column: x => x.SimilarDocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SimilarDocuments_Documents_SourceDocumentId",
                        column: x => x.SourceDocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SimilarDocuments_SimilarDocumentId",
                table: "SimilarDocuments",
                column: "SimilarDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_SimilarDocuments_SourceDocumentId",
                table: "SimilarDocuments",
                column: "SourceDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_SimilarDocuments_SourceDocumentId_Rank",
                table: "SimilarDocuments",
                columns: new[] { "SourceDocumentId", "Rank" });

            migrationBuilder.CreateIndex(
                name: "IX_SimilarDocuments_SourceDocumentId_SimilarityScore",
                table: "SimilarDocuments",
                columns: new[] { "SourceDocumentId", "SimilarityScore" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SimilarDocuments");
        }
    }
}
