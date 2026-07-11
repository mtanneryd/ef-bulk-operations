using Microsoft.Data.SqlClient;
using System;

namespace Tanneryd.BulkOperations.Common.Sql
{
    internal static class TempTableSqlHelper
    {
        public static void Drop(SqlConnection connection, SqlTransaction transaction, string tempTableName)
        {
            var query = $@"IF OBJECT_ID('{tempTableName}') IS NOT NULL DROP TABLE {tempTableName}";
            using var cmd = SqlCommandFactory.Create(query, connection, transaction, TimeSpan.FromSeconds(30));
            cmd.ExecuteNonQuery();
        }

        public static void EnableIdentityInsert(string tableName, SqlConnection connection, SqlTransaction transaction)
        {
            var query = $@"SET IDENTITY_INSERT {tableName} ON";
            using var cmd = SqlCommandFactory.Create(query, connection, transaction, TimeSpan.FromSeconds(30));
            cmd.ExecuteNonQuery();
        }

        public static void DisableIdentityInsert(string tableName, SqlConnection connection, SqlTransaction transaction)
        {
            var query = $@"SET IDENTITY_INSERT {tableName} OFF";
            using var cmd = SqlCommandFactory.Create(query, connection, transaction, TimeSpan.FromSeconds(30));
            cmd.ExecuteNonQuery();
        }
    }
}
