/*
 * Copyright ©  2017-2025 Tånneryd IT AB
 * Licensed under the Apache License, Version 2.0.
 */

using Microsoft.Data.SqlClient;
using System;
using System.Data;
using System.Data.Common;
using System.Data.Entity;

namespace Tanneryd.BulkOperations.EF6
{
    internal sealed class SqlServerConnection
    {
        private readonly SqlConnection _modernConnection;
        private readonly System.Data.SqlClient.SqlConnection _legacyConnection;

        private SqlServerConnection(SqlConnection modernConnection)
        {
            _modernConnection = modernConnection ?? throw new ArgumentNullException(nameof(modernConnection));
        }

        private SqlServerConnection(System.Data.SqlClient.SqlConnection legacyConnection)
        {
            _legacyConnection = legacyConnection ?? throw new ArgumentNullException(nameof(legacyConnection));
        }

        public bool IsLegacy => _legacyConnection != null;

        public string ConnectionString =>
            IsLegacy ? _legacyConnection.ConnectionString : _modernConnection.ConnectionString;

        public static SqlServerConnection Resolve(DbContext ctx)
        {
            if (ctx == null)
                throw new ArgumentNullException(nameof(ctx));

            var connection = ctx.Database.Connection;
            if (connection.State == ConnectionState.Closed)
                connection.Open();

            if (connection is SqlConnection modernConnection)
                return new SqlServerConnection(modernConnection);

            if (connection is System.Data.SqlClient.SqlConnection legacyConnection)
                return new SqlServerConnection(legacyConnection);

            if (connection is System.Data.Entity.Core.EntityClient.EntityConnection entityConnection)
            {
                var storeConnection = entityConnection.StoreConnection;
                if (storeConnection.State == ConnectionState.Closed)
                    storeConnection.Open();

                if (storeConnection is SqlConnection modernStoreConnection)
                    return new SqlServerConnection(modernStoreConnection);

                if (storeConnection is System.Data.SqlClient.SqlConnection legacyStoreConnection)
                    return new SqlServerConnection(legacyStoreConnection);
            }

            throw new NotSupportedException(
                $"Bulk operations require a SQL Server connection. Actual connection type: {connection.GetType().FullName}.");
        }

        public void EnsureOpen()
        {
            if (IsLegacy)
            {
                if (_legacyConnection.State == ConnectionState.Closed)
                    _legacyConnection.Open();
            }
            else if (_modernConnection.State == ConnectionState.Closed)
            {
                _modernConnection.Open();
            }
        }

        public SqlConnection AsModernConnection()
        {
            if (IsLegacy)
            {
                throw new NotSupportedException(
                    "GetSqlConnection requires the EF6 context to use the Microsoft.Data.SqlClient provider. " +
                    "Configure providerName=\"Microsoft.Data.SqlClient\" and register Microsoft.EntityFramework.SqlServer.");
            }

            EnsureOpen();
            return _modernConnection;
        }

        public SqlServerCommand CreateCommand(
            string query = null,
            SqlTransaction transaction = null,
            TimeSpan? timeout = null)
        {
            EnsureOpen();
            ValidateTransaction(transaction);

            if (IsLegacy)
                return SqlServerCommand.FromLegacy(_legacyConnection, query, null, timeout);

            return SqlServerCommand.FromModern(_modernConnection, query, transaction, timeout);
        }

        public SqlBulkCopySession CreateBulkCopy(
            SqlBulkCopyOptions options,
            SqlTransaction transaction,
            string destinationTableName)
        {
            EnsureOpen();
            ValidateTransaction(transaction);

            if (IsLegacy)
            {
                return SqlBulkCopySession.FromLegacy(
                    _legacyConnection,
                    options,
                    null,
                    destinationTableName);
            }

            return SqlBulkCopySession.FromModern(
                _modernConnection,
                options,
                transaction,
                destinationTableName);
        }

        public void ExecuteNonQuery(string query, SqlTransaction transaction, TimeSpan timeout)
        {
            using var cmd = CreateCommand(query, transaction, timeout);
            cmd.ExecuteNonQuery();
        }

