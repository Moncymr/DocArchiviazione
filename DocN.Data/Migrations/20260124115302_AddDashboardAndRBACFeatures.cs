using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocN.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDashboardAndRBACFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ErrorDetailsJson",
                table: "Documents",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ErrorMessage",
                table: "Documents",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ErrorType",
                table: "Documents",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRetryable",
                table: "Documents",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastRetryAt",
                table: "Documents",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxRetries",
                table: "Documents",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextRetryAt",
                table: "Documents",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousWorkflowState",
                table: "Documents",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetryCount",
                table: "Documents",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SourceConnectorId",
                table: "Documents",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceFileHash",
                table: "Documents",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceFilePath",
                table: "Documents",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SourceLastModified",
                table: "Documents",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StateEnteredAt",
                table: "Documents",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkflowState",
                table: "Documents",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChunkType",
                table: "DocumentChunks",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ImportanceScore",
                table: "DocumentChunks",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KeywordsJson",
                table: "DocumentChunks",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Section",
                table: "DocumentChunks",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "DocumentChunks",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DashboardWidgets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    WidgetType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    Configuration = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsVisible = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DashboardWidgets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DashboardWidgets_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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

            migrationBuilder.CreateTable(
                name: "SavedSearches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Query = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Filters = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SearchType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UseCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedSearches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavedSearches_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserActivities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ActivityType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DocumentId = table.Column<int>(type: "int", nullable: true),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserActivities_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserActivities_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

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

            migrationBuilder.CreateIndex(
                name: "IX_DashboardWidgets_UserId",
                table: "DashboardWidgets",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_DashboardWidgets_UserId_Position",
                table: "DashboardWidgets",
                columns: new[] { "UserId", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_GoldenDatasetEvaluationRecords_ConfigurationId",
                table: "GoldenDatasetEvaluationRecords",
                column: "ConfigurationId");

            migrationBuilder.CreateIndex(
                name: "IX_GoldenDatasetEvaluationRecords_EvaluatedAt",
                table: "GoldenDatasetEvaluationRecords",
                column: "EvaluatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_GoldenDatasetEvaluationRecords_GoldenDatasetId",
                table: "GoldenDatasetEvaluationRecords",
                column: "GoldenDatasetId");

            migrationBuilder.CreateIndex(
                name: "IX_GoldenDatasetEvaluationRecords_GoldenDatasetId_EvaluatedAt",
                table: "GoldenDatasetEvaluationRecords",
                columns: new[] { "GoldenDatasetId", "EvaluatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GoldenDatasetEvaluationRecords_TenantId",
                table: "GoldenDatasetEvaluationRecords",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_GoldenDatasets_DatasetId",
                table: "GoldenDatasets",
                column: "DatasetId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoldenDatasets_IsActive",
                table: "GoldenDatasets",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_GoldenDatasets_TenantId",
                table: "GoldenDatasets",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_GoldenDatasets_TenantId_IsActive",
                table: "GoldenDatasets",
                columns: new[] { "TenantId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_GoldenDatasetSamples_Category",
                table: "GoldenDatasetSamples",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_GoldenDatasetSamples_GoldenDatasetId",
                table: "GoldenDatasetSamples",
                column: "GoldenDatasetId");

            migrationBuilder.CreateIndex(
                name: "IX_GoldenDatasetSamples_GoldenDatasetId_IsActive",
                table: "GoldenDatasetSamples",
                columns: new[] { "GoldenDatasetId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_GoldenDatasetSamples_IsActive",
                table: "GoldenDatasetSamples",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_SavedSearches_UserId",
                table: "SavedSearches",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedSearches_UserId_LastUsedAt",
                table: "SavedSearches",
                columns: new[] { "UserId", "LastUsedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserActivities_CreatedAt",
                table: "UserActivities",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserActivities_DocumentId",
                table: "UserActivities",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_UserActivities_UserId",
                table: "UserActivities",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserActivities_UserId_CreatedAt",
                table: "UserActivities",
                columns: new[] { "UserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DashboardWidgets");

            migrationBuilder.DropTable(
                name: "GoldenDatasetEvaluationRecords");

            migrationBuilder.DropTable(
                name: "GoldenDatasetSamples");

            migrationBuilder.DropTable(
                name: "SavedSearches");

            migrationBuilder.DropTable(
                name: "UserActivities");

            migrationBuilder.DropTable(
                name: "GoldenDatasets");

            migrationBuilder.DropColumn(
                name: "ErrorDetailsJson",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ErrorMessage",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ErrorType",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "IsRetryable",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "LastRetryAt",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "MaxRetries",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "NextRetryAt",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "PreviousWorkflowState",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "RetryCount",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "SourceConnectorId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "SourceFileHash",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "SourceFilePath",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "SourceLastModified",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "StateEnteredAt",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "WorkflowState",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ChunkType",
                table: "DocumentChunks");

            migrationBuilder.DropColumn(
                name: "ImportanceScore",
                table: "DocumentChunks");

            migrationBuilder.DropColumn(
                name: "KeywordsJson",
                table: "DocumentChunks");

            migrationBuilder.DropColumn(
                name: "Section",
                table: "DocumentChunks");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "DocumentChunks");
        }
    }
}
