using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocN.Data.Migrations
{
    /// <inheritdoc />
    public partial class MakeOwnerIdOptionalAndFixConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop the existing foreign key constraint
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1 FROM sys.foreign_keys 
                    WHERE name = 'FK_Documents_AspNetUsers_OwnerId' 
                    AND parent_object_id = OBJECT_ID('Documents')
                )
                BEGIN
                    ALTER TABLE Documents DROP CONSTRAINT FK_Documents_AspNetUsers_OwnerId;
                END
            ");

            // Make OwnerId column nullable (if not already)
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_NAME = 'Documents' 
                    AND COLUMN_NAME = 'OwnerId' 
                    AND IS_NULLABLE = 'NO'
                )
                BEGIN
                    ALTER TABLE Documents ALTER COLUMN OwnerId NVARCHAR(450) NULL;
                END
            ");

            // Re-create the foreign key constraint with SET NULL on delete
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.foreign_keys 
                    WHERE name = 'FK_Documents_AspNetUsers_OwnerId' 
                    AND parent_object_id = OBJECT_ID('Documents')
                )
                BEGIN
                    ALTER TABLE Documents 
                    ADD CONSTRAINT FK_Documents_AspNetUsers_OwnerId 
                    FOREIGN KEY (OwnerId) 
                    REFERENCES AspNetUsers(Id) 
                    ON DELETE SET NULL;
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop the SET NULL foreign key constraint
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1 FROM sys.foreign_keys 
                    WHERE name = 'FK_Documents_AspNetUsers_OwnerId' 
                    AND parent_object_id = OBJECT_ID('Documents')
                )
                BEGIN
                    ALTER TABLE Documents DROP CONSTRAINT FK_Documents_AspNetUsers_OwnerId;
                END
            ");

            // Re-create the original CASCADE foreign key constraint
            migrationBuilder.Sql(@"
                ALTER TABLE Documents 
                ADD CONSTRAINT FK_Documents_AspNetUsers_OwnerId 
                FOREIGN KEY (OwnerId) 
                REFERENCES AspNetUsers(Id) 
                ON DELETE CASCADE;
            ");
        }
    }
}
