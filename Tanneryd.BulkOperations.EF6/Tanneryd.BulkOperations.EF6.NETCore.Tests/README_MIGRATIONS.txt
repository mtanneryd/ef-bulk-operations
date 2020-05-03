Enable-Migrations -MigrationsDirectory Migrations -ContextTypeName Tanneryd.BulkOperations.EF6.NET47.Tests.Models.EF.UnitTestContext -ProjectName Tanneryd.BulkOperations.EF6.NET47.Tests
Add-Migration -configuration Tanneryd.BulkOperations.EF6.NET47.Tests.Migrations.Configuration -ProjectName Tanneryd.BulkOperations.EF6.NET47.Tests InitialMigration

Enable-Migrations -MigrationsDirectory Migrations -ContextTypeName Tanneryd.BulkOperations.EF6.NETCore.Tests.Models.EF.UnitTestContext -ProjectName Tanneryd.BulkOperations.EF6.NETCore.Tests
Add-Migration -configuration Tanneryd.BulkOperations.EF6.NETCore.Tests.Migrations.Configuration -ProjectName Tanneryd.BulkOperations.EF6.NETCore.Tests InitialMigration


// This replaces the out commented line for Tax in the create table above.
Sql("ALTER TABLE dbo.Invoice ADD Tax AS (Gross - Net) PERSISTED NOT NULL");

// This replaces the out commented line for FullName in the create table above.
Sql("ALTER TABLE dbo.Instructor ADD FullName AS (FirstName + ' ' + LastName) PERSISTED NOT NULL");