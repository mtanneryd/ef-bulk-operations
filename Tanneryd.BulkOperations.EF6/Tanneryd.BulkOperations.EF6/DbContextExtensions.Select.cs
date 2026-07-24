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
using Tanneryd.BulkOperations.Common.Sql;
using Tanneryd.BulkOperations.EF6.Model;

namespace Tanneryd.BulkOperations.EF6
{
    public static partial class DbContextExtensions
    {

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
                // Key-only staging: omit Discriminator (EF Core parity); materializers
                // emit keys + rowno only.
                string tempTableName = null;
                var identityInsertEnabled = false;
                try
                {
                    tempTableName = await CreateTempTableAsync(
                        conn,
                        request.Transaction,
                        tableName,
                        null,
                        keyMappings.Select(m => m.Value.TableColumn.Name).ToArray(),
                        IncludeRowNumber.Yes,
                        cancellationToken).ConfigureAwait(false);

                    var keyProperties = GetProperties(t)
                        .Where(p => keyMappings.ContainsKey(p.Name)).ToArray();

                    var table = new DataTable();
                    using var bulkCopy = CreateBulkCopy(
                        table,
                        keyProperties,
                        keyMappings,
                        conn,
                        request.Transaction,
                        tempTableName,
                        null,
                        containsIdentityKey ? SqlBulkCopyOptions.KeepIdentity : SqlBulkCopyOptions.Default,
                        IncludeRowNumber.Yes,
                        request.CommandTimeout,
                        request.UseTableLock);
                    if (containsIdentityKey)
                    {
                        await EnableIdentityInsertAsync(tempTableName, conn, request.Transaction, cancellationToken).ConfigureAwait(false);
                        identityInsertEnabled = true;
                    }

                    var type = items[0].GetType();
                    using (var reader = new ObjectListDataReader(table, (System.Collections.IList)items, (entity, rowIndex) =>
                    {
                        var columnValues = new List<object>();
                        columnValues.AddRange(keyProperties.Select(p =>
                            (object)GetProperty(type, itemPropertByEntityProperty[p.Name], entity, DBNull.Value)));
                        columnValues.Add(rowIndex);
                        return columnValues.ToArray();
                    }))
                        await bulkCopy.WriteToServerAsync(reader, cancellationToken).ConfigureAwait(false);

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

                    using var cmd = CreateSqlCommand(query, conn, request.Transaction, request.CommandTimeout);

                    var existingEntities = new List<T1>();
                    using (var sqlDataReader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                    {
                        while (await sqlDataReader.ReadAsync(cancellationToken).ConfigureAwait(false))
                        {
                            var rowNo = (int)sqlDataReader[0];
                            existingEntities.Add(items[rowNo]);
                        }
                    }

                    return existingEntities;
                }
                finally
                {
                    if (identityInsertEnabled)
                    {
                        await DisableIdentityInsertAsync(
                            tempTableName,
                            conn,
                            request.Transaction,
                            CancellationToken.None).ConfigureAwait(false);
                    }
                    if (tempTableName != null)
                        await DropTempTableAsync(conn, request.Transaction, tempTableName, CancellationToken.None).ConfigureAwait(false);
                }
            }

            return new List<T1>();
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

            var unresolvedKeyProperties = request.KeyPropertyMappings
                .Select(kpm => kpm.EntityPropertyName)
                .Where(name => !keyMappings.ContainsKey(name))
                .Distinct()
                .ToArray();
            if (unresolvedKeyProperties.Length > 0)
            {
                throw new ArgumentException(
                    "KeyPropertyMappings contain property name(s) that are not mapped on the target entity: " +
                    string.Join(", ", unresolvedKeyProperties) + ".");
            }

