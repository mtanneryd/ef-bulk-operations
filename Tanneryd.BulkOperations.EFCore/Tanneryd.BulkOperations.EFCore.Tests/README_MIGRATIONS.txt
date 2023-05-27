Add-Migration -Project Tanneryd.BulkOperations.EFCore.Tests -Startup Tanneryd.BulkOperations.EFCore.Tests InitialMigration

// This replaces the out commented line for Tax in the create table above.
migrationBuilder.Sql("ALTER TABLE dbo.Invoice ADD Tax AS (Gross - Net) PERSISTED NOT NULL");

// This replaces the out commented line for FullName in the create table above.
migrationBuilder.Sql("ALTER TABLE dbo.Instructor ADD FullName AS (FirstName + ' ' + LastName) PERSISTED NOT NULL");

Add this at the end of "protected override void Up(MigrationBuilder migrationBuilder)"
migrationBuilder.Sql("CREATE VIEW AS SELECT FirstName, LastName FROM Person");