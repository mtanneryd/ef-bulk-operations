using System;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Tanneryd.BulkOperations.EFCore.Tests;

public class MSSQLContextFactory : IDbContextFactory<UnitTestContext>
{
    private readonly string _connectionString = @"data source=.\SQLEXPRESS;initial catalog=Tanneryd.BulkOperations.EFCore.Tests.Models.EF.UnitTestContext;persist security info=True;Integrated Security=SSPI;MultipleActiveResultSets=True;TrustServerCertificate=true";

    public UnitTestContext CreateDbContext()
    {
        var contextOptions = new DbContextOptionsBuilder<UnitTestContext>()
            .UseSqlServer(_connectionString)
            .Options;
        var context = new UnitTestContext(contextOptions);
        context.ChangeTracker.AutoDetectChangesEnabled = false;
        context.ChangeTracker.LazyLoadingEnabled = false;
        return context;
    }
}