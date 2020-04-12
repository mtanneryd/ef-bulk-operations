namespace Tanneryd.BulkOperations.EF6.NET47.Tests.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class InitialCreate : DbMigration
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
            
            CreateTable(
                "dbo.Blog",
                c => new
                    {
                        Id = c.Guid(nullable: false, identity: true),
                        Name = c.String(nullable: false),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.Post",
                c => new
                    {
                        Id = c.Guid(nullable: false, identity: true),
                        BlogId = c.Guid(nullable: false),
                        Text = c.String(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Blog", t => t.BlogId, cascadeDelete: true)
                .Index(t => t.BlogId);
            
            CreateTable(
                "dbo.Keyword",
                c => new
                    {
                        Id = c.Guid(nullable: false, identity: true),
                        PostId = c.Guid(nullable: false),
                        Text = c.String(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Post", t => t.PostId, cascadeDelete: true)
                .Index(t => t.PostId);
            
            CreateTable(
                "dbo.Visitor",
                c => new
                    {
                        Id = c.Guid(nullable: false, identity: true),
                        Name = c.String(nullable: false),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.CoachWithDbGeneratedGuid",
                c => new
                    {
                        Id = c.Guid(nullable: false, identity: true),
                        Firstname = c.String(nullable: false),
                        Lastname = c.String(nullable: false),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.TeamWithDbGeneratedGuid",
                c => new
                    {
                        Id = c.Guid(nullable: false, identity: true),
                        Name = c.String(nullable: false),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.PlayerWithDbGeneratedGuid",
                c => new
                    {
                        Id = c.Guid(nullable: false, identity: true),
                        Firstname = c.String(nullable: false),
                        Lastname = c.String(nullable: false),
                        TeamId = c.Guid(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.TeamWithDbGeneratedGuid", t => t.TeamId, cascadeDelete: true)
                .Index(t => t.TeamId);
            
            CreateTable(
                "dbo.CoachWithUserGeneratedGuid",
                c => new
                    {
                        Id = c.Guid(nullable: false),
                        Firstname = c.String(nullable: false),
                        Lastname = c.String(nullable: false),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.TeamWithUserGeneratedGuid",
                c => new
                    {
                        Id = c.Guid(nullable: false),
                        Name = c.String(nullable: false),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.PlayerWithUserGeneratedGuid",
                c => new
                    {
                        Id = c.Guid(nullable: false),
                        Firstname = c.String(nullable: false),
                        Lastname = c.String(nullable: false),
                        TeamId = c.Guid(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.TeamWithUserGeneratedGuid", t => t.TeamId, cascadeDelete: true)
                .Index(t => t.TeamId);
            
            CreateTable(
                "dbo.Company",
                c => new
                    {
                        Id = c.Long(nullable: false, identity: true),
                        Name = c.String(nullable: false),
                        ParentCompanyId = c.Long(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Company", t => t.ParentCompanyId)
                .Index(t => t.ParentCompanyId);
            
            CreateTable(
                "dbo.Employee",
                c => new
                    {
                        Id = c.Long(nullable: false, identity: true),
                        Name = c.String(nullable: false),
                        EmployerId = c.Long(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Company", t => t.EmployerId, cascadeDelete: true)
                .Index(t => t.EmployerId);
            
            CreateTable(
                "dbo.Composite",
                c => new
                    {
                        NumberId = c.Long(nullable: false),
                        UpdatedAt = c.DateTime(nullable: false),
                        UpdatedBy = c.String(),
                    })
                .PrimaryKey(t => t.NumberId)
                .ForeignKey("dbo.Number", t => t.NumberId)
                .Index(t => t.NumberId);
            
            CreateTable(
                "dbo.Number",
                c => new
                    {
                        Id = c.Long(nullable: false, identity: true),
                        Value = c.Long(nullable: false),
                        ParityId = c.Int(nullable: false),
                        PrimeId = c.Int(),
                        CompositeId = c.Int(),
                        UpdatedAt = c.DateTime(nullable: false),
                        UpdatedBy = c.String(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Parity", t => t.ParityId)
                .Index(t => t.ParityId);
            
            CreateTable(
                "dbo.Parity",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        Name = c.String(nullable: false),
                        UpdatedAt = c.DateTime(nullable: false),
                        UpdatedBy = c.String(),
                    })
                .PrimaryKey(t => t.Key);
            
            CreateTable(
                "dbo.Prime",
                c => new
                    {
                        NumberId = c.Long(nullable: false),
                        UpdatedAt = c.DateTime(nullable: false),
                        UpdatedBy = c.String(),
                    })
                .PrimaryKey(t => t.NumberId)
                .ForeignKey("dbo.Number", t => t.NumberId)
                .Index(t => t.NumberId);
            
            CreateTable(
                "dbo.Coordinate",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Value = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.Point",
                c => new
                    {
                        XCoordinateId = c.Int(nullable: false),
                        YCoordinateId = c.Int(nullable: false),
                        Value = c.Double(nullable: false),
                    })
                .PrimaryKey(t => new { t.XCoordinateId, t.YCoordinateId })
                .ForeignKey("dbo.Coordinate", t => t.XCoordinateId)
                .ForeignKey("dbo.Coordinate", t => t.YCoordinateId)
                .Index(t => t.XCoordinateId)
                .Index(t => t.YCoordinateId);
            
            CreateTable(
                "dbo.Course",
                c => new
                    {
                        CourseID = c.Int(nullable: false, identity: true),
                        Title = c.String(),
                        Credits = c.Int(nullable: false),
                        DepartmentID = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.CourseID)
                .ForeignKey("dbo.Department", t => t.DepartmentID, cascadeDelete: true)
                .Index(t => t.DepartmentID);
            
            CreateTable(
                "dbo.Department",
                c => new
                    {
                        DepartmentID = c.Int(nullable: false, identity: true),
                        Name = c.String(),
                        Budget = c.Decimal(nullable: false, precision: 18, scale: 2),
                        Administrator = c.Int(),
                    })
                .PrimaryKey(t => t.DepartmentID);
            
            CreateTable(
                "dbo.Instructor",
                c => new
                    {
                        InstructorID = c.Int(nullable: false, identity: true),
                        LastName = c.String(),
                        FirstName = c.String(),
                        //FullName = c.String(),
                        HireDate = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.InstructorID);
            
            // This replaces the out commented line for FullName in the create table above.
            Sql("ALTER TABLE dbo.Instructor ADD FullName AS (FirstName + ' ' + LastName) PERSISTED NOT NULL");

            CreateTable(
                "dbo.OfficeAssignment",
                c => new
                    {
                        InstructorID = c.Int(nullable: false),
                        Location = c.String(),
                        Timestamp = c.Binary(),
                    })
                .PrimaryKey(t => t.InstructorID)
                .ForeignKey("dbo.Instructor", t => t.InstructorID)
                .Index(t => t.InstructorID);
            
            CreateTable(
                "[In # Some.Complex_Schema @Name].SELECT WORSE FROM NAMES AS Extent1",
                c => new
                    {
                        ReportID = c.Int(nullable: false, identity: true),
                        Title = c.String(),
                        Entry = c.String(),
                        Date = c.DateTime(nullable: false),
                        Level1 = c.String(),
                        Level2 = c.String(),
                        Level3 = c.String(),
                        IamBadColumnName1 = c.String(name: "I am Bad Column Name 1 "),
                        A_Abc123Абв = c.String(name: "A #_@ Abc 123 Абв$"),
                        SELECTWORSEFROMNAMESASExtent = c.String(name: "SELECT WORSE FROM NAMES AS Extent "),
                        Volume = c.Decimal(nullable: false, precision: 18, scale: 2),
                        Amount = c.Decimal(nullable: false, precision: 18, scale: 2),
                        PeriodID = c.Int(nullable: false),
                        SummaryReportID = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.ReportID)
                .ForeignKey("[Some.Complex_Schema Name].Period", t => t.PeriodID, cascadeDelete: true)
                .ForeignKey("dbo.SummaryReportFROMTableASExtents", t => t.SummaryReportID)
                .Index(t => t.PeriodID)
                .Index(t => t.SummaryReportID);
            
            CreateTable(
                "[Some.Complex_Schema Name].Period",
                c => new
                    {
                        PeriodID = c.Int(nullable: false, identity: true),
                        Name = c.String(),
                    })
                .PrimaryKey(t => t.PeriodID);
            
            CreateTable(
                "dbo.SummaryReportFROMTableASExtents",
                c => new
                    {
                        ReportID = c.Int(nullable: false, identity: true),
                        Title = c.String(),
                        Entry = c.String(),
                        Volume = c.Decimal(nullable: false, precision: 18, scale: 2),
                        Amount = c.Decimal(nullable: false, precision: 18, scale: 2),
                        PeriodID = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.ReportID)
                .ForeignKey("[Some.Complex_Schema Name].Period", t => t.PeriodID, cascadeDelete: true)
                .Index(t => t.PeriodID);
            
            CreateTable(
                "dbo.EmptyTable",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.Level1",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Level1Name = c.String(),
                        Level2Name = c.String(),
                        Level3Name = c.String(),
                        Level2_Level3_Updated = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.Person",
                c => new
                    {
                        Id = c.Long(nullable: false, identity: true),
                        FirstName = c.String(nullable: false),
                        LastName = c.String(nullable: false),
                        BirthDate = c.DateTime(nullable: false),
                        MotherId = c.Long(),
                        EmployeeNumber = c.Int(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Person", t => t.MotherId)
                .Index(t => t.MotherId);
            
            CreateTable(
                "dbo.Price",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Date = c.DateTime(nullable: false),
                        Name = c.String(nullable: false),
                        Value = c.Decimal(precision: 18, scale: 2),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.ReservedSqlKeyword",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Key = c.Int(),
                        Identity = c.Int(),
                        Select = c.String(),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.VisitorPosts",
                c => new
                    {
                        VisitorId = c.Guid(nullable: false),
                        PostId = c.Guid(nullable: false),
                    })
                .PrimaryKey(t => new { t.VisitorId, t.PostId })
                .ForeignKey("dbo.Visitor", t => t.VisitorId, cascadeDelete: true)
                .ForeignKey("dbo.Post", t => t.PostId, cascadeDelete: true)
                .Index(t => t.VisitorId)
                .Index(t => t.PostId);
            
            CreateTable(
                "dbo.CoachTeamsWithDbGeneratedGuid",
                c => new
                    {
                        CoachId = c.Guid(nullable: false),
                        TeamId = c.Guid(nullable: false),
                    })
                .PrimaryKey(t => new { t.CoachId, t.TeamId })
                .ForeignKey("dbo.CoachWithDbGeneratedGuid", t => t.CoachId, cascadeDelete: true)
                .ForeignKey("dbo.TeamWithDbGeneratedGuid", t => t.TeamId, cascadeDelete: true)
                .Index(t => t.CoachId)
                .Index(t => t.TeamId);
            
            CreateTable(
                "dbo.CoachTeamsWithUserGeneratedGuid",
                c => new
                    {
                        CoachId = c.Guid(nullable: false),
                        TeamId = c.Guid(nullable: false),
                    })
                .PrimaryKey(t => new { t.CoachId, t.TeamId })
                .ForeignKey("dbo.CoachWithUserGeneratedGuid", t => t.CoachId, cascadeDelete: true)
                .ForeignKey("dbo.TeamWithUserGeneratedGuid", t => t.TeamId, cascadeDelete: true)
                .Index(t => t.CoachId)
                .Index(t => t.TeamId);
            
            CreateTable(
                "dbo.CompositePrime",
                c => new
                    {
                        CompositeId = c.Long(nullable: false),
                        PrimeId = c.Long(nullable: false),
                    })
                .PrimaryKey(t => new { t.CompositeId, t.PrimeId })
                .ForeignKey("dbo.Composite", t => t.CompositeId, cascadeDelete: true)
                .ForeignKey("dbo.Prime", t => t.PrimeId, cascadeDelete: true)
                .Index(t => t.CompositeId)
                .Index(t => t.PrimeId);
            
            CreateTable(
                "dbo.CourseInstructor",
                c => new
                    {
                        CourseID = c.Int(nullable: false),
                        InstructorID = c.Int(nullable: false),
                    })
                .PrimaryKey(t => new { t.CourseID, t.InstructorID })
                .ForeignKey("dbo.Course", t => t.CourseID, cascadeDelete: true)
                .ForeignKey("dbo.Instructor", t => t.InstructorID, cascadeDelete: true)
                .Index(t => t.CourseID)
                .Index(t => t.InstructorID);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.Person", "MotherId", "dbo.Person");
            DropForeignKey("[In # Some.Complex_Schema @Name].SELECT WORSE FROM NAMES AS Extent1", "SummaryReportID", "dbo.SummaryReportFROMTableASExtents");
            DropForeignKey("[In # Some.Complex_Schema @Name].SELECT WORSE FROM NAMES AS Extent1", "PeriodID", "[Some.Complex_Schema Name].Period");
            DropForeignKey("dbo.SummaryReportFROMTableASExtents", "PeriodID", "[Some.Complex_Schema Name].Period");
            DropForeignKey("dbo.CourseInstructor", "InstructorID", "dbo.Instructor");
            DropForeignKey("dbo.CourseInstructor", "CourseID", "dbo.Course");
            DropForeignKey("dbo.OfficeAssignment", "InstructorID", "dbo.Instructor");
            DropForeignKey("dbo.Course", "DepartmentID", "dbo.Department");
            DropForeignKey("dbo.Point", "YCoordinateId", "dbo.Coordinate");
            DropForeignKey("dbo.Point", "XCoordinateId", "dbo.Coordinate");
            DropForeignKey("dbo.CompositePrime", "PrimeId", "dbo.Prime");
            DropForeignKey("dbo.CompositePrime", "CompositeId", "dbo.Composite");
            DropForeignKey("dbo.Composite", "NumberId", "dbo.Number");
            DropForeignKey("dbo.Prime", "NumberId", "dbo.Number");
            DropForeignKey("dbo.Number", "ParityId", "dbo.Parity");
            DropForeignKey("dbo.Company", "ParentCompanyId", "dbo.Company");
            DropForeignKey("dbo.Employee", "EmployerId", "dbo.Company");
            DropForeignKey("dbo.CoachTeamsWithUserGeneratedGuid", "TeamId", "dbo.TeamWithUserGeneratedGuid");
            DropForeignKey("dbo.CoachTeamsWithUserGeneratedGuid", "CoachId", "dbo.CoachWithUserGeneratedGuid");
            DropForeignKey("dbo.PlayerWithUserGeneratedGuid", "TeamId", "dbo.TeamWithUserGeneratedGuid");
            DropForeignKey("dbo.CoachTeamsWithDbGeneratedGuid", "TeamId", "dbo.TeamWithDbGeneratedGuid");
            DropForeignKey("dbo.CoachTeamsWithDbGeneratedGuid", "CoachId", "dbo.CoachWithDbGeneratedGuid");
            DropForeignKey("dbo.PlayerWithDbGeneratedGuid", "TeamId", "dbo.TeamWithDbGeneratedGuid");
            DropForeignKey("dbo.Post", "BlogId", "dbo.Blog");
            DropForeignKey("dbo.VisitorPosts", "PostId", "dbo.Post");
            DropForeignKey("dbo.VisitorPosts", "VisitorId", "dbo.Visitor");
            DropForeignKey("dbo.Keyword", "PostId", "dbo.Post");
            DropForeignKey("dbo.InvoiceItem", "InvoiceId", "dbo.Invoice");
            DropForeignKey("dbo.InvoiceItem", "JournalId", "dbo.Journal");
            DropForeignKey("dbo.BatchInvoiceItem", "InvoiceId", "dbo.Invoice");
            DropForeignKey("dbo.BatchInvoiceItem", "BatchInvoiceId", "dbo.BatchInvoice");
            DropIndex("dbo.CourseInstructor", new[] { "InstructorID" });
            DropIndex("dbo.CourseInstructor", new[] { "CourseID" });
            DropIndex("dbo.CompositePrime", new[] { "PrimeId" });
            DropIndex("dbo.CompositePrime", new[] { "CompositeId" });
            DropIndex("dbo.CoachTeamsWithUserGeneratedGuid", new[] { "TeamId" });
            DropIndex("dbo.CoachTeamsWithUserGeneratedGuid", new[] { "CoachId" });
            DropIndex("dbo.CoachTeamsWithDbGeneratedGuid", new[] { "TeamId" });
            DropIndex("dbo.CoachTeamsWithDbGeneratedGuid", new[] { "CoachId" });
            DropIndex("dbo.VisitorPosts", new[] { "PostId" });
            DropIndex("dbo.VisitorPosts", new[] { "VisitorId" });
            DropIndex("dbo.Person", new[] { "MotherId" });
            DropIndex("dbo.SummaryReportFROMTableASExtents", new[] { "PeriodID" });
            DropIndex("[In # Some.Complex_Schema @Name].SELECT WORSE FROM NAMES AS Extent1", new[] { "SummaryReportID" });
            DropIndex("[In # Some.Complex_Schema @Name].SELECT WORSE FROM NAMES AS Extent1", new[] { "PeriodID" });
            DropIndex("dbo.OfficeAssignment", new[] { "InstructorID" });
            DropIndex("dbo.Course", new[] { "DepartmentID" });
            DropIndex("dbo.Point", new[] { "YCoordinateId" });
            DropIndex("dbo.Point", new[] { "XCoordinateId" });
            DropIndex("dbo.Prime", new[] { "NumberId" });
            DropIndex("dbo.Number", new[] { "ParityId" });
            DropIndex("dbo.Composite", new[] { "NumberId" });
            DropIndex("dbo.Employee", new[] { "EmployerId" });
            DropIndex("dbo.Company", new[] { "ParentCompanyId" });
            DropIndex("dbo.PlayerWithUserGeneratedGuid", new[] { "TeamId" });
            DropIndex("dbo.PlayerWithDbGeneratedGuid", new[] { "TeamId" });
            DropIndex("dbo.Keyword", new[] { "PostId" });
            DropIndex("dbo.Post", new[] { "BlogId" });
            DropIndex("dbo.InvoiceItem", new[] { "InvoiceId" });
            DropIndex("dbo.InvoiceItem", new[] { "JournalId" });
            DropIndex("dbo.BatchInvoiceItem", new[] { "InvoiceId" });
            DropIndex("dbo.BatchInvoiceItem", new[] { "BatchInvoiceId" });
            DropTable("dbo.CourseInstructor");
            DropTable("dbo.CompositePrime");
            DropTable("dbo.CoachTeamsWithUserGeneratedGuid");
            DropTable("dbo.CoachTeamsWithDbGeneratedGuid");
            DropTable("dbo.VisitorPosts");
            DropTable("dbo.ReservedSqlKeyword");
            DropTable("dbo.Price");
            DropTable("dbo.Person");
            DropTable("dbo.Level1");
            DropTable("dbo.EmptyTable");
            DropTable("dbo.SummaryReportFROMTableASExtents");
            DropTable("[Some.Complex_Schema Name].Period");
            DropTable("[In # Some.Complex_Schema @Name].SELECT WORSE FROM NAMES AS Extent1");
            DropTable("dbo.OfficeAssignment");
            DropTable("dbo.Instructor");
            DropTable("dbo.Department");
            DropTable("dbo.Course");
            DropTable("dbo.Point");
            DropTable("dbo.Coordinate");
            DropTable("dbo.Prime");
            DropTable("dbo.Parity");
            DropTable("dbo.Number");
            DropTable("dbo.Composite");
            DropTable("dbo.Employee");
            DropTable("dbo.Company");
            DropTable("dbo.PlayerWithUserGeneratedGuid");
            DropTable("dbo.TeamWithUserGeneratedGuid");
            DropTable("dbo.CoachWithUserGeneratedGuid");
            DropTable("dbo.PlayerWithDbGeneratedGuid");
            DropTable("dbo.TeamWithDbGeneratedGuid");
            DropTable("dbo.CoachWithDbGeneratedGuid");
            DropTable("dbo.Visitor");
            DropTable("dbo.Keyword");
            DropTable("dbo.Post");
            DropTable("dbo.Blog");
            DropTable("dbo.Journal");
            DropTable("dbo.InvoiceItem");
            DropTable("dbo.Invoice");
            DropTable("dbo.BatchInvoice");
            DropTable("dbo.BatchInvoiceItem");
        }
    }
}
