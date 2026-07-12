using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Tanneryd.BulkOperations.EFCore.Tests;

internal static class TestDatabaseConnection
{
    public const string DefaultConnectionString =
        @"data source=(localdb)\MSSQLLocalDB;initial catalog=Tanneryd.BulkOperations.EFCore.Tests.Models.EF.UnitTestContext;persist security info=True;Integrated Security=SSPI;MultipleActiveResultSets=True;TrustServerCertificate=true";
}

public class MSSQLDesignTimeContextFactory : IDesignTimeDbContextFactory<UnitTestContext>
{
    public UnitTestContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<UnitTestContext>();
        optionsBuilder.UseSqlServer(TestDatabaseConnection.DefaultConnectionString);

        return new UnitTestContext(optionsBuilder.Options);
    }
}

public class MSSQLContextFactory : IDbContextFactory<UnitTestContext>
{
    public UnitTestContext CreateDbContext()
    {
        return CreateDbContext(TestDatabaseConnection.DefaultConnectionString);
    }

    public UnitTestContext CreateDbContext(string connectionString)
    {
        var contextOptions = new DbContextOptionsBuilder<UnitTestContext>()
            .UseSqlServer(connectionString)
            .ConfigureWarnings(warnings => warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;
        var context = new UnitTestContext(contextOptions);
        context.ChangeTracker.AutoDetectChangesEnabled = false;
        context.ChangeTracker.LazyLoadingEnabled = false;
        return context;
    }
}