        public void DropTempTable(SqlTransaction transaction, string tempTableName)
        {
            var query = $@"IF OBJECT_ID('{tempTableName}') IS NOT NULL DROP TABLE {tempTableName}";
            ExecuteNonQuery(query, transaction, TimeSpan.FromSeconds(30));
        }

        public void EnableIdentityInsert(string tableName, SqlTransaction transaction)
        {
            ExecuteNonQuery($@"SET IDENTITY_INSERT {tableName} ON", transaction, TimeSpan.FromSeconds(30));
        }

        public void DisableIdentityInsert(string tableName, SqlTransaction transaction)
        {
            ExecuteNonQuery($@"SET IDENTITY_INSERT {tableName} OFF", transaction, TimeSpan.FromSeconds(30));
        }

        private void ValidateTransaction(SqlTransaction transaction)
        {
            if (transaction != null && IsLegacy)
            {
                throw new NotSupportedException(
                    "A Microsoft.Data.SqlClient.SqlTransaction cannot be used when the EF6 context uses the legacy " +
                    "System.Data.SqlClient provider. Pass null for Transaction or migrate to Microsoft.Data.SqlClient.");
            }
        }
    }

    internal sealed class SqlServerCommand : IDisposable
    {
        private readonly SqlCommand _modernCommand;
        private readonly System.Data.SqlClient.SqlCommand _legacyCommand;

        private SqlServerCommand(SqlCommand modernCommand)
        {
            _modernCommand = modernCommand;
        }

        private SqlServerCommand(System.Data.SqlClient.SqlCommand legacyCommand)
        {
            _legacyCommand = legacyCommand;
        }

        public static SqlServerCommand FromModern(
            SqlConnection connection,
            string query,
            SqlTransaction transaction,
            TimeSpan? timeout)
        {
            var command = new SqlCommand(query, connection, transaction);
            if (timeout.HasValue)
                command.CommandTimeout = (int)timeout.Value.TotalSeconds;

            return new SqlServerCommand(command);
        }

        public static SqlServerCommand FromLegacy(
            System.Data.SqlClient.SqlConnection connection,
            string query,
            System.Data.SqlClient.SqlTransaction transaction,
            TimeSpan? timeout)
        {
            var command = new System.Data.SqlClient.SqlCommand(query, connection, transaction);
            if (timeout.HasValue)
                command.CommandTimeout = (int)timeout.Value.TotalSeconds;

            return new SqlServerCommand(command);
        }

        public string CommandText
        {
            get => IsLegacy ? _legacyCommand.CommandText : _modernCommand.CommandText;
            set
            {
                if (IsLegacy)
                    _legacyCommand.CommandText = value;
                else
                    _modernCommand.CommandText = value;
            }
        }

        public int CommandTimeout
        {
            get => IsLegacy ? _legacyCommand.CommandTimeout : _modernCommand.CommandTimeout;
            set
            {
                if (IsLegacy)
                    _legacyCommand.CommandTimeout = value;
                else
                    _modernCommand.CommandTimeout = value;
            }
        }

        public SqlTransaction Transaction
        {
            set
            {
                if (IsLegacy)
                {
                    if (value != null)
                    {
                        throw new NotSupportedException(
                            "A Microsoft.Data.SqlClient.SqlTransaction cannot be used when the EF6 context uses the legacy " +
                            "System.Data.SqlClient provider. Pass null for Transaction or migrate to Microsoft.Data.SqlClient.");
                    }

                    return;
                }

                _modernCommand.Transaction = value;
            }
        }

        private bool IsLegacy => _legacyCommand != null;

        public int ExecuteNonQuery() =>
            IsLegacy ? _legacyCommand.ExecuteNonQuery() : _modernCommand.ExecuteNonQuery();