            if (keyMappings.Any())
            {
                var containsIdentityKey = keyMappings.Any(m =>
                    m.Value.TableColumn.IsStoreGeneratedIdentity &&
                    m.Value.TableColumn.TypeName != "uniqueidentifier");

                // Include rowno even when unused: on some hosts WriteToServer
                // does nothing if the temp table has no rowno column.
                // Key-only staging: omit Discriminator (EF Core parity); materializers
                // emit keys + rowno only.
                string tempTableName = null;
                var identityInsertEnabled = false;
                try
                {
                    tempTableName = await CreateTempTableAsync(
                        conn,
                        request.Transaction,
                        tableName,
                        null,
                        keyMappings.Select(m => m.Value.TableColumn.Name).ToArray(),
                        IncludeRowNumber.Yes,
                        cancellationToken).ConfigureAwait(false);

                    var properties = GetProperties(t);
                    var keyProperties = properties
                        .Where(p => keyMappings.ContainsKey(p.Name)).ToArray();

                    var table = new DataTable();
                    using var bulkCopy = CreateBulkCopy(
                        table,
                        keyProperties,
                        keyMappings,
                        conn,
                        request.Transaction,
                        tempTableName,
                        null,
                        containsIdentityKey ? SqlBulkCopyOptions.KeepIdentity : SqlBulkCopyOptions.Default,
                        IncludeRowNumber.Yes,
                        request.CommandTimeout,
                        request.UseTableLock);
                    if (containsIdentityKey)
                    {
                        await EnableIdentityInsertAsync(tempTableName, conn, request.Transaction, cancellationToken).ConfigureAwait(false);
                        identityInsertEnabled = true;
                    }

                    var type = typeof(T1);
                    using (var reader = new ObjectListDataReader(table, (System.Collections.IList)items, (entity, rowIndex) =>
                    {
                        var columnValues = new List<object>();
                        columnValues.AddRange(keyProperties.Select(p =>
                            (object)GetProperty(type, itemPropertyByEntityProperty[p.Name], entity, DBNull.Value)));
                        columnValues.Add(rowIndex);
                        return columnValues.ToArray();
                    }))
                        await bulkCopy.WriteToServerAsync(reader, cancellationToken).ConfigureAwait(false);

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

                    using var cmd = CreateSqlCommand(query, conn, request.Transaction, request.CommandTimeout);
                    foreach (var parameter in parameters)
                        cmd.AddParameter(parameter);
                    await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    if (identityInsertEnabled)
                    {
                        await DisableIdentityInsertAsync(
                            tempTableName,
                            conn,
                            request.Transaction,
                            CancellationToken.None).ConfigureAwait(false);
                    }
                    if (tempTableName != null)
                        await DropTempTableAsync(conn, request.Transaction, tempTableName, CancellationToken.None).ConfigureAwait(false);
                }
            }
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
                // Key-only staging: omit Discriminator (EF Core parity); materializers
                // emit keys + rowno only.
                string tempTableName = null;
                var identityInsertEnabled = false;
                try
                {
                    tempTableName = await CreateTempTableAsync(
                        conn,
                        request.Transaction,
                        tableName,
                        null,
                        keyMappings.Select(m => m.Value.TableColumn.Name).ToArray(),
                        IncludeRowNumber.Yes,
                        cancellationToken).ConfigureAwait(false);

                    var properties = GetProperties(t);
                    var keyProperties = properties
                        .Where(p => keyMappings.ContainsKey(p.Name)).ToArray();

                    var table = new DataTable();
                    using var bulkCopy = CreateBulkCopy(
                        table,
                        keyProperties,
                        keyMappings,
                        conn,
                        request.Transaction,
                        tempTableName,
                        null,
                        containsIdentityKey ? SqlBulkCopyOptions.KeepIdentity : SqlBulkCopyOptions.Default,
                        IncludeRowNumber.Yes,
                        request.CommandTimeout,
                        request.UseTableLock);
                    if (containsIdentityKey)
                    {
                        await EnableIdentityInsertAsync(tempTableName, conn, request.Transaction, cancellationToken).ConfigureAwait(false);
                        identityInsertEnabled = true;
                    }

                    var type = items[0].GetType();
                    using (var reader = new ObjectListDataReader(table, (System.Collections.IList)items, (entity, rowIndex) =>
                    {
                        var columnValues = new List<object>();
                        columnValues.AddRange(keyProperties.Select(p =>
                            (object)GetProperty(type, itemPropertByEntityProperty[p.Name], entity, DBNull.Value)));
                        columnValues.Add(rowIndex);
                        return columnValues.ToArray();
                    }))
                        await bulkCopy.WriteToServerAsync(reader, cancellationToken).ConfigureAwait(false);

                    var conditionStatements = keyMappings.Values.Select(c =>
                        $"([t0].[{c.TableColumn.Name}] = [t1].[{c.TableColumn.Name}] OR ([t0].[{c.TableColumn.Name}] IS NULL AND [t1].[{c.TableColumn.Name}] IS NULL))");
                    var conditionStatementsSql = string.Join(" AND ", conditionStatements);
                    var query = $@"SELECT [t0].*
                                   FROM {tableName.Fullname} AS [t0]
                                   INNER JOIN {tempTableName} AS [t1] ON {conditionStatementsSql}
                                   ORDER BY [t1].rowno ASC";

                    using var cmd = CreateSqlCommand(query, conn, request.Transaction, request.CommandTimeout);

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

                    return selectedEntities;
                }
                finally
                {
                    if (identityInsertEnabled)
                    {
                        await DisableIdentityInsertAsync(
                            tempTableName,
                            conn,
                            request.Transaction,
                            CancellationToken.None).ConfigureAwait(false);
                    }
                    if (tempTableName != null)
                        await DropTempTableAsync(conn, request.Transaction, tempTableName, CancellationToken.None).ConfigureAwait(false);
                }
            }

