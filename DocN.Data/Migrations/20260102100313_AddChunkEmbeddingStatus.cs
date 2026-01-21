using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocN.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddChunkEmbeddingStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "EmbeddingVector",
                table: "Documents",
                newName: "EmbeddingVector768");

            migrationBuilder.RenameColumn(
                name: "ChunkEmbedding",
                table: "DocumentChunks",
                newName: "ChunkEmbedding768");

            migrationBuilder.AddColumn<string>(
                name: "ChunkEmbeddingStatus",
                table: "Documents",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmbeddingVector1536",
                table: "Documents",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChunkEmbedding1536",
                table: "DocumentChunks",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AgentTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Icon = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AgentType = table.Column<int>(type: "int", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RecommendedProvider = table.Column<int>(type: "int", nullable: false),
                    RecommendedModel = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DefaultSystemPrompt = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DefaultParametersJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    IsBuiltIn = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    UsageCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OwnerId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ExampleQuery = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExampleResponse = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConfigurationGuide = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentTemplates_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Username = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ResourceType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ResourceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Details = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TenantId = table.Column<int>(type: "int", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Success = table.Column<bool>(type: "bit", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditLogs_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AuditLogs_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "LogEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Level = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Details = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    FileName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    StackTrace = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AgentConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    AgentType = table.Column<int>(type: "int", nullable: false),
                    PrimaryProvider = table.Column<int>(type: "int", nullable: false),
                    FallbackProvider = table.Column<int>(type: "int", nullable: true),
                    ModelName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EmbeddingModelName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MaxDocumentsToRetrieve = table.Column<int>(type: "int", nullable: false),
                    SimilarityThreshold = table.Column<double>(type: "float", nullable: false),
                    MaxTokensForContext = table.Column<int>(type: "int", nullable: false),
                    MaxTokensForResponse = table.Column<int>(type: "int", nullable: false),
                    Temperature = table.Column<double>(type: "float", nullable: false),
                    SystemPrompt = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CustomInstructions = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CanRetrieveDocuments = table.Column<bool>(type: "bit", nullable: false),
                    CanClassifyDocuments = table.Column<bool>(type: "bit", nullable: false),
                    CanExtractTags = table.Column<bool>(type: "bit", nullable: false),
                    CanSummarize = table.Column<bool>(type: "bit", nullable: false),
                    CanAnswer = table.Column<bool>(type: "bit", nullable: false),
                    UseHybridSearch = table.Column<bool>(type: "bit", nullable: false),
                    HybridSearchAlpha = table.Column<double>(type: "float", nullable: false),
                    EnableConversationHistory = table.Column<bool>(type: "bit", nullable: false),
                    MaxConversationHistoryMessages = table.Column<int>(type: "int", nullable: false),
                    EnableCitation = table.Column<bool>(type: "bit", nullable: false),
                    EnableStreaming = table.Column<bool>(type: "bit", nullable: false),
                    CategoryFilter = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TagFilter = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    VisibilityFilter = table.Column<int>(type: "int", nullable: true),
                    CacheTTLSeconds = table.Column<int>(type: "int", nullable: true),
                    EnableParallelRetrieval = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsPublic = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUsedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UsageCount = table.Column<int>(type: "int", nullable: false),
                    OwnerId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    TenantId = table.Column<int>(type: "int", nullable: true),
                    TemplateId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentConfigurations_AgentTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "AgentTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AgentConfigurations_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AgentConfigurations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AgentUsageLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AgentConfigurationId = table.Column<int>(type: "int", nullable: false),
                    Query = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Response = table.Column<string>(type: "nvarchar(max)", maxLength: 2147483647, nullable: true),
                    DocumentsRetrieved = table.Column<int>(type: "int", nullable: false),
                    RetrievalTimeTicks = table.Column<long>(type: "bigint", nullable: false),
                    SynthesisTimeTicks = table.Column<long>(type: "bigint", nullable: false),
                    TotalTimeTicks = table.Column<long>(type: "bigint", nullable: false),
                    RetrievalTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    SynthesisTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    TotalTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    PromptTokens = table.Column<int>(type: "int", nullable: true),
                    CompletionTokens = table.Column<int>(type: "int", nullable: true),
                    TotalTokens = table.Column<int>(type: "int", nullable: true),
                    ProviderUsed = table.Column<int>(type: "int", nullable: false),
                    ModelUsed = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RelevanceScore = table.Column<double>(type: "float", nullable: true),
                    UserFeedbackPositive = table.Column<bool>(type: "bit", nullable: true),
                    UserFeedbackComment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsError = table.Column<bool>(type: "bit", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    TenantId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentUsageLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentUsageLogs_AgentConfigurations_AgentConfigurationId",
                        column: x => x.AgentConfigurationId,
                        principalTable: "AgentConfigurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AgentUsageLogs_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AgentUsageLogs_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentConfigurations_AgentType",
                table: "AgentConfigurations",
                column: "AgentType");

            migrationBuilder.CreateIndex(
                name: "IX_AgentConfigurations_IsActive",
                table: "AgentConfigurations",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_AgentConfigurations_OwnerId",
                table: "AgentConfigurations",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentConfigurations_TemplateId",
                table: "AgentConfigurations",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentConfigurations_TenantId",
                table: "AgentConfigurations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentConfigurations_TenantId_IsActive",
                table: "AgentConfigurations",
                columns: new[] { "TenantId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentTemplates_AgentType",
                table: "AgentTemplates",
                column: "AgentType");

            migrationBuilder.CreateIndex(
                name: "IX_AgentTemplates_Category",
                table: "AgentTemplates",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_AgentTemplates_IsActive",
                table: "AgentTemplates",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_AgentTemplates_IsBuiltIn",
                table: "AgentTemplates",
                column: "IsBuiltIn");

            migrationBuilder.CreateIndex(
                name: "IX_AgentTemplates_OwnerId",
                table: "AgentTemplates",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentUsageLogs_AgentConfigurationId",
                table: "AgentUsageLogs",
                column: "AgentConfigurationId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentUsageLogs_AgentConfigurationId_CreatedAt",
                table: "AgentUsageLogs",
                columns: new[] { "AgentConfigurationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentUsageLogs_CreatedAt",
                table: "AgentUsageLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AgentUsageLogs_TenantId",
                table: "AgentUsageLogs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentUsageLogs_UserId",
                table: "AgentUsageLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Action",
                table: "AuditLogs",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Action_Timestamp",
                table: "AuditLogs",
                columns: new[] { "Action", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ResourceType",
                table: "AuditLogs",
                column: "ResourceType");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ResourceType_ResourceId",
                table: "AuditLogs",
                columns: new[] { "ResourceType", "ResourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_TenantId",
                table: "AuditLogs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Timestamp",
                table: "AuditLogs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId",
                table: "AuditLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId_Timestamp",
                table: "AuditLogs",
                columns: new[] { "UserId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_LogEntries_Category_Timestamp",
                table: "LogEntries",
                columns: new[] { "Category", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_LogEntries_Timestamp",
                table: "LogEntries",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_LogEntries_UserId_Timestamp",
                table: "LogEntries",
                columns: new[] { "UserId", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentUsageLogs");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "LogEntries");

            migrationBuilder.DropTable(
                name: "AgentConfigurations");

            migrationBuilder.DropTable(
                name: "AgentTemplates");

            migrationBuilder.DropColumn(
                name: "ChunkEmbeddingStatus",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "EmbeddingVector1536",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ChunkEmbedding1536",
                table: "DocumentChunks");

            migrationBuilder.RenameColumn(
                name: "EmbeddingVector768",
                table: "Documents",
                newName: "EmbeddingVector");

            migrationBuilder.RenameColumn(
                name: "ChunkEmbedding768",
                table: "DocumentChunks",
                newName: "ChunkEmbedding");
        }
    }
}
