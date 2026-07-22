using System.Data.Entity;

namespace Tanneryd.BulkOperations.EF6.NET48.Tests.Models.EF
{
    /// <summary>
    /// Same model/catalog as <see cref="UnitTestContext"/> but uses the legacy
    /// System.Data.SqlClient provider (connection string name = type name).
    /// Used to exercise IsLegacy paths such as concurrency BulkUpdate (H1).
    /// </summary>
    public class LegacyUnitTestContext : UnitTestContext
    {
        static LegacyUnitTestContext()
        {
            Database.SetInitializer<LegacyUnitTestContext>(null);
        }
    }
}
