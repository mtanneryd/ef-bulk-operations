using Microsoft.Data.SqlClient;
using System;

namespace Tanneryd.BulkOperations.Common.Sql
{
    /// <summary>
    /// Creates <see cref="SqlCommand"/> instances with a consistent timeout conversion
    /// and optional create/dispose reporting via <see cref="SqlResourceTracker"/>.
    /// </summary>
    internal static class SqlCommandFactory
    {
        public static SqlCommand Create(
            string query,
            SqlConnection connection,
            SqlTransaction transaction,
            TimeSpan timeout)
        {
            var cmd = new SqlCommand(query ?? string.Empty, connection, transaction)
            {
                CommandTimeout = (int)timeout.TotalSeconds
            };
            Track(cmd);
            return cmd;
        }

        public static SqlCommand Create(
            SqlConnection connection,
            SqlTransaction transaction,
            TimeSpan timeout)
        {
            return Create(string.Empty, connection, transaction, timeout);
        }

        /// <summary>
        /// Registers create/dispose notifications for an existing command.
        /// </summary>
        public static SqlCommand Track(SqlCommand command)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            SqlResourceTracker.NotifyCreated();
            command.Disposed += (_, _) => SqlResourceTracker.NotifyDisposed();
            return command;
        }
    }
}
