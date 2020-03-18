namespace Tanneryd.BulkOperations.EF6.Tests.Migrations.Invoice
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddComputedTaxColumnToInvoice : DbMigration
    {
        public override void Up()
        {
            Sql("ALTER TABLE dbo.Invoice ADD Tax AS (Gross - Net) PERSISTED NOT NULL");
        }
        
        public override void Down()
        {
        }
    }
}
