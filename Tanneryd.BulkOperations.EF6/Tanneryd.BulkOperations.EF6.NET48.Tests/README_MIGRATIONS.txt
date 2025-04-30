- Öppna Package Manager console
- Ställ dig i ef-bulk-operations\Tanneryd.BulkOperations.EF6\Tanneryd.BulkOperations.EF6.NET48.Tests
- Se till att Default project är Tanneryd.BulkOperations.EF6.NET48.Tests

EntityFramework6\Enable-Migrations -ContextTypeName UnitTestContext -Verbose
EntityFramework6\Add-Migration InitialCreate
EntityFramework6\Add-Migration Update-Database
EntityFramework6\Add-Migration UpdateComputedColumns
EntityFramework6\Add-Migration Update-Database

        public partial class UpdateComputedColumns : DbMigration
		{
			public override void Up()
			{
				// Update dbo.Invoice.Tax to be a computed column
				Sql("ALTER TABLE dbo.Invoice DROP COLUMN Tax");
				Sql("ALTER TABLE dbo.Invoice ADD Tax AS (Gross - Net) PERSISTED");

				// Add computed column FullName to dbo.Instructor
				Sql("ALTER TABLE dbo.Instructor ADD FullName AS (FirstName + ' ' + LastName) PERSISTED");

				// Create the Contact view
				Sql("CREATE VIEW Contact AS SELECT FirstName, LastName FROM Person");
			}
			
			public override void Down()
			{
				// Revert dbo.Invoice.Tax to a regular column
				Sql("ALTER TABLE dbo.Invoice DROP COLUMN Tax");
				AddColumn("dbo.Invoice", "Tax", c => c.Decimal(nullable: false, precision: 18, scale: 2));

				// Remove the computed column FullName from dbo.Instructor
				Sql("ALTER TABLE dbo.Instructor DROP COLUMN FullName");

				// Drop the Contact view
				Sql("DROP VIEW Contact");
			}


		}