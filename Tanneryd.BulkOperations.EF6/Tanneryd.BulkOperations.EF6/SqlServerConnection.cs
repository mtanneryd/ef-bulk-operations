/*
 * Copyright ©  2017-2026 Tånneryd IT AB
 * Licensed under the Apache License, Version 2.0.
 */

using Microsoft.Data.SqlClient;
using System;
using System.Data;
using System.Data.Common;
using System.Data.Entity;
using System.Threading;
using System.Threading.Tasks;
using Tanneryd.BulkOperations.Common.Sql;

namespace Tanneryd.BulkOperations.EF6
{
    /// <summary>
    /// Facade over Microsoft.Data.SqlClient and System.Data.SqlClient connections
    /// (including EntityConnection store connections). Sync wrappers use
    /// ConfigureAwait(false).GetAwaiter().GetResult(). Microsoft.Data.SqlClient
    /// transactions cannot be used against a legacy System.Data.SqlClient connection.
    /// </summary>
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
            return ResolveAsync(ctx).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public static async Task<SqlServerConnection> ResolveAsync(
            DbContext ctx,
            CancellationToken cancellationToken = default)
        {
            if (ctx == null)
                throw new ArgumentNullException(nameof(ctx));

            var connection = ctx.Database.Connection;
            if (connection.State == ConnectionState.Closed)
            {
                if (connection is SqlConnection modernToOpen)
                    await modernToOpen.OpenAsync(cancellationToken).ConfigureAwait(false);
                else if (connection is System.Data.SqlClient.SqlConnection legacyToOpen)
                    await legacyToOpen.OpenAsync(cancellationToken).ConfigureAwait(false);
                else
                    connection.Open();
            }

            if (connection is SqlConnection modernConnection)
                return new SqlServerConnection(modernConnection);

            if (connection is System.Data.SqlClient.SqlConnection legacyConnection)
                return new SqlServerConnection(legacyConnection);

            if (connection is System.Data.Entity.Core.EntityClient.EntityConnection entityConnection)
            {
                var storeConnection = entityConnection.StoreConnection;
                if (storeConnection.State == ConnectionState.Closed)
                {
                    if (storeConnection is SqlConnection modernStoreToOpen)
                        await modernStoreToOpen.OpenAsync(cancellationToken).ConfigureAwait(false);
                    else if (storeConnection is System.Data.SqlClient.SqlConnection legacyStoreToOpen)
                        await legacyStoreToOpen.OpenAsync(cancellationToken).ConfigureAwait(false);
                    else
                        storeConnection.Open();
                }

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
            EnsureOpenAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public async Task EnsureOpenAsync(CancellationToken cancellationToken = default)
        {
            if (IsLegacy)
            {
                if (_legacyConnection.State == ConnectionState.Closed)
                    await _legacyConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }
            else if (_modernConnection.State == ConnectionState.Closed)
            {
                await _modernConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
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
            ExecuteNonQueryAsync(query, transaction, timeout).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public async Task ExecuteNonQueryAsync(
            string query,
            SqlTransaction transaction,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            using var cmd = CreateCommand(query, transaction, timeout);
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        public void DropTempTable(SqlTransaction transaction, string tempTableName)
        {
            DropTempTableAsync(transaction, tempTableName).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public async Task DropTempTableAsync(
            SqlTransaction transaction,
            string tempTableName,
            CancellationToken cancellationToken = default)
        {
            var query = $@"IF OBJECT_ID('{tempTableName}') IS NOT NULL DROP TABLE {tempTableName}";
            await ExecuteNonQueryAsync(query, transaction, TimeSpan.FromSeconds(30), cancellationToken)
                .ConfigureAwait(false);
            TempTableTracker.NotifyDropped();
        }

        public void EnableIdentityInsert(string tableName, SqlTransaction transaction)
        {
            EnableIdentityInsertAsync(tableName, transaction).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public Task EnableIdentityInsertAsync(
            string tableName,
            SqlTransaction transaction,
            CancellationToken cancellationToken = default)
        {
            return ExecuteNonQueryAsync(
                $@"SET IDENTITY_INSERT {tableName} ON",
                transaction,
                TimeSpan.FromSeconds(30),
                cancellationToken);
        }

        public void DisableIdentityInsert(string tableName, SqlTransaction transaction)
        {
            DisableIdentityInsertAsync(tableName, transaction).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public Task DisableIdentityInsertAsync(
            string tableName,
            SqlTransaction transaction,
            CancellationToken cancellationToken = default)
        {
            return ExecuteNonQueryAsync(
                $@"SET IDENTITY_INSERT {tableName} OFF",
                transaction,
                TimeSpan.FromSeconds(30),
                cancellationToken);
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
        private bool _disposed;

        private SqlServerCommand(SqlCommand modernCommand)
        {
            _modernCommand = modernCommand;
            SqlResourceTracker.NotifyCreated();
        }

        private SqlServerCommand(System.Data.SqlClient.SqlCommand legacyCommand)
        {
            _legacyCommand = legacyCommand;
            SqlResourceTracker.NotifyCreated();
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
            ExecuteNonQueryAsync().ConfigureAwait(false).GetAwaiter().GetResult();

        public Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken = default) =>
            IsLegacy
                ? _legacyCommand.ExecuteNonQueryAsync(cancellationToken)
                : _modernCommand.ExecuteNonQueryAsync(cancellationToken);

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
            ExecuteScalarAsync().ConfigureAwait(false).GetAwaiter().GetResult();

        public Task<object> ExecuteScalarAsync(CancellationToken cancellationToken = default) =>
            IsLegacy
                ? _legacyCommand.ExecuteScalarAsync(cancellationToken)
                : _modernCommand.ExecuteScalarAsync(cancellationToken);

        public DbDataReader ExecuteReader() =>
            ExecuteReaderAsync().ConfigureAwait(false).GetAwaiter().GetResult();

        public async Task<DbDataReader> ExecuteReaderAsync(CancellationToken cancellationToken = default)
        {
            if (IsLegacy)
                return await _legacyCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            return await _modernCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            if (IsLegacy)
                _legacyCommand.Dispose();
            else
                _modernCommand.Dispose();

            SqlResourceTracker.NotifyDisposed();
        }
    }

    internal sealed class SqlBulkCopySession : IDisposable
    {
        private readonly SqlBulkCopy _modernBulkCopy;
        private readonly System.Data.SqlClient.SqlBulkCopy _legacyBulkCopy;
        private bool _disposed;

        private SqlBulkCopySession(SqlBulkCopy modernBulkCopy)
        {
            _modernBulkCopy = modernBulkCopy;
            SqlResourceTracker.NotifyCreated();
        }

        private SqlBulkCopySession(System.Data.SqlClient.SqlBulkCopy legacyBulkCopy)
        {
            _legacyBulkCopy = legacyBulkCopy;
            SqlResourceTracker.NotifyCreated();
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
            WriteToServerAsync(table).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public Task WriteToServerAsync(DataTable table, CancellationToken cancellationToken = default) =>
            IsLegacy
                ? _legacyBulkCopy.WriteToServerAsync(table, cancellationToken)
                : _modernBulkCopy.WriteToServerAsync(table, cancellationToken);

        public void WriteToServer(DbDataReader reader)
        {
            WriteToServerAsync(reader).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public Task WriteToServerAsync(DbDataReader reader, CancellationToken cancellationToken = default) =>
            IsLegacy
                ? _legacyBulkCopy.WriteToServerAsync(reader, cancellationToken)
                : _modernBulkCopy.WriteToServerAsync(reader, cancellationToken);

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            if (IsLegacy)
                _legacyBulkCopy.Close();
            else
                _modernBulkCopy.Close();

            SqlResourceTracker.NotifyDisposed();
        }
    }
}
