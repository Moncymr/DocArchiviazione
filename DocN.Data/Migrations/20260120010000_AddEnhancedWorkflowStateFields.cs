using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocN.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEnhancedWorkflowStateFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add enhanced workflow state management fields
            migrationBuilder.AddColumn<string>(
                name: "WorkflowState",
                table: "Documents",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousWorkflowState",
                table: "Documents",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StateEnteredAt",
                table: "Documents",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetryCount",
                table: "Documents",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxRetries",
                table: "Documents",
                type: "int",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastRetryAt",
                table: "Documents",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextRetryAt",
                table: "Documents",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ErrorDetailsJson",
                table: "Documents",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ErrorMessage",
                table: "Documents",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ErrorType",
                table: "Documents",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRetryable",
                table: "Documents",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Create indexes for workflow queries
            migrationBuilder.CreateIndex(
                name: "IX_Documents_WorkflowState",
                table: "Documents",
                column: "WorkflowState");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_NextRetryAt",
                table: "Documents",
                column: "NextRetryAt");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_WorkflowState_NextRetryAt",
                table: "Documents",
                columns: new[] { "WorkflowState", "NextRetryAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ErrorType",
                table: "Documents",
                column: "ErrorType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop indexes
            migrationBuilder.DropIndex(
                name: "IX_Documents_WorkflowState",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_NextRetryAt",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_WorkflowState_NextRetryAt",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_ErrorType",
                table: "Documents");

            // Drop columns
            migrationBuilder.DropColumn(name: "WorkflowState", table: "Documents");
            migrationBuilder.DropColumn(name: "PreviousWorkflowState", table: "Documents");
            migrationBuilder.DropColumn(name: "StateEnteredAt", table: "Documents");
            migrationBuilder.DropColumn(name: "RetryCount", table: "Documents");
            migrationBuilder.DropColumn(name: "MaxRetries", table: "Documents");
            migrationBuilder.DropColumn(name: "LastRetryAt", table: "Documents");
            migrationBuilder.DropColumn(name: "NextRetryAt", table: "Documents");
            migrationBuilder.DropColumn(name: "ErrorDetailsJson", table: "Documents");
            migrationBuilder.DropColumn(name: "ErrorMessage", table: "Documents");
            migrationBuilder.DropColumn(name: "ErrorType", table: "Documents");
            migrationBuilder.DropColumn(name: "IsRetryable", table: "Documents");
        }
    }
}
