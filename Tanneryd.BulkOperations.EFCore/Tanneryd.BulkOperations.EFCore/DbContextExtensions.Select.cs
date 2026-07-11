/*
 * Copyright ©  2017-2020 Tånneryd IT AB
 * Licensed under the Apache License, Version 2.0.
 */

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Tanneryd.BulkOperations.EFCore.Model;

namespace Tanneryd.BulkOperations.EFCore
{
    public static partial class DbContextExtensions
    {
        private static IList<T1> DoBulkSelectNotExisting<T1, T2>(DbContext ctx, BulkSelectRequest<T1> request)
        {
            if (!request.Items.Any()) return new List<T1>();

            Type t = typeof(T2);
            var mappings = GetMappingExtractor(ctx).GetMappings(t);
            var tableName = mappings.TableName;
            var columnMappings = mappings.ColumnMappingByPropertyName;
            var itemPropertByEntityProperty =
                request.KeyPropertyMappings.ToDictionary(p => p.EntityPropertyName, p => p.ItemPropertyName);
            var items = request.Items;
            var conn = GetSqlConnection(ctx);

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

                var tempTableName = CreateTempTable(
                    conn,
                    request.Transaction,
                    tableName,
                    keyMappings.Select(m => m.Value.TableColumn.Column.Name).ToArray(),
                    new TableColumn[0],
                    IncludeRowNumber.Yes);

                // We only need the key columns and the 
                // rowno column in our temp table.
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
                    new TableColumn[0],
                    containsIdentityKey ? SqlBulkCopyOptions.KeepIdentity : SqlBulkCopyOptions.Default,
                    IncludeRowNumber.Yes);
                if (containsIdentityKey) EnableIdentityInsert(tempTableName, conn, request.Transaction);

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

                bulkCopy.WriteToServer(table.CreateDataReader());

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

                var cmd = CreateSqlCommand(query, conn, request.Transaction, request.CommandTimeout);

                var existingEntities = new List<T1>();
                using (var sqlDataReader = cmd.ExecuteReader())
                {
                    while (sqlDataReader.Read())
                    {
                        var rowNo = (int)sqlDataReader[0];
                        existingEntities.Add(items[rowNo]);
                    }
                }

                DropTempTable(conn, request.Transaction, tempTableName);

                return existingEntities;
            }

