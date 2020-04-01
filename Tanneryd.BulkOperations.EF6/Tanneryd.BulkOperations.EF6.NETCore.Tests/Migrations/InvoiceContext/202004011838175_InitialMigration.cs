namespace Tanneryd.BulkOperations.EF6.NETCore.Tests.Migrations.InvoiceContext
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class InitialMigration : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.BatchInvoiceItem",
                c => new
                    {
                        PrimaryKey = c.Guid(nullable: false),
                        BatchInvoiceId = c.Guid(nullable: false),
                        InvoiceId = c.Guid(nullable: false),
                    })
                .PrimaryKey(t => t.PrimaryKey)
                .ForeignKey("dbo.BatchInvoice", t => t.BatchInvoiceId, cascadeDelete: true)
                .ForeignKey("dbo.Invoice", t => t.InvoiceId, cascadeDelete: true)
                .Index(t => t.BatchInvoiceId)
                .Index(t => t.InvoiceId);
            
            CreateTable(
                "dbo.BatchInvoice",
                c => new
                    {
                        PrimaryKey = c.Guid(nullable: false),
                    })
                .PrimaryKey(t => t.PrimaryKey);
            
            CreateTable(
                "dbo.Invoice",
                c => new
                    {
                        PrimaryKey = c.Guid(nullable: false),
                        Net = c.Decimal(nullable: false, precision: 18, scale: 2),
                        Gross = c.Decimal(nullable: false, precision: 18, scale: 2),
                        //Tax = c.Decimal(nullable: false, precision: 18, scale: 2),
                    })
                .PrimaryKey(t => t.PrimaryKey);
            
            // This replaces the out commented line for Tax in the create table above.
            Sql("ALTER TABLE dbo.Invoice ADD Tax AS (Gross - Net) PERSISTED NOT NULL");

            CreateTable(
                "dbo.InvoiceItem",
                c => new
                    {
                        PrimaryKey = c.Int(nullable: false, identity: true),
                        JournalId = c.Guid(nullable: false),
                        InvoiceId = c.Guid(nullable: false),
                    })
                .PrimaryKey(t => t.PrimaryKey)
                .ForeignKey("dbo.Journal", t => t.JournalId, cascadeDelete: true)
                .ForeignKey("dbo.Invoice", t => t.InvoiceId, cascadeDelete: true)
                .Index(t => t.JournalId)
                .Index(t => t.InvoiceId);
            
            CreateTable(
                "dbo.Journal",
                c => new
                    {
                        PrimaryKey = c.Guid(nullable: false),
                    })
                .PrimaryKey(t => t.PrimaryKey);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.InvoiceItem", "InvoiceId", "dbo.Invoice");
            DropForeignKey("dbo.InvoiceItem", "JournalId", "dbo.Journal");
            DropForeignKey("dbo.BatchInvoiceItem", "InvoiceId", "dbo.Invoice");
            DropForeignKey("dbo.BatchInvoiceItem", "BatchInvoiceId", "dbo.BatchInvoice");
            DropIndex("dbo.InvoiceItem", new[] { "InvoiceId" });
            DropIndex("dbo.InvoiceItem", new[] { "JournalId" });
            DropIndex("dbo.BatchInvoiceItem", new[] { "InvoiceId" });
            DropIndex("dbo.BatchInvoiceItem", new[] { "BatchInvoiceId" });
            DropTable("dbo.Journal");
            DropTable("dbo.InvoiceItem");
            DropTable("dbo.Invoice");
            DropTable("dbo.BatchInvoice");
            DropTable("dbo.BatchInvoiceItem");
        }
    }
}
