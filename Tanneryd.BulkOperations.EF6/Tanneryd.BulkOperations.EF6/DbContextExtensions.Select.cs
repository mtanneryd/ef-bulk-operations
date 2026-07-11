/*
 * Copyright ©  2017-2025 Tånneryd IT AB
 * Licensed under the Apache License, Version 2.0.
 */

using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using Tanneryd.BulkOperations.EF6.Model;

namespace Tanneryd.BulkOperations.EF6
{
    public static partial class DbContextExtensions
    {
        private static IList<T1> DoBulkSelectNotExisting<T1, T2>(DbContext ctx, BulkSelectRequest<T1> request)
        {
            if (!request.Items.Any()) return new List<T1>();

            Type t = typeof(T2);
            var mappings = MappingExtractor.GetMappings(ctx, t);
            var tableName = mappings.TableName;
            var columnMappings = mappings.ColumnMappingByPropertyName;
            var itemPropertByEntityProperty =
                request.KeyPropertyMappings.ToDictionary(p => p.EntityPropertyName, p => p.ItemPropertyName);
            var items = request.Items;
            var conn = ResolveSqlConnection(ctx);

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

                var tempTableName = CreateTempTable(
                    conn,
                    request.Transaction,
                    tableName,
                    mappings.Discriminator,
                    keyMappings.Select(m => m.Value.TableColumn.Name).ToArray(),
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
                    mappings.Discriminator,
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
            var mappings = MappingExtractor.GetMappings(ctx, t);
            var tableName = mappings.TableName;
            var columnMappings = mappings.ColumnMappingByPropertyName;
            var itemPropertyByEntityProperty =
                request.KeyPropertyMappings.ToDictionary(p => p.EntityPropertyName, p => p.ItemPropertyName);
            var items = request.Items;
            var conn = ResolveSqlConnection(ctx);

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
                var containsIdentityKey = keyMappings.Any(m =>
                    m.Value.TableColumn.IsStoreGeneratedIdentity &&
                    m.Value.TableColumn.TypeName != "uniqueidentifier");

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
                    mappings.Discriminator,
                    keyMappings.Select(m => m.Value.TableColumn.Name).ToArray(),
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
                    mappings.Discriminator,
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
            var mappings = MappingExtractor.GetMappings(ctx, t);
            var tableName = mappings.TableName;
            var columnMappings = mappings.ColumnMappingByPropertyName;
            var itemPropertByEntityProperty =
                request.KeyPropertyMappings.ToDictionary(p => p.EntityPropertyName, p => p.ItemPropertyName);
            var items = request.Items;
            var conn = ResolveSqlConnection(ctx);

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
                    mappings.Discriminator,
                    keyMappings.Select(m => m.Value.TableColumn.Name).ToArray(),
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
                    mappings.Discriminator,
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
                    keyMappings.Values.Select(c => $"t0.[{c.TableColumn.Name}] = t1.[{c.TableColumn.Name}]");
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
                            var val = sqlDataReader[mapping.TableColumn.Name];
                            SetProperty(property, t2, val);
                        }
                    }
                }

                DropTempTable(conn, request.Transaction, tempTableName);

                return selectedEntities;
            }

            return new List<T2>();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <param name="ctx"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        private static IList<T1>
            DoBulkSelectExisting<T1, T2>(DbContext ctx, BulkSelectRequest<T1> request)
        {
            if (!request.Items.Any()) return new List<T1>();

            Type t = typeof(T2);
            var mappings = MappingExtractor.GetMappings(ctx, t);
            var tableName = mappings.TableName;
            var columnMappings = mappings.ColumnMappingByPropertyName;
            var itemPropertyByEntityProperty =
                request.KeyPropertyMappings.ToDictionary(p => p.EntityPropertyName, p => p.ItemPropertyName);
            var items = request.Items;
            var conn = ResolveSqlConnection(ctx);

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

                // Create a temporary table with the supplied keys 
                // as columns, plus a rowno column.
                var tempTableName = CreateTempTable(
                    conn,
                    request.Transaction,
                    tableName,
                    mappings.Discriminator,
                    keyMappings.Select(m => m.Value.TableColumn.Name).ToArray(),
                    IncludeRowNumber.Yes);

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
                if (containsIdentityKey) EnableIdentityInsert(tempTableName, conn, request.Transaction);

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

                bulkCopy.WriteToServer(table.CreateDataReader());

                var conditionStatements = keyMappings.Values.Select(c =>
                {
                    // TODO
                    // the 'is null' checks are only relevant for nullable columns
                    var keyProperty = keyProperties.Single(p => p.Name == c.EntityProperty.Name);
                    return
                        $"([t0].[{c.TableColumn.Name}] = [t1].[{c.TableColumn.Name}] OR ([t0].[{c.TableColumn.Name}] IS NULL AND [t1].[{c.TableColumn.Name}] IS NULL))";
                });

                var conditionStatementsSql = string.Join(" AND ", conditionStatements);
                // We could improve performance here by replacing "[t1].*" below with the actual
                // columns as specified in request.ColumnPropertyMappings.
                var query = $@"SELECT DISTINCT [t0].[rowno], [t1].*
                               FROM {tempTableName} AS [t0]
                               INNER JOIN {tableName.Fullname} AS [t1] ON {conditionStatementsSql}";

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
    }
}
