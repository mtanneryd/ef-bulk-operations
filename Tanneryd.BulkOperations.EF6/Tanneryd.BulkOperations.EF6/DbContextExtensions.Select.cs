/*
 * Copyright ©  2017-2026 Tånneryd IT AB
 * Licensed under the Apache License, Version 2.0.
 */

using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tanneryd.BulkOperations.EF6.Model;

namespace Tanneryd.BulkOperations.EF6
{
    public static partial class DbContextExtensions
    {
        private static IList<T1> DoBulkSelectNotExisting<T1, T2>(DbContext ctx, BulkSelectRequest<T1> request)
        {
            return DoBulkSelectNotExistingAsync<T1, T2>(ctx, request).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        private static async Task<IList<T1>> DoBulkSelectNotExistingAsync<T1, T2>(
            DbContext ctx,
            BulkSelectRequest<T1> request,
            CancellationToken cancellationToken = default)
        {
            if (!request.Items.Any()) return new List<T1>();

            Type t = typeof(T2);
            var mappings = MappingExtractor.GetMappings(ctx, t);
            var tableName = mappings.TableName;
            var columnMappings = mappings.ColumnMappingByPropertyName;
            var itemPropertByEntityProperty =
                request.KeyPropertyMappings.ToDictionary(p => p.EntityPropertyName, p => p.ItemPropertyName);
            var items = request.Items;
            var conn = await ResolveSqlConnectionAsync(ctx, cancellationToken).ConfigureAwait(false);

            if (!request.KeyPropertyMappings.Any())
            {
                throw new ArgumentException(
                    "The KeyPropertyMappings request property must be set and contain at least one name.");
            }

            var keyMappings = columnMappings.Values
                .Where(m => request.KeyPropertyMappings.Any(kpm => kpm.EntityPropertyName == m.EntityProperty.Name))
                .ToDictionary(m => m.EntityProperty.Name, m => m);

            if (keyMappings.Any())
            {
                var containsIdentityKey = keyMappings.Any(m =>
                    (m.Value.TableColumn.IsStoreGeneratedIdentity &&
                     m.Value.TableColumn.TypeName != "uniqueidentifier") ||
                    m.Value.TableColumn.IsStoreGeneratedComputed);

                // Include rowno even when unused: on some hosts WriteToServer
                // does nothing if the temp table has no rowno column.
                var tempTableName = await CreateTempTableAsync(
                    conn,
                    request.Transaction,
                    tableName,
                    mappings.Discriminator,
                    keyMappings.Select(m => m.Value.TableColumn.Name).ToArray(),
                    IncludeRowNumber.Yes,
                    cancellationToken).ConfigureAwait(false);

                var keyProperties = GetProperties(t)
                    .Where(p => keyMappings.ContainsKey(p.Name)).ToArray();

                var table = new DataTable();
                var bulkCopy = CreateBulkCopy(
                    table,
                    keyProperties,
                    keyMappings,
                    conn,
                    request.Transaction,
                    tempTableName,
                    mappings.Discriminator,
                    containsIdentityKey ? SqlBulkCopyOptions.KeepIdentity : SqlBulkCopyOptions.Default,
                    IncludeRowNumber.Yes);
                if (containsIdentityKey)
                    await EnableIdentityInsertAsync(tempTableName, conn, request.Transaction, cancellationToken).ConfigureAwait(false);

                int i = 0;
                var type = items[0].GetType();
                foreach (var entity in items)
                {
                    var e = entity;
                    var columnValues = new List<dynamic>();
                    columnValues.AddRange(keyProperties.Select(p =>
                        GetProperty(type, itemPropertByEntityProperty[p.Name], e, DBNull.Value)));
                    columnValues.Add(i++);
                    table.Rows.Add(columnValues.ToArray());
                }

                await bulkCopy.WriteToServerAsync(table.CreateDataReader(), cancellationToken).ConfigureAwait(false);

                var conditionStatements = keyMappings.Values.Select(c =>
                {
                    var keyProperty = keyProperties.Single(p => p.Name == c.EntityProperty.Name);
                    return
                        $"([t1].[{c.TableColumn.Name}] = [t2].[{c.TableColumn.Name}] OR ([t1].[{c.TableColumn.Name}] IS NULL AND [t2].[{c.TableColumn.Name}] IS NULL))";
                });

                var conditionStatementsSql = string.Join(" AND ", conditionStatements);
                var query = $@"SELECT DISTINCT [t0].[rowno] 
                               FROM {tempTableName} AS [t0]
                               EXCEPT
                               SELECT DISTINCT [t1].[rowno] 
                               FROM {tempTableName} AS [t1]
                               INNER JOIN {tableName.Fullname} AS [t2] ON {conditionStatementsSql}";

                var cmd = CreateSqlCommand(query, conn, request.Transaction, request.CommandTimeout);

                var existingEntities = new List<T1>();
                using (var sqlDataReader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                {
                    while (await sqlDataReader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        var rowNo = (int)sqlDataReader[0];
                        existingEntities.Add(items[rowNo]);
                    }
                }

                await DropTempTableAsync(conn, request.Transaction, tempTableName, cancellationToken).ConfigureAwait(false);

                return existingEntities;
            }

            return new List<T1>();
        }

        private static void DoBulkDeleteNotExisting<T1, T2>(DbContext ctx, BulkDeleteRequest<T1> request)
        {
            DoBulkDeleteNotExistingAsync<T1, T2>(ctx, request).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        private static async Task DoBulkDeleteNotExistingAsync<T1, T2>(
            DbContext ctx,
            BulkDeleteRequest<T1> request,
            CancellationToken cancellationToken = default)
        {
            Type t = typeof(T2);
            var mappings = MappingExtractor.GetMappings(ctx, t);
            var tableName = mappings.TableName;
            var columnMappings = mappings.ColumnMappingByPropertyName;
            var itemPropertyByEntityProperty =
                request.KeyPropertyMappings.ToDictionary(p => p.EntityPropertyName, p => p.ItemPropertyName);
            var items = request.Items;
            var conn = await ResolveSqlConnectionAsync(ctx, cancellationToken).ConfigureAwait(false);

            if (!itemPropertyByEntityProperty.Any())
            {
                throw new ArgumentException(
                    "The KeyPropertyMappings request property must be set and contain at least one name.");
            }

            var keyMappings = columnMappings.Values
                .Where(m => request.KeyPropertyMappings.Any(kpm => kpm.EntityPropertyName == m.EntityProperty.Name))
                .ToDictionary(m => m.EntityProperty.Name, m => m);

            if (keyMappings.Any())
            {
                var containsIdentityKey = keyMappings.Any(m =>
                    m.Value.TableColumn.IsStoreGeneratedIdentity &&
                    m.Value.TableColumn.TypeName != "uniqueidentifier");

                // Include rowno even when unused: on some hosts WriteToServer
                // does nothing if the temp table has no rowno column.
                var tempTableName = await CreateTempTableAsync(
                    conn,
                    request.Transaction,
                    tableName,
                    mappings.Discriminator,
                    keyMappings.Select(m => m.Value.TableColumn.Name).ToArray(),
                    IncludeRowNumber.Yes,
                    cancellationToken).ConfigureAwait(false);

                var properties = GetProperties(t);
                var keyProperties = properties
                    .Where(p => keyMappings.ContainsKey(p.Name)).ToArray();

                var table = new DataTable();
                var bulkCopy = CreateBulkCopy(
                    table,
                    keyProperties,
                    keyMappings,
                    conn,
                    request.Transaction,
                    tempTableName,
                    mappings.Discriminator,
                    containsIdentityKey ? SqlBulkCopyOptions.KeepIdentity : SqlBulkCopyOptions.Default,
                    IncludeRowNumber.Yes);
                if (containsIdentityKey)
                    await EnableIdentityInsertAsync(tempTableName, conn, request.Transaction, cancellationToken).ConfigureAwait(false);

                int i = 0;
                var type = typeof(T1);
                foreach (var entity in items)
                {
                    var e = entity;
                    var columnValues = new List<dynamic>();
                    columnValues.AddRange(keyProperties.Select(p =>
                        GetProperty(type, itemPropertyByEntityProperty[p.Name], e, DBNull.Value)));
                    columnValues.Add(i++);
                    table.Rows.Add(columnValues.ToArray());
                }

                await bulkCopy.WriteToServerAsync(table.CreateDataReader(), cancellationToken).ConfigureAwait(false);

                var parameters = new List<SqlParameter>();
                var condStatementsSql = BuildParameterizedSqlConditions(
                    request.SqlConditions,
                    mappings,
                    "t0",
                    parameters,
                    "deleteCond");
                var conditionStatements = keyMappings.Values.Select(c =>
                {
                    return
                        $"([t0].[{c.TableColumn.Name}] = [t1].[{c.TableColumn.Name}] OR ([t0].[{c.TableColumn.Name}] IS NULL AND [t1].[{c.TableColumn.Name}] IS NULL))";
                });

                var conditionStatementsSql = string.Join(" AND ", conditionStatements);
                var query = $@"DELETE {tableName.Fullname}
                               FROM  {tableName.Fullname} AS [t0]
                               WHERE {condStatementsSql}
                                AND NOT EXISTS (
                                SELECT NULL
                                FROM {tempTableName} AS [t1]
                                WHERE {conditionStatementsSql}
                               )";

                var cmd = CreateSqlCommand(query, conn, request.Transaction, request.CommandTimeout);
                foreach (var parameter in parameters)
                    cmd.AddParameter(parameter);
                await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

                await DropTempTableAsync(conn, request.Transaction, tempTableName, cancellationToken).ConfigureAwait(false);
            }
        }

        private static IList<T2> DoBulkSelect<T1, T2>(DbContext ctx, BulkSelectRequest<T1> request) where T2 : new()
        {
            return DoBulkSelectAsync<T1, T2>(ctx, request).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        private static async Task<IList<T2>> DoBulkSelectAsync<T1, T2>(
            DbContext ctx,
            BulkSelectRequest<T1> request,
            CancellationToken cancellationToken = default) where T2 : new()
        {
            if (!request.Items.Any()) return new List<T2>();

            Type t = typeof(T2);
            var mappings = MappingExtractor.GetMappings(ctx, t);
            var tableName = mappings.TableName;
            var columnMappings = mappings.ColumnMappingByPropertyName;
            var itemPropertByEntityProperty =
                request.KeyPropertyMappings.ToDictionary(p => p.EntityPropertyName, p => p.ItemPropertyName);
            var items = request.Items;
            var conn = await ResolveSqlConnectionAsync(ctx, cancellationToken).ConfigureAwait(false);

            if (!itemPropertByEntityProperty.Any())
            {
                throw new ArgumentException(
                    "The KeyPropertyMappings request property must be set and contain at least one name.");
            }

            var keyMappings = columnMappings.Values
                .Where(m => request.KeyPropertyMappings.Any(kpm => kpm.EntityPropertyName == m.EntityProperty.Name))
                .ToDictionary(m => m.EntityProperty.Name, m => m);

            if (keyMappings.Any())
            {
                var containsIdentityKey = keyMappings.Any(m =>
                    (m.Value.TableColumn.IsStoreGeneratedIdentity &&
                     m.Value.TableColumn.TypeName != "uniqueidentifier") ||
                    m.Value.TableColumn.IsStoreGeneratedComputed);

                // Include rowno even when unused: on some hosts WriteToServer
                // does nothing if the temp table has no rowno column.
                var tempTableName = await CreateTempTableAsync(
                    conn,
                    request.Transaction,
                    tableName,
                    mappings.Discriminator,
                    keyMappings.Select(m => m.Value.TableColumn.Name).ToArray(),
                    IncludeRowNumber.Yes,
                    cancellationToken).ConfigureAwait(false);

                var properties = GetProperties(t);
                var keyProperties = properties
                    .Where(p => keyMappings.ContainsKey(p.Name)).ToArray();

                var table = new DataTable();
                var bulkCopy = CreateBulkCopy(
                    table,
                    keyProperties,
                    keyMappings,
                    conn,
                    request.Transaction,
                    tempTableName,
                    mappings.Discriminator,
                    containsIdentityKey ? SqlBulkCopyOptions.KeepIdentity : SqlBulkCopyOptions.Default,
                    IncludeRowNumber.Yes);
                if (containsIdentityKey)
                    await EnableIdentityInsertAsync(tempTableName, conn, request.Transaction, cancellationToken).ConfigureAwait(false);

                int i = 0;
                var type = items[0].GetType();
                foreach (var entity in items)
                {
                    var e = entity;
                    var columnValues = new List<dynamic>();
                    columnValues.AddRange(keyProperties.Select(p =>
                        GetProperty(type, itemPropertByEntityProperty[p.Name], e, DBNull.Value)));
                    columnValues.Add(i++);
                    table.Rows.Add(columnValues.ToArray());
                }

                await bulkCopy.WriteToServerAsync(table.CreateDataReader(), cancellationToken).ConfigureAwait(false);

                var conditionStatements =
                    keyMappings.Values.Select(c => $"t0.[{c.TableColumn.Name}] = t1.[{c.TableColumn.Name}]");
                var conditionStatementsSql = string.Join(" AND ", conditionStatements);
                var query = $@"SELECT [t0].*
                               FROM {tableName.Fullname} AS [t0]
                               INNER JOIN {tempTableName} AS [t1] ON {conditionStatementsSql}
                               ORDER BY [t1].rowno ASC";

                var cmd = CreateSqlCommand(query, conn, request.Transaction, request.CommandTimeout);

                var selectedEntities = new List<T2>();
                using (var sqlDataReader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                {
                    while (await sqlDataReader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        var t2 = new T2();
                        selectedEntities.Add(t2);
                        foreach (var property in properties)
                        {
                            if (!columnMappings.ContainsKey(property.Name)) continue;
                            var mapping = columnMappings[property.Name];
                            var val = sqlDataReader[mapping.TableColumn.Name];
                            SetProperty(property, t2, val);
                        }
                    }
                }

                await DropTempTableAsync(conn, request.Transaction, tempTableName, cancellationToken).ConfigureAwait(false);

                return selectedEntities;
            }

            return new List<T2>();
        }

        private static IList<T1> DoBulkSelectExisting<T1, T2>(DbContext ctx, BulkSelectRequest<T1> request)
        {
            return DoBulkSelectExistingAsync<T1, T2>(ctx, request).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        private static async Task<IList<T1>> DoBulkSelectExistingAsync<T1, T2>(
            DbContext ctx,
            BulkSelectRequest<T1> request,
            CancellationToken cancellationToken = default)
        {
            if (!request.Items.Any()) return new List<T1>();

            Type t = typeof(T2);
            var mappings = MappingExtractor.GetMappings(ctx, t);
            var tableName = mappings.TableName;
            var columnMappings = mappings.ColumnMappingByPropertyName;
            var itemPropertyByEntityProperty =
                request.KeyPropertyMappings.ToDictionary(p => p.EntityPropertyName, p => p.ItemPropertyName);
            var items = request.Items;
            var conn = await ResolveSqlConnectionAsync(ctx, cancellationToken).ConfigureAwait(false);

            if (!request.KeyPropertyMappings.Any())
            {
                throw new ArgumentException(
                    "The KeyPropertyMappings request property must be set and contain at least one name.");
            }

            var keyMappings = columnMappings.Values
                .Where(m => request.KeyPropertyMappings.Any(kpm => kpm.EntityPropertyName == m.EntityProperty.Name))
                .ToDictionary(m => m.EntityProperty.Name, m => m);

            if (keyMappings.Any())
            {
                var containsIdentityKey = keyMappings.Any(m =>
                    m.Value.TableColumn.IsStoreGeneratedIdentity &&
                    m.Value.TableColumn.TypeName != "uniqueidentifier");

                // Include rowno even when unused: on some hosts WriteToServer
                // does nothing if the temp table has no rowno column.
                var tempTableName = await CreateTempTableAsync(
                    conn,
                    request.Transaction,
                    tableName,
                    mappings.Discriminator,
                    keyMappings.Select(m => m.Value.TableColumn.Name).ToArray(),
                    IncludeRowNumber.Yes,
                    cancellationToken).ConfigureAwait(false);

                var keyProperties = GetProperties(t)
                    .Where(p => keyMappings.ContainsKey(p.Name)).ToArray();

                var table = new DataTable();
                var bulkCopy = CreateBulkCopy(
                    table,
                    keyProperties,
                    keyMappings,
                    conn,
                    request.Transaction,
                    tempTableName,
                    mappings.Discriminator,
                    containsIdentityKey ? SqlBulkCopyOptions.KeepIdentity : SqlBulkCopyOptions.Default,
                    IncludeRowNumber.Yes);
                if (containsIdentityKey)
                    await EnableIdentityInsertAsync(tempTableName, conn, request.Transaction, cancellationToken).ConfigureAwait(false);

                int i = 0;
                var type = items[0].GetType();
                foreach (var entity in items)
                {
                    var e = entity;
                    var columnValues = new List<dynamic>();
                    columnValues.AddRange(keyProperties.Select(p =>
                        GetProperty(type, itemPropertyByEntityProperty[p.Name], e, DBNull.Value)));
                    columnValues.Add(i++);
                    table.Rows.Add(columnValues.ToArray());
                }

                await bulkCopy.WriteToServerAsync(table.CreateDataReader(), cancellationToken).ConfigureAwait(false);

                var conditionStatements = keyMappings.Values.Select(c =>
                {
                    var keyProperty = keyProperties.Single(p => p.Name == c.EntityProperty.Name);
                    return
                        $"([t0].[{c.TableColumn.Name}] = [t1].[{c.TableColumn.Name}] OR ([t0].[{c.TableColumn.Name}] IS NULL AND [t1].[{c.TableColumn.Name}] IS NULL))";
                });

                var conditionStatementsSql = string.Join(" AND ", conditionStatements);
                var query = $@"SELECT DISTINCT [t0].[rowno], [t1].*
                               FROM {tempTableName} AS [t0]
                               INNER JOIN {tableName.Fullname} AS [t1] ON {conditionStatementsSql}";

                var cmd = CreateSqlCommand(query, conn, request.Transaction, request.CommandTimeout);

                var existingEntities = new List<T1>();

                using (var sqlDataReader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                {
                    while (await sqlDataReader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        var rowNo = (int)sqlDataReader[0];
                        var item = items[rowNo];
                        foreach (var cpm in request.ColumnPropertyMappings)
                        {
                            SetProperty(cpm.ItemPropertyName, item, sqlDataReader[cpm.EntityPropertyName]);
                        }
                        existingEntities.Add(items[rowNo]);
                    }
                }

                await DropTempTableAsync(conn, request.Transaction, tempTableName, cancellationToken).ConfigureAwait(false);

                return existingEntities;
            }

            return new List<T1>();
        }
    }
}
