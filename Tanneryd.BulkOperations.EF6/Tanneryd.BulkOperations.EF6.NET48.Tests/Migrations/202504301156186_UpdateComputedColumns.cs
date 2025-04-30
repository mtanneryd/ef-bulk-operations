namespace Tanneryd.BulkOperations.EF6.NET48.Tests.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class UpdateComputedColumns : DbMigration
    {
        public override void Up()
        {
            // Update dbo.Invoice.Tax to be a computed column
            Sql("ALTER TABLE dbo.Invoice DROP COLUMN Tax");
            Sql("ALTER TABLE dbo.Invoice ADD Tax AS (Gross - Net) PERSISTED");

            // Add computed column FullName to dbo.Instructor
            Sql("ALTER TABLE dbo.Instructor DROP COLUMN FullName");
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
}
