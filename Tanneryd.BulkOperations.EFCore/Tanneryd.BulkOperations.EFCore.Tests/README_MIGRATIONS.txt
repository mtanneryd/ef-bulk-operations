dotnet ef migrations add InitialMigration --verbose
dotnet ef database update --verbose

dotnet ef migrations add UpdateComputedColumns --verbose
dotnet ef database update --verbose

Tests apply migrations at runtime via Database.Migrate() in BulkOperationTestBase.InitializeUnitTestContext(),
matching EF6's DbMigrator.Update() behavior.

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
