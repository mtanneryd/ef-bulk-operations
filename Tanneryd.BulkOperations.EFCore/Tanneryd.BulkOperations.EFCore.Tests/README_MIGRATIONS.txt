EF Core test database migrations
================================

This project uses EF Core migrations to create and maintain the SQL Server
schema used by the integration tests.

Database
--------
  Server:   (localdb)\MSSQLLocalDB
  Catalog:  Tanneryd.BulkOperations.EFCore.Tests.Models.EF.UnitTestContext
  Config:   MSSQLContextFactory.cs / TestDatabaseConnection.DefaultConnectionString
  Design:   MSSQLDesignTimeContextFactory (for dotnet ef CLI)

Migrations (in order)
---------------------
  1. InitialMigration
     Scaffolds the full test schema, including Level1 (complex types) and
     LogItem with a TPH discriminator (LogType). Invoice.Tax and
     Instructor.FullName are created as ordinary columns because the model only
     marks them ValueGeneratedOnAddOrUpdate() without HasComputedColumnSql().

  2. UpdateComputedColumns
     Hand-written migrationBuilder.Sql() that:
       - Converts Invoice.Tax to (Gross - Net) PERSISTED
       - Converts Instructor.FullName to (FirstName + ' ' + LastName) PERSISTED
       - Creates the Contact view over Person (mapped via ContactConfiguration)

  UnitTestContextModelSnapshot.cs holds the current EF model state. EF Core
  compares the live model against this file when you run "dotnet ef migrations
  add". It is not executed against the database at runtime.

What happens when tests run
---------------------------
  Each test class calls BulkOperationTestBase.InitializeUnitTestContext() from
  [TestInitialize]. That method:

    1. Opens a context and calls Database.Migrate() to apply pending migrations
    2. Deletes all rows via CleanupUnitTestContext() (schema is kept)

  Migrations are idempotent: after the first run, Migrate() is a no-op unless
  a new migration has been added. Tests do not drop or recreate the database.

Creating a new migration (developer workflow)
---------------------------------------------
  From the repository root (or this project directory):

    dotnet ef migrations add <Name> \
      --project Tanneryd.BulkOperations.EFCore/Tanneryd.BulkOperations.EFCore.Tests \
      --context UnitTestContext

    dotnet ef database update \
      --project Tanneryd.BulkOperations.EFCore/Tanneryd.BulkOperations.EFCore.Tests \
      --context UnitTestContext

  If the scaffolded migration cannot express what you need (computed columns,
  views), edit the generated .cs file and add migrationBuilder.Sql() in Up()/
  Down(). See UpdateComputedColumns.cs for the pattern used in this project.

  To generate computed columns automatically in future migrations, configure
  HasComputedColumnSql(...) on the entity properties instead of only
  ValueGeneratedOnAddOrUpdate().

Resetting a broken local database
---------------------------------
  If a previous setup left the database in an inconsistent state, drop it once:

    DROP DATABASE [Tanneryd.BulkOperations.EFCore.Tests.Models.EF.UnitTestContext];

  The next test run will recreate it by applying all migrations from scratch.
