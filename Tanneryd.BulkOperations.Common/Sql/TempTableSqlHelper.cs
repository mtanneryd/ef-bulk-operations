using Microsoft.Data.SqlClient;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Tanneryd.BulkOperations.Common.Sql
{
    /// <summary>
    /// Shared SQL helpers for session-scoped temp tables and IDENTITY_INSERT.
    /// Async-only so bulk paths do not nest sync-over-async on these helpers.
    /// </summary>
    internal static class TempTableSqlHelper
    {
        public static async Task DropAsync(
            SqlConnection connection,
            SqlTransaction transaction,
            string tempTableName,
            CancellationToken cancellationToken = default)
        {
            var query = $@"IF OBJECT_ID('{tempTableName}') IS NOT NULL DROP TABLE {tempTableName}";
            await ExecuteNonQueryAsync(query, connection, transaction, cancellationToken).ConfigureAwait(false);
            TempTableTracker.NotifyDropped();
        }

        /// <summary>
        /// Required when bulk-copying explicit identity values into a table
        /// (or temp table) that inherits identity metadata from the source.
        /// </summary>
        public static async Task EnableIdentityInsertAsync(
            string tableName,
            SqlConnection connection,
            SqlTransaction transaction,
            CancellationToken cancellationToken = default)
        {
            var query = $@"SET IDENTITY_INSERT {tableName} ON";
            await ExecuteNonQueryAsync(query, connection, transaction, cancellationToken).ConfigureAwait(false);
            IdentityInsertTracker.NotifyEnabled();
        }

        public static async Task DisableIdentityInsertAsync(
            string tableName,
            SqlConnection connection,
            SqlTransaction transaction,
            CancellationToken cancellationToken = default)
        {
            var query = $@"SET IDENTITY_INSERT {tableName} OFF";
            await ExecuteNonQueryAsync(query, connection, transaction, cancellationToken).ConfigureAwait(false);
            IdentityInsertTracker.NotifyDisabled();
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
