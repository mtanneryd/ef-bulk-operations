/*
 * Copyright ©  2017-2026 Tånneryd IT AB
 * Licensed under the Apache License, Version 2.0.
 */

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tanneryd.BulkOperations.Common.Sql;
using Tanneryd.BulkOperations.EFCore.Model;

namespace Tanneryd.BulkOperations.EFCore
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
            var mappings = GetMappingExtractor(ctx).GetMappings(t);
            var tableName = mappings.TableName;
            var columnMappings = mappings.ColumnMappingByPropertyName;
            var itemPropertByEntityProperty =
                request.KeyPropertyMappings.ToDictionary(p => p.EntityPropertyName, p => p.ItemPropertyName);
            var items = request.Items;
            var conn = await GetSqlConnectionAsync(ctx, cancellationToken).ConfigureAwait(false);

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
                var containsIdentityKey = keyMappings.Any(m => m.Value.IsIdentity);

                string tempTableName = null;
                var identityInsertEnabled = false;
                try
                {
                    tempTableName = await CreateTempTableAsync(
                        conn,
                        request.Transaction,
                        tableName,
                        keyMappings.Select(m => m.Value.TableColumn.Column.Name).ToArray(),
                        new TableColumn[0],
                        IncludeRowNumber.Yes,
                        cancellationToken).ConfigureAwait(false);

                    // We only need the key columns and the 
                    // rowno column in our temp table.
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
                        new TableColumn[0],
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
                        // TODO
                        // the 'is null' checks are only relevant for nullable columns
                        var keyProperty = keyProperties.Single(p => p.Name == c.EntityProperty.Name);
                        return
                            $"([t1].[{c.TableColumn.Column.Name}] = [t2].[{c.TableColumn.Column.Name}] OR ([t1].[{c.TableColumn.Column.Name}] IS NULL AND [t2].[{c.TableColumn.Column.Name}] IS NULL))";
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
            var mappings = GetMappingExtractor(ctx).GetMappings(t);
            var tableName = mappings.TableName;
            var columnMappings = mappings.ColumnMappingByPropertyName;
            var itemPropertyByEntityProperty =
                request.KeyPropertyMappings.ToDictionary(p => p.EntityPropertyName, p => p.ItemPropertyName);
            var items = request.Items;
            var conn = await GetSqlConnectionAsync(ctx, cancellationToken).ConfigureAwait(false);

            if (!itemPropertyByEntityProperty.Any())
            {
                throw new ArgumentException(
                    "The KeyPropertyMappings request property must be set and contain at least one name.");
            }

            // Get EF key mappings for the entity properties we are selecting on.
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
                var containsIdentityKey = keyMappings.Any(m => m.Value.IsIdentity);

                // Include rowno even when unused: on some hosts WriteToServer
                // does nothing if the temp table has no rowno column.
                string tempTableName = null;
                var identityInsertEnabled = false;
                try
                {
                    tempTableName = await CreateTempTableAsync(
                        conn,
                        request.Transaction,
                        tableName,
                        keyMappings.Select(m => m.Value.TableColumn.Column.Name).ToArray(),
                        new TableColumn[0],
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
                        new TableColumn[0],
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
                            $"([t0].[{c.TableColumn.Column.Name}] = [t1].[{c.TableColumn.Column.Name}] OR ([t0].[{c.TableColumn.Column.Name}] IS NULL AND [t1].[{c.TableColumn.Column.Name}] IS NULL))";
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
                        cmd.Parameters.Add(parameter);
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

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <param name="ctx"></param>
        /// <param name="request"></param>
        /// <returns></returns>

        private static async Task<IList<T2>> DoBulkSelectAsync<T1, T2>(
            DbContext ctx,
            BulkSelectRequest<T1> request,
            CancellationToken cancellationToken = default) where T2 : new()
        {
            if (!request.Items.Any()) return new List<T2>();

            Type t = typeof(T2);
            var mappings = GetMappingExtractor(ctx).GetMappings(t);
            var tableName = mappings.TableName;
            var columnMappings = mappings.ColumnMappingByPropertyName;
            var itemPropertByEntityProperty =
                request.KeyPropertyMappings.ToDictionary(p => p.EntityPropertyName, p => p.ItemPropertyName);
            var items = request.Items;
            var conn = await GetSqlConnectionAsync(ctx, cancellationToken).ConfigureAwait(false);

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
                var containsIdentityKey = keyMappings.Any(m => m.Value.IsIdentity);

                // Include rowno even when unused: on some hosts WriteToServer
                // does nothing if the temp table has no rowno column.
                string tempTableName = null;
                var identityInsertEnabled = false;
                try
                {
                    tempTableName = await CreateTempTableAsync(
                        conn,
                        request.Transaction,
                        tableName,
                        keyMappings.Select(m => m.Value.TableColumn.Column.Name).ToArray(),
                        new TableColumn[0],
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
                        new TableColumn[0],
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

                    var conditionStatements =
                        keyMappings.Values.Select(c => $"t0.[{c.TableColumn.Column.Name}] = t1.[{c.TableColumn.Column.Name}]");
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
                                var val = sqlDataReader[mapping.TableColumn.Column.Name];
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

        private static SelectMapping FindJoinTableMappingsForSelectExisting(
            KeyPropertyMapping[] keyPropertyMappings, 
            Mappings mappings,
            DbContext ctx,
            Type dbTableEntityType)
        {
            // We allow for one of the request.KeyPropertyMappings to represent
            // a property in a foreign key nav property of the actual table we
            // are selecting existing entities from. These have an entity property
            // name like <nav prop name>.<name>. So, if we are looking for
            // employees belonging to a company where the employee fk to the
            // company has the nav property Employer and the name of the company
            // in the company table has the property Name the EntityPropertyName
            // would have to have the value "Employer.Name" for this hack to work.
            if (keyPropertyMappings.Any(m=>m.EntityPropertyName.Contains(".")))
            {
                var keyPropertyMapping = (
                        from m in keyPropertyMappings
                        where m.EntityPropertyName.Contains(".")
                        select m)
                    .Single();

                var navPropertyName = keyPropertyMapping.EntityPropertyName.Split('.')[0];
                var selectPropertyName = keyPropertyMapping.EntityPropertyName.Split('.')[1];
                var fkMapping = (
                    from m in mappings.ToForeignKeyMappings
                    where m.NavigationPropertyName == navPropertyName
                    select m).Single();

                var navigationProperty = dbTableEntityType.GetProperty(fkMapping.NavigationPropertyName);
                var navigationPropertyType = navigationProperty.PropertyType;
                var navigationPropertyTableMappings = GetMappingExtractor(ctx).GetMappings(navigationPropertyType);
                var selectPropertyTableColumnMapping = navigationPropertyTableMappings.ColumnMappingByPropertyName[selectPropertyName];
                var navigationPropertyTableName = GetMappingExtractor(ctx).GetTableName(ctx, navigationPropertyType);
                var fromProperty = fkMapping.ForeignKeyRelations[0].FromProperty;
                var toProperty = fkMapping.ForeignKeyRelations[0].ToProperty;

                return new SelectMapping
                {
                    ItemPropertyName = keyPropertyMapping.ItemPropertyName,
                    SelectPropertyName = selectPropertyTableColumnMapping.EntityProperty.Name,
                    SelectPropertyType = selectPropertyTableColumnMapping.EntityProperty.ClrType,
                    SelectPropertySqlType = selectPropertyTableColumnMapping.TableColumn.Column.StoreType,
                    TableName = navigationPropertyTableName,
                    FkFromPropertyName = fromProperty,
                    FkToPropertyName = toProperty
                };
            }

            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <param name="ctx"></param>
        /// <param name="request"></param>
        /// <returns></returns>

        private static async Task<IList<T1>> DoBulkSelectExistingAsync<T1, T2>(
            DbContext ctx,
            BulkSelectRequest<T1> request,
            CancellationToken cancellationToken = default)
        {
            if (!request.Items.Any()) return new List<T1>();

            Type t = typeof(T2);
            var mappings = GetMappingExtractor(ctx).GetMappings(t);
            var tableName = mappings.TableName;
            var columnMappings = mappings.ColumnMappingByPropertyName;
            var itemPropertyByEntityProperty =
                request.KeyPropertyMappings.ToDictionary(p => p.EntityPropertyName, p => p.ItemPropertyName);
            var items = request.Items;
            var conn = await GetSqlConnectionAsync(ctx, cancellationToken).ConfigureAwait(false);

            if (!request.KeyPropertyMappings.Any())
            {
                throw new ArgumentException(
                    "The KeyPropertyMappings request property must be set and contain at least one name.");
            }

            var keyMappings = columnMappings.Values
                .Where(m => request.KeyPropertyMappings.Any(kpm => kpm.EntityPropertyName == m.EntityProperty.Name))
                .ToDictionary(m => m.EntityProperty.Name, m => m);

            var selectMapping = FindJoinTableMappingsForSelectExisting(request.KeyPropertyMappings, mappings, ctx, typeof(T2));

            if (keyMappings.Any() || selectMapping != null)
            {
                var containsIdentityKey = keyMappings.Any(m => m.Value.IsIdentity);
                
                // Create a temporary table with the supplied keys 
                // as columns, plus a rowno column.
                var columnNames = keyMappings.Select(m => m.Value.TableColumn.Column.Name).ToArray();
                var extraColumnNames = new List<TableColumn>();
                if (selectMapping !=null)
                {
                    var extraColumn = new TableColumn
                    {
                        Name = selectMapping.ItemPropertyName,
                        Type = selectMapping.SelectPropertyType,
                        SqlType = selectMapping.SelectPropertySqlType,
                        UseQuotes = true
                    };
                    extraColumnNames.Add(extraColumn);
                }
                string tempTableName = null;
                var identityInsertEnabled = false;
                try
                {
                    tempTableName = await CreateTempTableAsync(
                        conn,
                        request.Transaction,
                        tableName,
                        columnNames.ToArray(),
                        extraColumnNames.ToArray(),
                        IncludeRowNumber.Yes,
                        cancellationToken).ConfigureAwait(false);

                    var keyProperties = GetProperties(t)
                        .Where(p => keyMappings.ContainsKey(p.Name)).ToArray();

                    var dataTable = new DataTable();
                    using var bulkCopy = CreateBulkCopy(
                        dataTable,
                        keyProperties,
                        keyMappings,
                        conn,
                        request.Transaction,
                        tempTableName,
                        extraColumnNames.ToArray(),
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
                    using (var reader = new ObjectListDataReader(dataTable, (System.Collections.IList)items, (entity, rowIndex) =>
                    {
                        var columnValues = new List<object>();
                        columnValues.AddRange(keyProperties.Select(p =>
                            (object)GetProperty(type, itemPropertyByEntityProperty[p.Name], entity, DBNull.Value)));
                        columnValues.AddRange(extraColumnNames.Select(p =>
                            (object)GetProperty(type, p.Name, entity, DBNull.Value)));
                        columnValues.Add(rowIndex);
                        return columnValues.ToArray();
                    }))
                        await bulkCopy.WriteToServerAsync(reader, cancellationToken).ConfigureAwait(false);

                    var conditionStatements = keyMappings.Values.Select(c =>
                    {
                        // TODO
                        // the 'is null' checks are only relevant for nullable columns
                        var keyProperty = keyProperties.Single(p => p.Name == c.EntityProperty.Name);
                        return
                            $"([t0].[{c.TableColumn.Column.Name}] = [t1].[{c.TableColumn.Column.Name}] OR ([t0].[{c.TableColumn.Column.Name}] IS NULL AND [t1].[{c.TableColumn.Column.Name}] IS NULL))";
                    });
                    
                    var conditionStatementsSql = string.Join(" AND ", conditionStatements);
                    // We could improve performance here by replacing "[t1].*" below with the actual
                    // columns as specified in request.ColumnPropertyMappings.
                    var query = $@"SELECT DISTINCT [t0].[rowno], [t1].*
                                   FROM {tempTableName} AS [t0]
                                   INNER JOIN {tableName.Fullname} AS [t1] ON {conditionStatementsSql}";
                    
                    if (selectMapping != null)
                    {
                        // Figure out the db table name of the table we want to join with.
                        //var joinTableMember = typeof(T2).GetProperty(selectMapping.ForeignKeyMapping.NavigationPropertyName);
                        //var joinTableType = joinTableMember.PropertyType;
                        //var joinTableName = GetMappingExtractor(ctx).GetTableName(ctx, joinTableType);
                        //var fromProperty = selectMapping.ForeignKeyMapping.ForeignKeyRelations[0].FromProperty;
                        //var toProperty = selectMapping.ForeignKeyMapping.ForeignKeyRelations[0].ToProperty;
                        var fkJoinStatement = $"INNER JOIN {selectMapping.TableName.Fullname} AS [t2] ON [t2].[{selectMapping.FkFromPropertyName}] = [t1].[{selectMapping.FkToPropertyName}]";
                        var fkWhereStatement = $"WHERE [t2].[{selectMapping.SelectPropertyName}] = [t0].[{selectMapping.ItemPropertyName}]";
                        query = $@"{query}
                                   {fkJoinStatement}
                                   {fkWhereStatement}";
                    }
                    query += "\nORDER BY [t0].[rowno]";
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="request"></param>
    }
}
