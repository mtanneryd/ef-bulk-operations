using Microsoft.Data.SqlClient;

namespace Tanneryd.BulkOperations.Common.Sql
{
    /// <summary>
    /// Helpers for inspecting caller-provided <see cref="SqlTransaction"/> instances.
    /// </summary>
    internal static class SqlTransactionHelper
    {
        /// <summary>
        /// True when a transaction was provided but is no longer usable — committed,
        /// rolled back, or aborted by the server. SqlClient sets <c>Connection</c> to
        /// null in that state ("zombie" transaction); creating or executing commands
        /// on it throws <see cref="System.InvalidOperationException"/>.
        /// </summary>
        public static bool IsZombied(SqlTransaction transaction)
        {
            return transaction != null && transaction.Connection == null;
        }
    }
}