        public void AddParameter(SqlParameter parameter)
        {
            if (parameter == null)
                throw new ArgumentNullException(nameof(parameter));

            if (IsLegacy)
            {
                _legacyCommand.Parameters.Add(new System.Data.SqlClient.SqlParameter(
                    parameter.ParameterName,
                    parameter.SqlDbType)
                {
                    Value = parameter.Value ?? DBNull.Value,
                    Size = parameter.Size,
                    Precision = parameter.Precision,
                    Scale = parameter.Scale
                });
            }
            else
            {
                _modernCommand.Parameters.Add(parameter);
            }
        }

        public object ExecuteScalar() =>
            IsLegacy ? _legacyCommand.ExecuteScalar() : _modernCommand.ExecuteScalar();

        public DbDataReader ExecuteReader() =>
            IsLegacy ? _legacyCommand.ExecuteReader() : _modernCommand.ExecuteReader();

        public void Dispose()
        {
            if (IsLegacy)
                _legacyCommand.Dispose();
            else
                _modernCommand.Dispose();
        }
    }

    internal sealed class SqlBulkCopySession : IDisposable
    {
        private readonly SqlBulkCopy _modernBulkCopy;
        private readonly System.Data.SqlClient.SqlBulkCopy _legacyBulkCopy;

        private SqlBulkCopySession(SqlBulkCopy modernBulkCopy)
        {
            _modernBulkCopy = modernBulkCopy;
        }

        private SqlBulkCopySession(System.Data.SqlClient.SqlBulkCopy legacyBulkCopy)
        {
            _legacyBulkCopy = legacyBulkCopy;
        }

        public static SqlBulkCopySession FromModern(
            SqlConnection connection,
            SqlBulkCopyOptions options,
            SqlTransaction transaction,
            string destinationTableName)
        {
            var bulkCopy = new SqlBulkCopy(connection, options, transaction)
            {
                DestinationTableName = destinationTableName
            };

            return new SqlBulkCopySession(bulkCopy);
        }

        public static SqlBulkCopySession FromLegacy(
            System.Data.SqlClient.SqlConnection connection,
            SqlBulkCopyOptions options,
            System.Data.SqlClient.SqlTransaction transaction,
            string destinationTableName)
        {
            var legacyOptions = (System.Data.SqlClient.SqlBulkCopyOptions)options;
            var bulkCopy = new System.Data.SqlClient.SqlBulkCopy(connection, legacyOptions, transaction)
            {
                DestinationTableName = destinationTableName
            };

            return new SqlBulkCopySession(bulkCopy);
        }

        public bool EnableStreaming
        {
            set
            {
                if (IsLegacy)
                    _legacyBulkCopy.EnableStreaming = value;
                else
                    _modernBulkCopy.EnableStreaming = value;
            }
        }

        public int BatchSize
        {
            set
            {
                if (IsLegacy)
                    _legacyBulkCopy.BatchSize = value;
                else
                    _modernBulkCopy.BatchSize = value;
            }
        }

        public int BulkCopyTimeout
        {
            set
            {
                if (IsLegacy)
                    _legacyBulkCopy.BulkCopyTimeout = value;
                else
                    _modernBulkCopy.BulkCopyTimeout = value;
            }
        }

        private bool IsLegacy => _legacyBulkCopy != null;

        public void AddColumnMapping(string sourceColumn, string destinationColumn)
        {
            if (IsLegacy)
                _legacyBulkCopy.ColumnMappings.Add(
                    new System.Data.SqlClient.SqlBulkCopyColumnMapping(sourceColumn, destinationColumn));
            else
                _modernBulkCopy.ColumnMappings.Add(new SqlBulkCopyColumnMapping(sourceColumn, destinationColumn));
        }

        public void WriteToServer(DataTable table)
        {
            if (IsLegacy)
                _legacyBulkCopy.WriteToServer(table);
            else
                _modernBulkCopy.WriteToServer(table);
        }

        public void WriteToServer(DbDataReader reader)
        {
            if (IsLegacy)
                _legacyBulkCopy.WriteToServer(reader);
            else
                _modernBulkCopy.WriteToServer(reader);
        }

        public void Dispose()
        {
            if (IsLegacy)
                _legacyBulkCopy.Close();
            else
                _modernBulkCopy.Close();
        }
    }
}
