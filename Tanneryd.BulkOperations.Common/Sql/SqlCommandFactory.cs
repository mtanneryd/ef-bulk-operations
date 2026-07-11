using Microsoft.Data.SqlClient;
using System;

namespace Tanneryd.BulkOperations.Common.Sql
{
    internal static class SqlCommandFactory
    {
        public static SqlCommand Create(
            string query,
            SqlConnection connection,
            SqlTransaction transaction,
            TimeSpan timeout)
        {
            var cmd = new SqlCommand(query, connection, transaction);
            cmd.CommandTimeout = (int)timeout.TotalSeconds;
            return cmd;
        }
    }
}
