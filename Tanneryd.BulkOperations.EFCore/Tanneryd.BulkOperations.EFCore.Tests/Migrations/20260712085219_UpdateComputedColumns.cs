using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tanneryd.BulkOperations.EFCore.Tests.Migrations
{
    /// <inheritdoc />
    public partial class UpdateComputedColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Update dbo.Invoice.Tax to be a computed column
            migrationBuilder.Sql("ALTER TABLE dbo.Invoice DROP COLUMN Tax");
            migrationBuilder.Sql("ALTER TABLE dbo.Invoice ADD Tax AS (Gross - Net) PERSISTED");

            // Add computed column FullName to dbo.Instructor
            migrationBuilder.Sql("ALTER TABLE dbo.Instructor DROP COLUMN FullName");
            migrationBuilder.Sql("ALTER TABLE dbo.Instructor ADD FullName AS (FirstName + ' ' + LastName) PERSISTED");

            // Create the Contact view
            migrationBuilder.Sql("CREATE VIEW Contact AS SELECT FirstName, LastName FROM Person");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop the Contact view
            migrationBuilder.Sql("DROP VIEW Contact");

            // Revert dbo.Invoice.Tax to a regular column
            migrationBuilder.Sql("ALTER TABLE dbo.Invoice DROP COLUMN Tax");
            migrationBuilder.AddColumn<decimal>(
                name: "Tax",
                schema: "dbo",
                table: "Invoice",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            // Remove the computed column FullName from dbo.Instructor
            migrationBuilder.Sql("ALTER TABLE dbo.Instructor DROP COLUMN FullName");
            migrationBuilder.AddColumn<string>(
                name: "FullName",
                schema: "dbo",
                table: "Instructor",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