            return new List<T1>();
        }

        private static void DoBulkDeleteNotExisting<T1, T2>(DbContext ctx, BulkDeleteRequest<T1> request)
        {
            Type t = typeof(T2);
            var mappings = GetMappingExtractor(ctx).GetMappings(t);
            var tableName = mappings.TableName;
            var columnMappings = mappings.ColumnMappingByPropertyName;
            var itemPropertyByEntityProperty =
                request.KeyPropertyMappings.ToDictionary(p => p.EntityPropertyName, p => p.ItemPropertyName);
            var items = request.Items;
            var conn = GetSqlConnection(ctx);

            if (!itemPropertyByEntityProperty.Any())
            {
                throw new ArgumentException(
                    "The KeyPropertyMappings request property must be set and contain at least one name.");
            }

            // Get EF key mappings for the entity properties we are selecting on.
            var keyMappings = columnMappings.Values
                .Where(m => request.KeyPropertyMappings.Any(kpm => kpm.EntityPropertyName == m.EntityProperty.Name))
                .ToDictionary(m => m.EntityProperty.Name, m => m);

            if (keyMappings.Any())
            {
                var containsIdentityKey = keyMappings.Any(m => m.Value.IsIdentity);

                // Create a temporary table with the supplied keys 
                // as columns. We include the rowno column as well
                // even though we do not need it. But, for some
                // ungodly reason WriteToServer does nothing, on
                // some platforms, if we omit it. Need to figure
                // that out at some point.
                var tempTableName = CreateTempTable(
                    conn,
                    request.Transaction,
                    tableName,
                    keyMappings.Select(m => m.Value.TableColumn.Column.Name).ToArray(),
                    new TableColumn[0],
                    IncludeRowNumber.Yes);

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
                    new TableColumn[0],
                    containsIdentityKey ? SqlBulkCopyOptions.KeepIdentity : SqlBulkCopyOptions.Default,
                    IncludeRowNumber.Yes);
                if (containsIdentityKey) EnableIdentityInsert(tempTableName, conn, request.Transaction);

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

                bulkCopy.WriteToServer(table.CreateDataReader());

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

                var cmd = CreateSqlCommand(query, conn, request.Transaction, request.CommandTimeout);
                foreach (var parameter in parameters)
                    cmd.Parameters.Add(parameter);
                cmd.ExecuteNonQuery();

                DropTempTable(conn, request.Transaction, tempTableName);
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
        private static IList<T2> DoBulkSelect<T1, T2>(DbContext ctx, BulkSelectRequest<T1> request) where T2 : new()
        {
            if (!request.Items.Any()) return new List<T2>();

            Type t = typeof(T2);
            var mappings = GetMappingExtractor(ctx).GetMappings(t);
            var tableName = mappings.TableName;
            var columnMappings = mappings.ColumnMappingByPropertyName;
            var itemPropertByEntityProperty =
                request.KeyPropertyMappings.ToDictionary(p => p.EntityPropertyName, p => p.ItemPropertyName);
            var items = request.Items;
            var conn = GetSqlConnection(ctx);

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

                // Create a temporary table with the supplied keys 
                // as columns. We include the rowno column as well
                // even though we do not need it. But, for some
                // ungodly reason WriteToServer does nothing, on
                // some platforms, if we omit it. Need to figure
                // that out at some point.
                var tempTableName = CreateTempTable(
                    conn,
                    request.Transaction,
                    tableName,
                    keyMappings.Select(m => m.Value.TableColumn.Column.Name).ToArray(),
                    new TableColumn[0],
                    IncludeRowNumber.Yes);

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
                    new TableColumn[0],
                    containsIdentityKey ? SqlBulkCopyOptions.KeepIdentity : SqlBulkCopyOptions.Default,
                    IncludeRowNumber.Yes);
                if (containsIdentityKey) EnableIdentityInsert(tempTableName, conn, request.Transaction);

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

                bulkCopy.WriteToServer(table.CreateDataReader());

                var conditionStatements =
                    keyMappings.Values.Select(c => $"t0.[{c.TableColumn.Column.Name}] = t1.[{c.TableColumn.Column.Name}]");
                var conditionStatementsSql = string.Join(" AND ", conditionStatements);
                var query = $@"SELECT [t0].*
                               FROM {tableName.Fullname} AS [t0]
                               INNER JOIN {tempTableName} AS [t1] ON {conditionStatementsSql}
                               ORDER BY [t1].rowno ASC";

                var cmd = CreateSqlCommand(query, conn, request.Transaction, request.CommandTimeout);

                var selectedEntities = new List<T2>();
                using (var sqlDataReader = cmd.ExecuteReader())
                {
                    while (sqlDataReader.Read())
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

                DropTempTable(conn, request.Transaction, tempTableName);

                return selectedEntities;
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
        private static IList<T1> DoBulkSelectExisting<T1, T2>(DbContext ctx, BulkSelectRequest<T1> request)
        {
            if (!request.Items.Any()) return new List<T1>();

            Type t = typeof(T2);
            var mappings = GetMappingExtractor(ctx).GetMappings(t);
            var tableName = mappings.TableName;
            var columnMappings = mappings.ColumnMappingByPropertyName;
            var itemPropertyByEntityProperty =
                request.KeyPropertyMappings.ToDictionary(p => p.EntityPropertyName, p => p.ItemPropertyName);
            var items = request.Items;
            var conn = GetSqlConnection(ctx);

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
                var tempTableName = CreateTempTable(
                    conn,
                    request.Transaction,
                    tableName,
                    columnNames.ToArray(),
                    extraColumnNames.ToArray(),
                    IncludeRowNumber.Yes);

                var keyProperties = GetProperties(t)
                    .Where(p => keyMappings.ContainsKey(p.Name)).ToArray();

                var dataTable = new DataTable();
                var bulkCopy = CreateBulkCopy(
                    dataTable,
                    keyProperties,
                    keyMappings,
                    conn,
                    request.Transaction,
                    tempTableName,
                    extraColumnNames.ToArray(),
                    containsIdentityKey ? SqlBulkCopyOptions.KeepIdentity : SqlBulkCopyOptions.Default,
                    IncludeRowNumber.Yes);
                if (containsIdentityKey) EnableIdentityInsert(tempTableName, conn, request.Transaction);

                int i = 0;
                var type = items[0].GetType();
                foreach (var entity in items)
                {
                    var e = entity;
                    var columnValues = new List<dynamic>();
                    columnValues.AddRange(keyProperties.Select(p =>
                        GetProperty(type, itemPropertyByEntityProperty[p.Name], e, DBNull.Value)));
                    columnValues.AddRange(extraColumnNames.Select(p =>
                        GetProperty(type, p.Name, e, DBNull.Value)));
                    columnValues.Add(i++);
                    dataTable.Rows.Add(columnValues.ToArray());
                }

                bulkCopy.WriteToServer(dataTable.CreateDataReader());

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
                var cmd = CreateSqlCommand(query, conn, request.Transaction, request.CommandTimeout);

                var existingEntities = new List<T1>();

                using (var sqlDataReader = cmd.ExecuteReader())
                {
                    while (sqlDataReader.Read())
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

                DropTempTable(conn, request.Transaction, tempTableName);

                return existingEntities;
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
