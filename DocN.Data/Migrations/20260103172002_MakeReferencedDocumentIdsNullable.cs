using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocN.Data.Migrations
{
    /// <inheritdoc />
    public partial class MakeReferencedDocumentIdsNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Make ReferencedDocumentIds column nullable for existing databases
            // that were created with the incorrect NOT NULL constraint
            migrationBuilder.AlterColumn<string>(
                name: "ReferencedDocumentIds",
                table: "Messages",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Update NULL values to empty string before changing constraint
            migrationBuilder.Sql(
                "UPDATE Messages SET ReferencedDocumentIds = '' WHERE ReferencedDocumentIds IS NULL");
            
            // Revert to NOT NULL (though this would cause issues with user messages)
            migrationBuilder.AlterColumn<string>(
                name: "ReferencedDocumentIds",
                table: "Messages",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                defaultValue: "");
        }
    }
}
