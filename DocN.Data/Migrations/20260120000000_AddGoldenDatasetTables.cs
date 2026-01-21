using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocN.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGoldenDatasetTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create GoldenDatasets table
            migrationBuilder.CreateTable(
                name: "GoldenDatasets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DatasetId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Version = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TenantId = table.Column<int>(type: "int", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoldenDatasets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoldenDatasets_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            // Create GoldenDatasetSamples table
            migrationBuilder.CreateTable(
                name: "GoldenDatasetSamples",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GoldenDatasetId = table.Column<int>(type: "int", nullable: false),
                    Query = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    GroundTruth = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    RelevantDocumentIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExpectedResponse = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DifficultyLevel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ImportanceWeight = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoldenDatasetSamples", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoldenDatasetSamples_GoldenDatasets_GoldenDatasetId",
                        column: x => x.GoldenDatasetId,
                        principalTable: "GoldenDatasets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Create GoldenDatasetEvaluationRecords table
            migrationBuilder.CreateTable(
                name: "GoldenDatasetEvaluationRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GoldenDatasetId = table.Column<int>(type: "int", nullable: false),
                    EvaluatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConfigurationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TotalSamples = table.Column<int>(type: "int", nullable: false),
                    EvaluatedSamples = table.Column<int>(type: "int", nullable: false),
                    FailedSamples = table.Column<int>(type: "int", nullable: false),
                    AverageFaithfulnessScore = table.Column<double>(type: "float", nullable: false),
                    AverageAnswerRelevancyScore = table.Column<double>(type: "float", nullable: false),
                    AverageContextPrecisionScore = table.Column<double>(type: "float", nullable: false),
                    AverageContextRecallScore = table.Column<double>(type: "float", nullable: false),
                    OverallRAGASScore = table.Column<double>(type: "float", nullable: false),
                    AverageConfidenceScore = table.Column<double>(type: "float", nullable: false),
                    LowConfidenceRate = table.Column<double>(type: "float", nullable: false),
                    HallucinationRate = table.Column<double>(type: "float", nullable: false),
                    CitationVerificationRate = table.Column<double>(type: "float", nullable: false),
                    DetailedResultsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FailedSampleIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DurationSeconds = table.Column<double>(type: "float", nullable: false),
                    TenantId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoldenDatasetEvaluationRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoldenDatasetEvaluationRecords_GoldenDatasets_GoldenDatasetId",
                        column: x => x.GoldenDatasetId,
                        principalTable: "GoldenDatasets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GoldenDatasetEvaluationRecords_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            // Create indexes for GoldenDatasets
            migrationBuilder.CreateIndex(
                name: "IX_GoldenDatasets_DatasetId",
                table: "GoldenDatasets",
                column: "DatasetId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoldenDatasets_TenantId",
                table: "GoldenDatasets",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_GoldenDatasets_IsActive",
                table: "GoldenDatasets",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_GoldenDatasets_TenantId_IsActive",
                table: "GoldenDatasets",
                columns: new[] { "TenantId", "IsActive" });

            // Create indexes for GoldenDatasetSamples
            migrationBuilder.CreateIndex(
                name: "IX_GoldenDatasetSamples_GoldenDatasetId",
                table: "GoldenDatasetSamples",
                column: "GoldenDatasetId");

            migrationBuilder.CreateIndex(
                name: "IX_GoldenDatasetSamples_Category",
                table: "GoldenDatasetSamples",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_GoldenDatasetSamples_IsActive",
                table: "GoldenDatasetSamples",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_GoldenDatasetSamples_GoldenDatasetId_IsActive",
                table: "GoldenDatasetSamples",
                columns: new[] { "GoldenDatasetId", "IsActive" });

            // Create indexes for GoldenDatasetEvaluationRecords
            migrationBuilder.CreateIndex(
                name: "IX_GoldenDatasetEvaluationRecords_GoldenDatasetId",
                table: "GoldenDatasetEvaluationRecords",
                column: "GoldenDatasetId");

            migrationBuilder.CreateIndex(
                name: "IX_GoldenDatasetEvaluationRecords_EvaluatedAt",
                table: "GoldenDatasetEvaluationRecords",
                column: "EvaluatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_GoldenDatasetEvaluationRecords_ConfigurationId",
                table: "GoldenDatasetEvaluationRecords",
                column: "ConfigurationId");

            migrationBuilder.CreateIndex(
                name: "IX_GoldenDatasetEvaluationRecords_TenantId",
                table: "GoldenDatasetEvaluationRecords",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_GoldenDatasetEvaluationRecords_GoldenDatasetId_EvaluatedAt",
                table: "GoldenDatasetEvaluationRecords",
                columns: new[] { "GoldenDatasetId", "EvaluatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "GoldenDatasetEvaluationRecords");
            migrationBuilder.DropTable(name: "GoldenDatasetSamples");
            migrationBuilder.DropTable(name: "GoldenDatasets");
        }
    }
}
