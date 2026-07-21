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

Migrations
----------
  InitialMigration only.

  Scaffolds the full test schema (including Level1 complex types, ConcurrencyItem
  with a rowversion concurrency token, and LogItem TPH), then applies SQL
  Server-specific computed columns for Invoice.Tax and Instructor.FullName, and
  creates the Contact view over Person.

  UnitTestContextModelSnapshot.cs holds the current EF model state.

What happens when tests run
---------------------------
  Each test class calls BulkOperationTestBase.InitializeUnitTestContext() from
  [TestInitialize]. That method:

    1. Opens a context and calls Database.Migrate() to apply pending migrations
    2. Deletes all rows via CleanupUnitTestContext() (schema is kept)

  When the model or InitialMigration changes, drop the local test database and
  let the next test run recreate it:

    DROP DATABASE [Tanneryd.BulkOperations.EFCore.Tests.Models.EF.UnitTestContext];

Creating a new migration (developer workflow)
---------------------------------------------
  Prefer editing InitialMigration (and the model snapshot / Designer) rather than
  stacking follow-up migrations for this test project. After model changes, drop
  the local database as above.

  Keep the existing migration id/filename stable:

    20250330182347_InitialMigration.cs
    20250330182347_InitialMigration.Designer.cs

  Do not let `dotnet ef migrations add` leave a new timestamped migration in the
  tree — that shows up as delete+add in git and obscures the schema diff. If you
  scaffold a temporary migration to generate code, fold its Up/Down and snapshot
  changes into InitialMigration, keep
  `[Migration("20250330182347_InitialMigration")]`, then delete the temporary
  migration files.

  From the repository root (optional scaffold aid):

    dotnet ef migrations add <TempName> \
      --project Tanneryd.BulkOperations.EFCore/Tanneryd.BulkOperations.EFCore.Tests \
      --context UnitTestContext

  Then fold the generated changes into InitialMigration and remove <TempName>.
