namespace Tanneryd.BulkOperations.EF6.Tests.Migrations.School
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddComputedFullNameColumnToInstructor : DbMigration
    {
        public override void Up()
        {
            //AddColumn("dbo.Instructor", "FullName", c => c.String());
            Sql("ALTER TABLE dbo.Instructor ADD FullName AS (FirstName + ' ' + LastName) PERSISTED NOT NULL");
        }
        
        public override void Down()
        {
            DropColumn("dbo.Instructor", "FullName");
        }
    }
}
