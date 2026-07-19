using Microsoft.Data.SqlClient;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Tanneryd.BulkOperations.Common.Sql
{
    /// <summary>
    /// Shared SQL helpers for session-scoped temp tables and IDENTITY_INSERT.
    /// Sync methods block with GetAwaiter().GetResult() for the legacy sync
    /// surface; prefer the Async variants from async call paths.
    /// </summary>
    internal static class TempTableSqlHelper
    {
        public static void Drop(SqlConnection connection, SqlTransaction transaction, string tempTableName)
        {
            DropAsync(connection, transaction, tempTableName).GetAwaiter().GetResult();
        }

        public static Task DropAsync(
            SqlConnection connection,
            SqlTransaction transaction,
            string tempTableName,
            CancellationToken cancellationToken = default)
        {
            var query = $@"IF OBJECT_ID('{tempTableName}') IS NOT NULL DROP TABLE {tempTableName}";
            return ExecuteNonQueryAsync(query, connection, transaction, cancellationToken);
        }

        /// <summary>
        /// Required when bulk-copying explicit identity values into a table
        /// (or temp table) that inherits identity metadata from the source.
        /// </summary>
        public static void EnableIdentityInsert(string tableName, SqlConnection connection, SqlTransaction transaction)
        {
            EnableIdentityInsertAsync(tableName, connection, transaction).GetAwaiter().GetResult();
        }

        public static Task EnableIdentityInsertAsync(
            string tableName,
            SqlConnection connection,
            SqlTransaction transaction,
            CancellationToken cancellationToken = default)
        {
            var query = $@"SET IDENTITY_INSERT {tableName} ON";
            return ExecuteNonQueryAsync(query, connection, transaction, cancellationToken);
        }

        public static void DisableIdentityInsert(string tableName, SqlConnection connection, SqlTransaction transaction)
        {
            DisableIdentityInsertAsync(tableName, connection, transaction).GetAwaiter().GetResult();
        }

        public static Task DisableIdentityInsertAsync(
            string tableName,
            SqlConnection connection,
            SqlTransaction transaction,
            CancellationToken cancellationToken = default)
        {
            var query = $@"SET IDENTITY_INSERT {tableName} OFF";
            return ExecuteNonQueryAsync(query, connection, transaction, cancellationToken);
        }

        private static async Task ExecuteNonQueryAsync(
            string query,
            SqlConnection connection,
            SqlTransaction transaction,
            CancellationToken cancellationToken)
        {
            using var cmd = SqlCommandFactory.Create(query, connection, transaction, TimeSpan.FromSeconds(30));
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
