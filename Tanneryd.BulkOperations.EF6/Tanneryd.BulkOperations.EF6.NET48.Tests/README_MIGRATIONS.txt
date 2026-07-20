EF6 test database migrations
=============================

This project uses Code First migrations to create and maintain the SQL Server
schema used by the integration tests.

Database
--------
  Server:   (localdb)\MSSQLLocalDB
  Catalog:  Tanneryd.BulkOperations.EF6.NET48.Tests.Models.EF.UnitTestContext
  Config:   app.config connection string "UnitTestContext"

Migrations
----------
  InitialCreate only.

  Creates the full test schema (including ConcurrencyItem with a rowversion
  column), then applies SQL Server-specific computed columns for Invoice.Tax
  and Instructor.FullName, and creates the Contact view over Person.

What happens when tests run
---------------------------
  Each test class calls BulkOperationTestBase.InitializeUnitTestContext() from
  [TestInitialize]. That method:

    1. Disables the database initializer (SetInitializer(null))
    2. Runs DbMigrator.Update() to apply any pending migrations
    3. Ensures dbo.Contact is a view (drops a stray Contact table if present)
    4. Deletes all rows via CleanupUnitTestContext() (schema is kept)

  When the model or InitialCreate changes, drop the local test database and let
  the next test run recreate it:

    DROP DATABASE [Tanneryd.BulkOperations.EF6.NET48.Tests.Models.EF.UnitTestContext];

Creating a new migration (developer workflow)
---------------------------------------------
  Prefer editing InitialCreate (and its .resx model Target) rather than stacking
  follow-up migrations for this test project. After model changes, drop the
  local database as above.