            return new List<T2>();
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
                // Key-only staging: omit Discriminator (EF Core parity); materializers
                // emit keys + rowno only.
                string tempTableName = null;
                var identityInsertEnabled = false;
                try
                {
                    tempTableName = await CreateTempTableAsync(
                        conn,
                        request.Transaction,
                        tableName,
                        null,
                        keyMappings.Select(m => m.Value.TableColumn.Name).ToArray(),
                        IncludeRowNumber.Yes,
                        cancellationToken).ConfigureAwait(false);

                    var keyProperties = GetProperties(t)
                        .Where(p => keyMappings.ContainsKey(p.Name)).ToArray();

                    var table = new DataTable();
                    using var bulkCopy = CreateBulkCopy(
                        table,
                        keyProperties,
                        keyMappings,
                        conn,
                        request.Transaction,
                        tempTableName,
                        null,
                        containsIdentityKey ? SqlBulkCopyOptions.KeepIdentity : SqlBulkCopyOptions.Default,
                        IncludeRowNumber.Yes,
                        request.CommandTimeout,
                        request.UseTableLock);
                    if (containsIdentityKey)
                    {
                        await EnableIdentityInsertAsync(tempTableName, conn, request.Transaction, cancellationToken).ConfigureAwait(false);
                        identityInsertEnabled = true;
                    }

                    var type = items[0].GetType();
                    using (var reader = new ObjectListDataReader(table, (System.Collections.IList)items, (entity, rowIndex) =>
                    {
                        var columnValues = new List<object>();
                        columnValues.AddRange(keyProperties.Select(p =>
                            (object)GetProperty(type, itemPropertyByEntityProperty[p.Name], entity, DBNull.Value)));
                        columnValues.Add(rowIndex);
                        return columnValues.ToArray();
                    }))
                        await bulkCopy.WriteToServerAsync(reader, cancellationToken).ConfigureAwait(false);

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

                    using var cmd = CreateSqlCommand(query, conn, request.Transaction, request.CommandTimeout);

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

                    return existingEntities;
                }
                finally
                {
                    if (identityInsertEnabled)
                    {
                        await DisableIdentityInsertAsync(
                            tempTableName,
                            conn,
                            request.Transaction,
                            CancellationToken.None).ConfigureAwait(false);
                    }
                    if (tempTableName != null)
                        await DropTempTableAsync(conn, request.Transaction, tempTableName, CancellationToken.None).ConfigureAwait(false);
                }
            }

            return new List<T1>();
        }
    }
}
