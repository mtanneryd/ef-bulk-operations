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
                CommandTimeout = ToCommandTimeoutSeconds(timeout)
            };
            Track(cmd);
            return cmd;
        }

        /// <summary>
        /// Converts a <see cref="TimeSpan"/> to an ADO.NET command timeout in whole
        /// seconds. Negative values are rejected up front (SqlCommand would otherwise
        /// throw a less helpful error later), values too large for an int (for example
        /// <see cref="TimeSpan.MaxValue"/>, which would overflow to a negative int)
        /// are clamped to <see cref="int.MaxValue"/>, and <see cref="TimeSpan.Zero"/>
        /// keeps its ADO.NET meaning of no timeout.
        /// </summary>
        public static int ToCommandTimeoutSeconds(TimeSpan timeout)
        {
            if (timeout < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(
                    nameof(timeout),
                    timeout,
                    "Command timeout must not be negative. Use TimeSpan.Zero for no timeout.");

            var totalSeconds = timeout.TotalSeconds;
            return totalSeconds >= int.MaxValue ? int.MaxValue : (int)totalSeconds;
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
