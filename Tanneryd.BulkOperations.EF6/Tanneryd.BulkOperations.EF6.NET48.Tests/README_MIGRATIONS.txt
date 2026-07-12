EF6 test database migrations
=============================

This project uses Code First migrations to create and maintain the SQL Server
schema used by the integration tests.

Database
--------
  Server:   (localdb)\MSSQLLocalDB
  Catalog:  Tanneryd.BulkOperations.EF6.NET48.Tests.Models.EF.UnitTestContext
  Config:   app.config connection string "UnitTestContext"

Migrations (in order)
---------------------
  1. InitialCreate
     Scaffolds the full test schema. Invoice.Tax and Instructor.FullName are
     created as ordinary columns because EF6 migrations cannot express computed
     column formulas in CreateTable.

  2. UpdateComputedColumns
     Hand-written Sql() that:
       - Converts Invoice.Tax to (Gross - Net) PERSISTED
       - Converts Instructor.FullName to (FirstName + ' ' + LastName) PERSISTED
       - Creates the Contact view over Person (mapped via ContactViewContext in tests;
         ContactViewContext shares the UnitTestContext database via name=UnitTestContext)

What happens when tests run
---------------------------
  Each test class calls BulkOperationTestBase.InitializeUnitTestContext() from
  [TestInitialize]. That method:

    1. Disables the database initializer (SetInitializer(null))
    2. Runs DbMigrator.Update() to apply any pending migrations
    3. Ensures dbo.Contact is a view (drops a stray Contact table if present)
    4. Deletes all rows via CleanupUnitTestContext() (schema is kept)

  Migrations are idempotent: after the first run, Update() is a no-op unless a
  new migration has been added. Tests do not drop or recreate the database.

Creating a new migration (developer workflow)
---------------------------------------------
  Package Manager Console:
    Default project: Tanneryd.BulkOperations.EF6.NET48.Tests
    Working directory: ...\Tanneryd.BulkOperations.EF6.NET48.Tests

    EntityFramework6\Add-Migration <Name>
    EntityFramework6\Update-Database

  Automatic migrations are disabled (see Migrations\Configuration.cs).

  If the scaffolded migration cannot express what you need (computed columns,
  views), add raw Sql() in the migration Up()/Down() methods. See
  UpdateComputedColumns.cs for the pattern used in this project.

Resetting a broken local database
---------------------------------
  If a previous setup left the database in an inconsistent state, drop it once:

    DROP DATABASE [Tanneryd.BulkOperations.EF6.NET48.Tests.Models.EF.UnitTestContext];

  The next test run will recreate it by applying all migrations from scratch.
