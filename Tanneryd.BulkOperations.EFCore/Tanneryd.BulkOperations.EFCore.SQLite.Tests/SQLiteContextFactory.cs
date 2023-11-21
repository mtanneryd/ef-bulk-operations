using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Tanneryd.BulkOperations.EFCore.SQLite.Tests
{
    public class SQLiteContextFactory : IDbContextFactory<UnitTestContext>, IDisposable
    {
        private readonly SqliteConnection _masterConnection;
        private readonly string _connectionString = "Data Source=UnitTestInMemoryDb;Mode=Memory;Cache=Shared";

        public SQLiteContextFactory()
        {
            _masterConnection = new SqliteConnection(_connectionString);
            _masterConnection.Open();

            Environment.SetEnvironmentVariable("UseSQLite", "true");

            var contextOptions = new DbContextOptionsBuilder<UnitTestContext>()
                .UseSqlite(_masterConnection)
                .Options;
            var context = new UnitTestContext(contextOptions);
            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();
        }

        public UnitTestContext CreateDbContext()
        {
            var connection = new SqliteConnection(_connectionString);
            connection.Open();
            var contextOptions = new DbContextOptionsBuilder<UnitTestContext>()
                .UseSqlite(connection)
                .Options;
            var context = new UnitTestContext(contextOptions);
            context.ChangeTracker.AutoDetectChangesEnabled = false;
            context.ChangeTracker.LazyLoadingEnabled = false;
            return context;
        }


        public void Dispose()
        {
            _masterConnection?.Dispose();
        }
    }
}
