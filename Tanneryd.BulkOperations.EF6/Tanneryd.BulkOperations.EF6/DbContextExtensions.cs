/*
 * Copyright ©  2017-2019 Tånneryd IT AB
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *   http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Core.Mapping;
using System.Data.Entity.Core.Metadata.Edm;
using System.Data.Entity.Core.Objects;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Tanneryd.BulkOperations.EF6.Model;

namespace Tanneryd.BulkOperations.EF6
{
    public static class DbContextExtensions
    {
        private static readonly HashSet<Type> IntegerTypes = new HashSet<Type>
        {
            typeof(Int16),
            typeof(Int32),
            typeof(Int64),
            typeof(UInt16),
            typeof(UInt32),
            typeof(UInt64),
        };

        #region Public API

        /// <summary>
        ///
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <param name="ctx"></param>
        /// <param name="request"></param>
        public static void BulkDeleteNotExisting<T1, T2>(
            this DbContext ctx,
            BulkDeleteRequest<T1> request)
        {
            DoBulkDeleteNotExisting<T1, T2>(ctx, request);
        }

        /// <summary>
        /// The request object contains a mapping between properties in T1
        /// and properties in T2. BulkSelect will match all rows in the T2
        /// table given the list of T1 items and their defined key
        /// properties and return the set of T2 items matched. This is
        /// particularly useful when you need to select a number of rows
        /// from a table using multiple selector columns. If there is only
        /// one column you need to select on you could simply use the EF
        /// equivalent of 'where in', Contains() or Any() on your local
        /// collection holding the values you want to apply 'where in' on.
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <param name="ctx"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        public static IList<T2> BulkSelect<T1, T2>(
            this DbContext ctx,
            BulkSelectRequest<T1> request) where T2 : new()
        {
            return DoBulkSelect<T1, T2>(ctx, request);
        }

        /// <summary>
        /// Given a set of entities we return the subset of these entities
        /// that already exist in the database, according to the key selector
        /// used.
        /// </summary>
        /// <typeparam name="T1">The item collection type</typeparam>
        /// <typeparam name="T2">The EF entity type</typeparam>
        /// <param name="ctx"></param>
        /// <param name="request"></param>
        public static IList<T1> BulkSelectExisting<T1, T2>(
            this DbContext ctx,
            BulkSelectRequest<T1> request)
        {
            return DoBulkSelectExisting<T1, T2>(ctx, request);
        }

        /// <summary>
        /// Given a set of entities we return the subset of these entities
        /// that do not exist in the database, according to the key selector
        /// used.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="ctx"></param>
        /// <param name="request"></param>
        public static IList<T1> BulkSelectNotExisting<T1, T2>(
            this DbContext ctx,
            BulkSelectRequest<T1> request)
        {
            return DoBulkSelectNotExisting<T1, T2>(ctx, request);
        }

        private static IList BulkSelectNotExisting(DbContext ctx, Type t, IList entities,
            TableColumnMapping[] pkColumnMappings, SqlTransaction sqlTransaction)
        {
            var request = typeof(BulkSelectRequest<>).MakeGenericType(t);
            var keyPropertyNames = pkColumnMappings.Select(m => m.TableColumn.Name).ToArray();
            var r = Activator.CreateInstance(request, keyPropertyNames, entities.ToArray(t), sqlTransaction);
            Type ex = typeof(DbContextExtensions);
            MethodInfo mi = ex.GetMethod("BulkSelectNotExisting");
            MethodInfo miGeneric = mi.MakeGenericMethod(new[] { t, t });
            object[] args = { ctx, r };
            var notExistingEntities = (IList)miGeneric.Invoke(null, args);

            return notExistingEntities;
        }

        /// <summary>
        /// All columns of the entities' corresponding table rows 
        /// will be updated using the table primary key. If a 
        /// transaction object is provided the update will be made
        /// within that transaction. Tables with no primary key will
        /// be left untouched.
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="entities"></param>
        /// <param name="transaction"></param>
        public static BulkOperationResponse BulkUpdateAll(
            this DbContext ctx,
            IList entities,
            SqlTransaction transaction)
        {
            var request = new BulkUpdateRequest
            {
                Entities = entities,
                Transaction = transaction,
            };
            return BulkUpdateAll(ctx, request);
        }

        /// <summary>
        /// 
        /// The request object properties have the following function:
        /// 
        /// Entities - The entities are mapped to rows in a table and these table rows will be updated.
        /// UpdatedColumnNames - Specifies which columns to update. An empty list will update ALL columns.
        /// KeyMemberNames - Specifies which columns to use as row selectors. An empty list will result
        ///                  in the primary key columns to be used.
        /// Transaction - If a transaction object is provided the update will be made within that transaction.
        /// InsertIfNew - When set to true, any entities new to the table will be inserted. Otherwise they 
        ///               will be ignored.
        /// 
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="request"></param>
        public static BulkOperationResponse BulkUpdateAll(
            this DbContext ctx,
            BulkUpdateRequest request)
        {
            var response = new BulkOperationResponse();
            if (request.Entities.Count == 0) return response;
            DoBulkUpdateAll(ctx, request, response);

            return response;
        }


        /// <summary>
        /// Insert all entities using System.Data.SqlClient.SqlBulkCopy. 
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="entities"></param>
        /// <param name="transaction"></param>
        /// <param name="recursive">True if the entire entity graph should be inserted, false otherwise.</param>
        public static BulkInsertResponse BulkInsertAll<T>(
            this DbContext ctx,
            IList<T> entities,
            SqlTransaction transaction = null,
            bool recursive = false)
        {
            var request = new BulkInsertRequest<T>
            {
                Entities = entities,
                Transaction = transaction,
                Recursive = recursive,
            };
            return BulkInsertAll(ctx, request);
        }

        /// <summary>
        /// 
        /// The request object properties have the following function:
        /// 
        ///  Entities - The entities are mapped to rows in a table and these table rows will 
        ///             be updated.
        ///  Transaction - If a transaction object is provided the update will be made within that transaction.
        ///  Recursive - If true any new entities added to navigation properties will also be inserted. Foreign 
        ///              key relationships will be honored for both new and existing entities in the entire 
        ///              entity graph.
        /// 
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        public static BulkInsertResponse BulkInsertAll<T>(
            this DbContext ctx,
            BulkInsertRequest<T> request)
        {
            var response = new BulkInsertResponse();

            if (request.Entities.Count == 0) return response;

            var s = new Stopwatch();
            s.Start();

            try
            {
                var t = typeof(T);
                var tableName = GetTableName(ctx, t);
                var mappingsByType = new Dictionary<Type, Mappings>();
                if (request.SortUsingClusteredIndex)
                {
                    var mappings = GetMappings(ctx, t);
                    mappingsByType.Add(t, mappings);

                    var s0 = new Stopwatch();
                    s0.Start();
                    var clusteredIndexColumns = GetClusteredIndexColumns(
                        ctx,
                        tableName.Name,
                        request.Transaction,
                        mappings);

                    request.Entities = clusteredIndexColumns.Any()
                        ? Sort(request.Entities, clusteredIndexColumns)
                        : request.Entities;
                    s0.Stop();
                    response.TimeElapsedDuringSorting = s0.Elapsed;
                }

                DoBulkInsertAll(
                    ctx,
                    request.Entities.Cast<dynamic>().ToList(),
                    request.Transaction,
                    request.Recursive,
                    request.AllowNotNullSelfReferences,
                    request.CommandTimeout,
                    new Dictionary<object, object>(new IdentityEqualityComparer<object>()),
                    mappingsByType,
                    response);

                if (request.UpdateStatistics)
                {
                    var s0 = new Stopwatch();
                    s0.Start();
                    var query = $"UPDATE STATISTICS {tableName.Fullname} WITH ALL";
                    var connection = GetSqlConnection(ctx);
                    var cmd = CreateSqlCommand(query, connection, request.Transaction, request.CommandTimeout);
                    cmd.ExecuteNonQuery();
                    s0.Stop();
                    response.TimeElapsedDuringUpdateStatistics = s0.Elapsed;
                }
            }
            finally
            {
                foreach (var tableName in response.TablesWithNoCheckConstraints)
                {
                    var query = $"ALTER TABLE {tableName} WITH CHECK CHECK CONSTRAINT ALL";
                    var connection = GetSqlConnection(ctx);
                    var cmd = CreateSqlCommand(query, connection, request.Transaction, request.CommandTimeout);
                    cmd.ExecuteNonQuery();
                }
            }

            s.Stop();
            response.Elapsed = s.Elapsed;

            return response;
        }

        public static BulkInsertResponse UpdateStatistics<T>(this DbContext ctx)
        {
            var response = new BulkInsertResponse();
            var tableName = GetTableName(ctx, typeof(T));

            var s0 = new Stopwatch();
            s0.Start();
            var query = $"UPDATE STATISTICS {tableName.Fullname} WITH ALL";
            var connection = GetSqlConnection(ctx);
            var cmd = CreateSqlCommand(query, connection, null, TimeSpan.FromMinutes(15));
            cmd.ExecuteNonQuery();
            s0.Stop();
            response.TimeElapsedDuringUpdateStatistics = s0.Elapsed;

            return response;
        }

        #endregion

        #region Private methods

        /// <summary>
        /// 
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="transaction"></param>
        /// <param name="tableName"></param>
        /// <param name="columnNames"></param>
        /// <param name="includeRowNumber"></param>
        /// <returns></returns>
        private static string CreateTempTable(
            SqlConnection connection,
            SqlTransaction transaction,
            TableName tableName,
            string[] columnNames,
            IncludeRowNumber includeRowNumber = IncludeRowNumber.No)
        {
            var selectClause = string.Join(",", columnNames.Select(p => $"[{p}]"));

            if (includeRowNumber == IncludeRowNumber.Yes)
            {
                selectClause = "cast(1 as int) as rowno," + selectClause;
            }

            var guid = Guid.NewGuid().ToString("N");
            var tempTableName = $"tempdb..#{guid}";
            var query = $@"   
                        IF OBJECT_ID('{tempTableName}') IS NOT NULL DROP TABLE {tempTableName}

                        SELECT {selectClause}
                        INTO {tempTableName}
                        FROM {tableName.Fullname}
                        WHERE 1=0";
            var cmd = CreateSqlCommand(query, connection, transaction, TimeSpan.FromSeconds(30));
            cmd.ExecuteNonQuery();

            return tempTableName;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="transaction"></param>
        /// <param name="tempTableName"></param>
        private static void DropTempTable(
            SqlConnection connection,
            SqlTransaction transaction,
            string tempTableName)
        {
            var cmdFooter = $@"DROP TABLE IF EXISTS {tempTableName}";
            var cmd = CreateSqlCommand(cmdFooter, connection, transaction, TimeSpan.FromSeconds(30));
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="table"></param>
        /// <param name="properties"></param>
        /// <param name="columnMappings"></param>
        /// <param name="connection"></param>
        /// <param name="transaction"></param>
        /// <param name="tableName"></param>
        /// <param name="options"></param>
        /// <param name="includeRowNumber"></param>
        /// <returns></returns>
        private static SqlBulkCopy CreateBulkCopy(
            DataTable table,
            BulkPropertyInfo[] properties,
            Dictionary<string, TableColumnMapping> columnMappings,
            SqlConnection connection,
            SqlTransaction transaction,
            string tableName,
            SqlBulkCopyOptions options = SqlBulkCopyOptions.Default,
            IncludeRowNumber includeRowNumber = IncludeRowNumber.No)
        {
            options = options | SqlBulkCopyOptions.TableLock;
            var bulkCopy = new SqlBulkCopy(connection, options, transaction)
            {
                DestinationTableName = tableName,
                EnableStreaming = true,
                BatchSize = 1000000,
                BulkCopyTimeout = 10 * 60
            };

            foreach (var property in properties)
            {
                Type propertyType = property.Type;

                // Nullable properties need special treatment.
                if (propertyType.IsGenericType &&
                    propertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    propertyType = Nullable.GetUnderlyingType(propertyType);
                }

                // Ignore all properties that we have no mappings for. We might have done so
                // already but just to be really really sure.
                if (columnMappings.ContainsKey(property.Name))
                {
                    // Since we cannot trust the CLR type properties to be in the same order as
                    // the table columns we use the SqlBulkCopy column mappings.
                    table.Columns.Add(new DataColumn(property.Name, propertyType));
                    var clrPropertyName = property.Name;
                    var tableColumnName = columnMappings[property.Name].TableColumn.Name;
                    bulkCopy.ColumnMappings.Add(new SqlBulkCopyColumnMapping(clrPropertyName, tableColumnName));
                }
            }

            if (includeRowNumber == IncludeRowNumber.Yes)
            {
                table.Columns.Add(new DataColumn("rowno", typeof(int)));
                bulkCopy.ColumnMappings.Add(new SqlBulkCopyColumnMapping("rowno", "rowno"));
            }

            return bulkCopy;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <param name="ctx"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        private static IList<T1> DoBulkSelectNotExisting<T1, T2>(DbContext ctx, BulkSelectRequest<T1> request)
        {
            if (!request.Items.Any()) return new List<T1>();

            Type t = typeof(T2);
            var mappings = GetMappings(ctx, t);
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
                .Where(m => request.KeyPropertyMappings.Any(kpm => kpm.EntityPropertyName == m.TableColumn.Name))
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
            if (!request.Items.Any()) return;

            Type t = typeof(T2);
            var mappings = GetMappings(ctx, t);
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

                var condStatements = request.SqlConditions.Select(c => $"[t0].[{c.ColumnName}] = {c.ColumnValue}");
                var condStatementsSql = string.Join(" AND ", condStatements);
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
            var mappings = GetMappings(ctx, t);
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
            var mappings = GetMappings(ctx, t);
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
                var query = $@"SELECT DISTINCT [t0].[rowno]
                               FROM {tempTableName} AS [t0]
                               INNER JOIN {tableName.Fullname} AS [t1] ON {conditionStatementsSql}";

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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="request"></param>
        /// <param name="response"></param>
        private static void DoBulkUpdateAll(
            this DbContext ctx,
            BulkUpdateRequest request,
            BulkOperationResponse response)
        {
            var rowsAffected = 0;

            var keyMemberNames = request.KeyPropertyNames;
            var updatedColumnNames = request.UpdatedColumnNames;
            var entities = request.Entities;
            var transaction = request.Transaction;

            Type t = request.Entities[0].GetType();
            var mappings = GetMappings(ctx, t);
            var tableName = mappings.TableName;
            var columnMappings = mappings.ColumnMappingByPropertyName;

            //
            // Check to see if the table has a primary key. If so,
            // get a clr property name to table column name mapping.
            //
            var primaryKeyMembers = GetPrimaryKeyMembers(columnMappings);

            var selectedKeyMembers = keyMemberNames.Any() ? keyMemberNames : primaryKeyMembers.ToArray();
            var allKeyMembers = new List<string>();
            allKeyMembers.AddRange(primaryKeyMembers);
            allKeyMembers.AddRange(keyMemberNames);

            var selectedKeyMappings = columnMappings.Values
                .Where(m => selectedKeyMembers.Contains(m.TableColumn.Name))
                .ToArray();

            if (selectedKeyMappings.Any())
            {
                //
                // Get a clr property name to table column name mapping
                // for the columns we want to update. Exclude any primary
                // key column as well as any column that we chose to use
                // as a key column in this specific update operation.
                //
                var modifiedColumnMappingCandidates = columnMappings.Values
                    .Where(m => !allKeyMembers.Contains(m.TableColumn.Name))
                    .Select(m => m)
                    .ToArray();
                if (updatedColumnNames.Any())
                {
                    modifiedColumnMappingCandidates = modifiedColumnMappingCandidates
                        .Where(c => updatedColumnNames.Contains(c.EntityProperty.Name)).ToArray();
                }

                var modifiedColumnMappings = modifiedColumnMappingCandidates.ToArray();

                //
                // Create and populate a temp table to hold the updated values.
                //
                var conn = GetSqlConnection(ctx);
                var tempTableName = FillTempTable(conn, entities, tableName, columnMappings, selectedKeyMappings,
                    modifiedColumnMappings, transaction);

                //
                // Update the target table using the temp table we just created.
                //
                var setStatements =
                    modifiedColumnMappings.Select(c => $"t0.[{c.TableColumn.Name}] = t1.[{c.TableColumn.Name}]");
                var setStatementsSql = string.Join(" , ", setStatements);
                var conditionStatements =
                    selectedKeyMappings.Select(c => $"t0.[{c.TableColumn.Name}] = t1.[{c.TableColumn.Name}]");
                var conditionStatementsSql = string.Join(" AND ", conditionStatements);
                var cmdBody = $@"UPDATE t0 SET {setStatementsSql}
                                 FROM {tableName.Fullname} AS t0
                                 INNER JOIN {tempTableName} AS t1 ON {conditionStatementsSql}
                                ";
                var cmd = CreateSqlCommand(cmdBody, conn, request.Transaction, request.CommandTimeout);
                rowsAffected += cmd.ExecuteNonQuery();

                if (request.InsertIfNew)
                {
                    var columns = columnMappings.Values
                        .Where(m => !primaryKeyMembers.Contains(m.TableColumn.Name))
                        .Select(m => m.TableColumn.Name)
                        .ToArray();
                    var columnNames = string.Join(",", columns.Select(c => $"[{c}]"));
                    var t0ColumnNames = string.Join(",", columns.Select(c => $"[t0].[{c}]"));
                    cmdBody = $@"INSERT INTO {tableName.Fullname}
                             SELECT {columnNames}
                             FROM {tempTableName}
                             EXCEPT
                             SELECT {t0ColumnNames}
                             FROM {tempTableName} AS t0
                             INNER JOIN {tableName.Fullname} AS t1 ON {conditionStatementsSql}            
                            ";
                    cmd = CreateSqlCommand(cmdBody, conn, request.Transaction, request.CommandTimeout);
                    rowsAffected += cmd.ExecuteNonQuery();
                }

                //
                // Clean up. Delete the temp table.
                //
                DropTempTable(conn, transaction, tempTableName);
            }

            response.AffectedRows.Add(new Tuple<Type, long>(t, rowsAffected));
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="entities"></param>
        /// <param name="sqlTransaction"></param>
        /// <param name="recursive"></param>
        /// <param name="allowNotNullSelfReferences"></param>
        /// <param name="commandTimeout"></param>
        /// <param name="savedEntities"></param>
        /// <param name="mappingsByType"></param>
        /// <param name="response"></param>
        private static void DoBulkInsertAll(
            this DbContext ctx,
            IList<dynamic> entities,
            SqlTransaction sqlTransaction,
            bool recursive,
            bool allowNotNullSelfReferences,
            TimeSpan commandTimeout,
            Dictionary<object, object> savedEntities,
            Dictionary<Type, Mappings> mappingsByType,
            BulkInsertResponse response)
        {
            if (entities.Count == 0) return;

            // exclude already existing entities

            Type t = entities[0].GetType();
            if (!mappingsByType.ContainsKey(t))
            {
                mappingsByType.Add(t, GetMappings(ctx, t));
            }

            var mappings = mappingsByType[t];

            // 
            // Find any 'one-or-zero to many' related entities. If found, those
            // entities should be persisted and their primary key values copied
            // to the foreign keys in these entities.
            //
            if (recursive)
            {
                //Trace.TraceInformation($"DoBulkInsertAll - ToForeignKeyMappings - {t.ToString()}");
                foreach (var fkMapping in mappings.ToForeignKeyMappings)
                {
                    // ToForeignKeyMappings means that the entity is connected TO
                    // another entity via a foreign key relationship. So, these mappings
                    // must always be one-to-one. At least I can't come up with a
                    // situation where we would be interested in a collection type
                    // mapping here. When we have self references collections appear
                    // here but we ignore them and take care of them in the from mapping.
                    var isCollection = fkMapping.BuiltInTypeKind == BuiltInTypeKind.CollectionType ||
                                       fkMapping.BuiltInTypeKind == BuiltInTypeKind.CollectionKind;
                    if (isCollection) continue;

                    var navigationPropertyName = fkMapping.NavigationPropertyName;

                    var navProperties = new HashSet<object>();
                    var modifiedEntities = new List<object[]>();
                    Type navPropertyType = null;
                    foreach (var entity in entities)
                    {
                        var navProperty = GetProperty(t, navigationPropertyName, entity);
                        if (navProperty != null)
                        {
                            if (navPropertyType == null)
                                navPropertyType = GetProperty(t, navigationPropertyName, entity).GetType();
                            foreach (var foreignKeyRelation in fkMapping.ForeignKeyRelations)
                            {
                                PropertyInfo toPropertyInfo = t.GetProperty(foreignKeyRelation.ToProperty);
                                var navPropertyKeyType = toPropertyInfo.PropertyType;
                                var isGuid = IsGuid(navPropertyKeyType);
                                var navPropertyKey = GetProperty(t, foreignKeyRelation.ToProperty, entity);

                                // we do nothing unless the one-to-one
                                // nav property in previously unknown
                                if (navPropertyKey == null ||
                                    (isGuid && navPropertyKey == Guid.Empty) ||
                                    navPropertyKey == 0)
                                {
                                    var currentValue = GetProperty(navPropertyType, foreignKeyRelation.FromProperty,
                                        navProperty);
                                    if ((isGuid && navPropertyKey != Guid.Empty) ||
                                        (!isGuid && currentValue > 0))
                                    {
                                        SetProperty(foreignKeyRelation.ToProperty, entity, currentValue);
                                    }
                                    else
                                    {
                                        var same = navProperty.GetType() == entity.GetType() &&
                                                   navProperty == entity;
                                        if (!same)

                                        {
                                            navProperties.Add(navProperty);
                                            modifiedEntities.Add(new object[] { entity, navProperty });
                                        }
                                    }
                                }
                            }
                        }
                    }

                    if (!navProperties.Any()) continue;

                    DoBulkInsertAll(
                        ctx,
                        navProperties.ToList(),
                        sqlTransaction,
                        recursive,
                        allowNotNullSelfReferences,
                        commandTimeout,
                        savedEntities,
                        mappingsByType,
                        response);
                    foreach (var modifiedEntity in modifiedEntities)
                    {
                        var e = modifiedEntity[0];
                        var p = modifiedEntity[1];
                        foreach (var foreignKeyRelation in fkMapping.ForeignKeyRelations)
                        {
                            SetProperty(foreignKeyRelation.ToProperty, e,
                                GetProperty(foreignKeyRelation.FromProperty, p));
                        }
                    }
                }
            }

            //
            // Some entities might appear in many places and there 
            // is no point in saving them more than once. We use a
            // custom comparer checking for reference identity.
            //
            var validEntities = new ArrayList();
            foreach (dynamic entity in entities)
            {
                if (savedEntities.ContainsKey(entity)) continue;

                validEntities.Add(entity);
                savedEntities.Add(entity, entity);
            }

            DoBulkCopy(ctx, validEntities, t, mappings, sqlTransaction, allowNotNullSelfReferences, commandTimeout,
                response);

            //
            // Any many-to-one (parent-child) foreign key related entities are found here. 
            // We collect all the children, save the parents, update the foreign key in
            // the children and then save the children. When we have self references we need
            // to handle that with care.
            //
            if (recursive)
            {
                var fkMappings =
                    mappings.FromForeignKeyMappings.Concat(
                        mappings.ToForeignKeyMappings.Where(m => m.AssociationMapping != null));
                foreach (var fkMapping in fkMappings)
                {
                    var isCollection = fkMapping.BuiltInTypeKind == BuiltInTypeKind.CollectionType ||
                                       fkMapping.BuiltInTypeKind == BuiltInTypeKind.CollectionKind;

                    var navigationPropertyName = fkMapping.NavigationPropertyName;

                    var navPropertyEntities = new List<dynamic>();
                    var navPropertySelfReferences = new List<SelfReference>();
                    var joinTableNavPropertiesByEntity = new Dictionary<dynamic, List<dynamic>>();
                    var joinTableNavProperties = new List<dynamic>();

                    foreach (var entity in validEntities)
                    {
                        if (isCollection)
                        {
                            var propertyCollection = GetProperty(t, navigationPropertyName, entity);
                            if (propertyCollection == null) continue;

                            var navProperties = new List<dynamic>();
                            foreach (var p in propertyCollection)
                            {
                                navProperties.Add(p);
                            }

                            if (navProperties.Count == 0) continue;


                            if (fkMapping.ForeignKeyRelations.Any())
                            {
                                foreach (var navProperty in navProperties)
                                {
                                    foreach (var foreignKeyRelation in fkMapping.ForeignKeyRelations)
                                    {
                                        SetProperty(foreignKeyRelation.ToProperty, navProperty,
                                            GetProperty(t, foreignKeyRelation.FromProperty, entity));
                                    }

                                    navPropertyEntities.Add(navProperty);
                                }
                            }
                            else if (fkMapping.AssociationMapping != null)
                            {
                                joinTableNavPropertiesByEntity.Add(entity, navProperties);
                                joinTableNavProperties.AddRange(navProperties);
                            }
                        }
                        else
                        {
                            var navProperty = GetProperty(t, navigationPropertyName, entity);
                            if (navProperty != null)
                            {
                                foreach (var foreignKeyRelation in fkMapping.ForeignKeyRelations)
                                {
                                    SetProperty(foreignKeyRelation.ToProperty, navProperty,
                                        GetProperty(t, foreignKeyRelation.FromProperty, entity));
                                }

                                var same = navProperty.GetType() == entity.GetType() &&
                                           navProperty == entity;
                                if (!same)
                                    navPropertyEntities.Add(navProperty);
                                else
                                    navPropertySelfReferences.Add(new SelfReference
                                    {
                                        Entity = entity,
                                        ForeignKeyProperties = fkMapping.ForeignKeyRelations.Select(p => p.ToProperty)
                                            .ToArray()
                                    });
                            }
                        }
                    }

                    if (joinTableNavPropertiesByEntity.Any())
                    {
                        var navPropertyType = (Type)joinTableNavProperties[0].GetType();
                        var pkColumnMappings = GetPrimaryKeyColumnMappings(ctx, navPropertyType, mappingsByType);
                        var notExistingNavProperties = BulkSelectNotExisting(
                            ctx,
                            navPropertyType,
                            joinTableNavProperties,
                            pkColumnMappings,
                            sqlTransaction);
                        DoBulkInsertAll(ctx,
                            notExistingNavProperties.ToArray(navPropertyType),
                            sqlTransaction,
                            recursive,
                            allowNotNullSelfReferences,
                            commandTimeout,
                            savedEntities,
                            mappingsByType,
                            response);

                        foreach (var joinTableNavPropertiesForEntity in joinTableNavPropertiesByEntity)
                        {
                            var entity = joinTableNavPropertiesForEntity.Key;
                            if (fkMapping.AssociationMapping.Source.EntityProperty.DeclaringType.Name ==
                                entity.GetType().Name)
                            {
                                foreach (var navProperty in joinTableNavPropertiesForEntity.Value)
                                {
                                    dynamic np = new ExpandoObject();
                                    AddProperty(np, fkMapping.AssociationMapping.Source.TableColumn.Name,
                                        GetProperty(t, fkMapping.AssociationMapping.Source.EntityProperty.Name,
                                            entity));
                                    AddProperty(np, fkMapping.AssociationMapping.Target.TableColumn.Name,
                                        GetProperty(navPropertyType,
                                            fkMapping.AssociationMapping.Target.EntityProperty.Name, navProperty));
                                    navPropertyEntities.Add(np);
                                }
                            }
                            else
                            {
                                foreach (var navProperty in joinTableNavPropertiesForEntity.Value)
                                {
                                    dynamic np = new ExpandoObject();
                                    AddProperty(np, fkMapping.AssociationMapping.Source.TableColumn.Name,
                                        GetProperty(navPropertyType,
                                            fkMapping.AssociationMapping.Source.EntityProperty.Name, navProperty));
                                    AddProperty(np, fkMapping.AssociationMapping.Target.TableColumn.Name,
                                        GetProperty(t, fkMapping.AssociationMapping.Target.EntityProperty.Name,
                                            entity));
                                    navPropertyEntities.Add(np);
                                }
                            }
                        }
                    }

                    if (navPropertySelfReferences.Any())
                    {
                        var request = new BulkUpdateRequest
                        {
                            Entities = navPropertySelfReferences.Select(e => e.Entity).Distinct().ToArray(),
                            UpdatedColumnNames = navPropertySelfReferences.SelectMany(e => e.ForeignKeyProperties)
                                .Distinct().ToArray(),
                            Transaction = sqlTransaction
                        };
                        DoBulkUpdateAll(
                            ctx,
                            request,
                            response);
                    }

                    if (navPropertyEntities.Any())
                    {
                        if (navPropertyEntities.First() is ExpandoObject)
                        {
                            // We have to create our own mappings for this one. Nothing
                            // available in our context. There should be something in there
                            // we could use but I cannot find it.
                            var expandoMappings = new Mappings
                            {
                                TableName = fkMapping.AssociationMapping.TableName,
                                ColumnMappingByPropertyName = new Dictionary<string, TableColumnMapping>()
                            };
                            expandoMappings.ColumnMappingByPropertyName.Add(
                                fkMapping.AssociationMapping.Source.TableColumn.Name,
                                new TableColumnMapping
                                {
                                    EntityProperty = fkMapping.AssociationMapping.Source.TableColumn,
                                    TableColumn = fkMapping.AssociationMapping.Source.TableColumn
                                });
                            expandoMappings.ColumnMappingByPropertyName.Add(
                                fkMapping.AssociationMapping.Target.TableColumn.Name,
                                new TableColumnMapping
                                {
                                    EntityProperty = fkMapping.AssociationMapping.Target.TableColumn,
                                    TableColumn = fkMapping.AssociationMapping.Target.TableColumn
                                });
                            DoBulkCopy(
                                ctx,
                                navPropertyEntities.ToArray(),
                                typeof(ExpandoObject),
                                expandoMappings,
                                sqlTransaction,
                                allowNotNullSelfReferences,
                                commandTimeout,
                                response);
                        }
                        else
                            DoBulkInsertAll(
                                ctx,
                                navPropertyEntities.ToArray(),
                                sqlTransaction,
                                recursive,
                                allowNotNullSelfReferences,
                                commandTimeout,
                                savedEntities,
                                mappingsByType,
                                response);
                    }
                }
            }
        }

        private static string[] GetPrimaryKeyMembers(Dictionary<string, TableColumnMapping> columnMappings)
        {
            dynamic declaringType = columnMappings
                .Values
                .First()
                .TableColumn
                .DeclaringType;

            var primaryKeyMembers = new List<string>();
            foreach (var keyMember in declaringType.KeyMembers)
                primaryKeyMembers.Add(keyMember.ToString());

            return primaryKeyMembers.ToArray();
        }

        private static TableColumnMapping[] GetPrimaryKeyColumnMappings(DbContext ctx, Type t,
            Dictionary<Type, Mappings> mappingsByType)
        {
            if (!mappingsByType.ContainsKey(t))
            {
                mappingsByType.Add(t, GetMappings(ctx, t));
            }

            var mappings = mappingsByType[t];
            var columnMappings = mappings.ColumnMappingByPropertyName;
            return GetPrimaryKeyColumnMappings(columnMappings);
        }

        private static TableColumnMapping[] GetPrimaryKeyColumnMappings(
            Dictionary<string, TableColumnMapping> columnMappings)
        {
            var primaryKeyMembers = GetPrimaryKeyMembers(columnMappings);
            return GetPrimaryKeyColumnMappings(columnMappings, primaryKeyMembers);
        }

        private static TableColumnMapping[] GetPrimaryKeyColumnMappings(
            Dictionary<string, TableColumnMapping> columnMappings, string[] primaryKeyMembers)
        {
            var pkColumnMappings = columnMappings.Values
                .Where(m => primaryKeyMembers.Contains(m.TableColumn.Name))
                .ToArray();
            return pkColumnMappings;
        }

        private static void DoBulkCopy(
            this DbContext ctx,
            IList entities,
            Type t,
            Mappings mappings,
            SqlTransaction transaction,
            bool allowNotNullSelfReferences,
            TimeSpan commandTimeout,
            BulkInsertResponse response)
        {
            // If we for some reason are called with an empty list we return immediately.
            if (entities.Count == 0) return;

            var rowsAffected = 0;
            bool hasComplexProperties = mappings.ComplexPropertyNames.Any();
            var tableName = mappings.TableName;
            var columnMappings = mappings.ColumnMappingByPropertyName;

            var conn = GetSqlConnection(ctx);

            // If we are dealing with entities with properties configured 
            // as complex types we need to flatten all entities. We use 
            // ExpandoObject for this since we are already compatible with
            // those little critters.
            if (hasComplexProperties)
            {
                IList flattenedEntities = new List<object>();
                foreach (var entity in entities)
                {
                    var flatEntity = new ExpandoObject();
                    Flatten(flatEntity, entity, mappings);
                    flattenedEntities.Add(flatEntity);
                }

                entities = flattenedEntities;
            }

            // Ignore all properties that we have no mappings for.
            var properties = GetProperties(entities[0])
                .Where(p => columnMappings.ContainsKey(p.Name)).ToArray();

            var table = new DataTable();

            // Check to see if the table has a primary key.
            var primaryKeyMembers = GetPrimaryKeyMembers(columnMappings);
            var pkColumnMappings = GetPrimaryKeyColumnMappings(columnMappings, primaryKeyMembers);

            // There are four different scenarios here:
            //
            // (1) There are no primary keys and since EF6 does not
            //     support this neither do we so we just throw an 
            //     exception.
            //
            // (2) Join tables are treated as a special case and we
            //     identify them by looking at the entity type which 
            //     for these tables always is ExpandoObject.
            //
            // (3) The table has a single column primary key that is
            //     generated or computed by the database. In these
            //     cases we need to perform some black magic in order
            //     to reliably retrieve these primary key values and
            //     update the corresponding entity objects. Separating
            //     new entities from previously existing entities is
            //     easy. We simply look at the primary key property
            //     and if it has no value the entity is a new one and
            //     it should be written to the database table.
            //
            // (4) In all other cases we use bulk copy directly to the
            //     target table (so no black magic required) but
            //     selecting the new entities requires an actual lookup
            //     in the database.

            if (pkColumnMappings.Length == 0)
            {
                throw new ArgumentException(
                    "No primary key found. This should not be possible since EF6 has no support for tables without a primary key.");
            }

            // Join tables are treated as a special case. However,
            // we should be able to do this with a direct bulk copy
            // if we select the new entities first instead of
            // excluding them in hte insert into statement.
            if (t == typeof(ExpandoObject))
            {
                var nonPrimaryKeyColumnMappings = columnMappings
                    .Values
                    .Except(pkColumnMappings)
                    .ToArray();
                var tempTableName = FillTempTable(
                    conn,
                    entities,
                    tableName,
                    columnMappings,
                    pkColumnMappings,
                    nonPrimaryKeyColumnMappings,
                    transaction);

                var conditionStatements =
                    pkColumnMappings.Select(c => $"[t0].[{c.TableColumn.Name}] = [t1].[{c.TableColumn.Name}]");
                var conditionStatementsSql = string.Join(" AND ", conditionStatements);

                string listOfPrimaryKeyColumns = string.Join(",",
                    pkColumnMappings.Select(c => $"[{c.TableColumn.Name}]"));
                string listOfColumns = string.Join(",",
                    pkColumnMappings.Concat(nonPrimaryKeyColumnMappings).Select(c => $"[{c.TableColumn.Name}]"));

                var cmdBody = $@"INSERT INTO {tableName.Fullname} ({listOfColumns})
                                 SELECT {listOfColumns} 
                                 FROM {tempTableName} AS [t0]
                                 WHERE NOT EXISTS (
                                    SELECT {listOfPrimaryKeyColumns}
                                    FROM {tableName.Fullname} AS [t1]
                                    WHERE {conditionStatementsSql}
                                 )
                                    ";
                var cmd = CreateSqlCommand(cmdBody, conn, transaction, commandTimeout);
                rowsAffected += cmd.ExecuteNonQuery();

                //
                // Clean up. Delete the temp table.
                //
                DropTempTable(conn, transaction, tempTableName);
            }
            else if (IsPrimaryKeyStoreGenerated(pkColumnMappings))
            {
                var pkColumn = pkColumnMappings[0].TableColumn;
                var pkProperty = pkColumnMappings[0].EntityProperty;

                var newEntities = SelectNewEntities(entities, pkProperty, t);

                //
                // Save new entities to the database table making sure that the entities
                // are updated with their generated or computed primary key.
                //
                if (newEntities.Count > 0)
                {
                    var allColumnNames = columnMappings.Values.Select(v => v.TableColumn.Name).ToArray();
                    var tempTableName =
                        CreateTempTable(conn, transaction, tableName, allColumnNames, IncludeRowNumber.Yes);

                    var bulkCopy = CreateBulkCopy(
                        table,
                        properties,
                        columnMappings,
                        conn,
                        transaction,
                        tempTableName,
                        SqlBulkCopyOptions.Default,
                        IncludeRowNumber.Yes);

                    AddEntitiesToTable(table, newEntities, properties, t, IncludeRowNumber.Yes);

                    var s = new Stopwatch();
                    s.Start();
                    bulkCopy.WriteToServer(table.CreateDataReader());
                    s.Stop();
                    var stats = new BulkInsertStatistics
                    {
                        TimeElapsedDuringBulkCopy = s.Elapsed
                    };

                    var pkColumnType = Type.GetType(pkColumn.PrimitiveType.ClrEquivalentType.FullName);

                    var nonPrimaryKeyColumnMappings = columnMappings.Values
                        .Where(m => !primaryKeyMembers.Contains(m.TableColumn.Name))
                        .Where(m => !m.TableColumn.IsStoreGeneratedComputed)
                        .Where(m => !m.TableColumn.IsStoreGeneratedIdentity)
                        .ToArray();

                    rowsAffected += SelectIntoUsingOutputClause(
                        conn,
                        transaction,
                        tableName,
                        pkColumnType,
                        allowNotNullSelfReferences,
                        response,
                        nonPrimaryKeyColumnMappings,
                        pkColumn,
                        tempTableName,
                        s,
                        stats,
                        newEntities,
                        pkProperty,
                        hasComplexProperties,
                        t);
                }
            }
            else
            {
                var bulkCopy = CreateBulkCopy(
                    table,
                    properties,
                    columnMappings,
                    conn,
                    transaction,
                    tableName.Fullname,
                    SqlBulkCopyOptions.Default,
                    IncludeRowNumber.No);

                // Make sure that we only insert entities not already in the database.
                var notExistingEntities = BulkSelectNotExisting(ctx, t, entities, pkColumnMappings, transaction);
                AddEntitiesToTable(table, notExistingEntities, properties, t, IncludeRowNumber.No);
                rowsAffected += notExistingEntities.Count;

                var s = new Stopwatch();
                s.Start();
                bulkCopy.WriteToServer(table.CreateDataReader());
                s.Stop();
                var stats = new BulkInsertStatistics
                {
                    TimeElapsedDuringBulkCopy = s.Elapsed
                };
                response.BulkInsertStatistics.Add(new Tuple<Type, BulkInsertStatistics>(t, stats));
            }

            response.AffectedRows.Add(new Tuple<Type, long>(t, rowsAffected));
        }

        /// <summary>
        /// The assumption here is that db generated primary keys
        /// are always one-column primary keys. I.e, not composite
        /// keys.
        /// </summary>
        /// <param name="pkColumnMappings"></param>
        /// <returns></returns>
        private static bool IsPrimaryKeyStoreGenerated(TableColumnMapping[] pkColumnMappings)
        {
            return pkColumnMappings.Length == 1 &&
                   (pkColumnMappings[0].TableColumn.IsStoreGeneratedIdentity ||
                    pkColumnMappings[0].TableColumn.IsStoreGeneratedComputed);
        }

        private static int SelectIntoForIntegerTypePrimaryKey(
            SqlConnection conn,
            SqlTransaction transaction,
            TableName tableName,
            Type pkColumnType,
            bool allowNotNullSelfReferences,
            BulkInsertResponse response,
            TableColumnMapping[] nonPrimaryKeyColumnMappings,
            EdmProperty pkColumn,
            string tempTableName,
            Stopwatch s,
            BulkInsertStatistics stats,
            ArrayList newEntities,
            EdmProperty pkProperty,
            bool hasComplexProperties,
            Type t)
        {
            var cmd = conn.CreateCommand();
            cmd.CommandTimeout = (int)TimeSpan.FromMinutes(30).TotalSeconds;
            cmd.Transaction = transaction;

            // Get the number of existing rows in the table.
            cmd.CommandText = $@"SELECT CASE WHEN EXISTS (SELECT TOP 1 * FROM {tableName.Fullname}) THEN 1 ELSE 0 END";
            var result = cmd.ExecuteScalar();
            var count = Convert.ToInt64(result);

            // Get the identity increment value
            cmd.CommandText = $"SELECT IDENT_INCR('{tableName.Fullname}')";
            result = cmd.ExecuteScalar();
            dynamic identIncrement = Convert.ChangeType(result, pkColumnType);

            // Get the last identity value generated for our table
            cmd.CommandText = $"SELECT IDENT_CURRENT('{tableName.Fullname}')";
            result = cmd.ExecuteScalar();
            dynamic identcurrent = Convert.ChangeType(result, pkColumnType);

            var nextId = identcurrent + (count > 0 ? identIncrement : 0);

            string query;
            if (allowNotNullSelfReferences)
            {
                query = $"ALTER TABLE {tableName.Fullname} NOCHECK CONSTRAINT ALL";
                cmd.CommandText = query;
                cmd.ExecuteNonQuery();
                response.TablesWithNoCheckConstraints.Add(tableName.Fullname);
            }

            var columnNames = string.Join(",", nonPrimaryKeyColumnMappings.Select(p => $"[{p.TableColumn.Name}]"));
            query = $@"                                  
                        INSERT INTO {tableName.Fullname} ({columnNames})                        
                        SELECT {columnNames}
                        FROM   {tempTableName}
                        ORDER BY rowno
                      ";
            cmd.CommandText = query;
            s.Restart();
            int rowsAffected = cmd.ExecuteNonQuery();
            s.Stop();
            stats.TimeElapsedDuringInsertInto = s.Elapsed;
            response.BulkInsertStatistics.Add(new Tuple<Type, BulkInsertStatistics>(t, stats));

            cmd.CommandText = $"SELECT SCOPE_IDENTITY()";
            result = cmd.ExecuteScalar();
            dynamic lastId = Convert.ChangeType(result, pkColumnType);

            cmd.CommandText =
                $"SELECT [{pkColumn.Name}] From {tableName.Fullname} WHERE [{pkColumn.Name}] >= {nextId} and [{pkColumn.Name}] <= {lastId}";

            object[] ids = null;
            using (var sqlDataReader = cmd.ExecuteReader())
            {
                ids = (from IDataRecord r in sqlDataReader
                       let pk = r[pkColumn.Name]
                       select pk)
                    .OrderBy(i => i)
                    .ToArray();
            }

            if (ids.Length != newEntities.Count)
                throw new ArgumentException(
                    "More id values generated than we had entities. Something went wrong, try again.");

            for (int i = 0; i < newEntities.Count; i++)
            {
                SetProperty(pkProperty.Name, newEntities[i], ids[i]);

                if (hasComplexProperties)
                {
                    var dict = (IDictionary<string, object>)newEntities[i];
                    if (dict.ContainsKey("#OriginalEntity"))
                    {
                        SetProperty(pkProperty.Name, dict["#OriginalEntity"], ids[i]);
                    }
                }
            }

            return rowsAffected;
        }

        private static int SelectIntoUsingOutputClause(
            SqlConnection conn,
            SqlTransaction transaction,
            TableName tableName,
            Type pkColumnType,
            bool allowNotNullSelfReferences,
            BulkInsertResponse response,
            TableColumnMapping[] nonPrimaryKeyColumnMappings,
            EdmProperty pkColumn,
            string tempTableName,
            Stopwatch s,
            BulkInsertStatistics stats,
            ArrayList newEntities,
            EdmProperty pkProperty,
            bool hasComplexProperties,
            Type t)
        {
            var cmd = conn.CreateCommand();
            cmd.CommandTimeout = (int)TimeSpan.FromMinutes(30).TotalSeconds;
            cmd.Transaction = transaction;

            string query;
            if (allowNotNullSelfReferences)
            {
                query = $"ALTER TABLE {tableName.Fullname} NOCHECK CONSTRAINT ALL";
                cmd.CommandText = query;
                cmd.ExecuteNonQuery();
                response.TablesWithNoCheckConstraints.Add(tableName.Fullname);
            }

            var columnNames = string.Join(",", nonPrimaryKeyColumnMappings.Select(p => $"[{p.TableColumn.Name}]"));
            query = $@"  
                        MERGE {tableName.Fullname}
                        USING 
                            (SELECT {columnNames}, rowno
                             FROM   {tempTableName}) t ({columnNames}, rowno)
                        ON 1 = 0
                        WHEN NOT MATCHED THEN
                        INSERT ({columnNames})
                        VALUES ({columnNames})
                        OUTPUT t.rowno,
                               inserted.[{pkColumn.Name}]; 
                                 ";
            cmd.CommandText = query;
            s.Restart();
            object[] ids = null;
            using (var reader = cmd.ExecuteReader())
            {
                ids = (
                    from IDataRecord r in reader
                    let pk = r[pkColumn.Name]
                    let rno = r["rowno"]
                    orderby rno
                    select pk).ToArray();
            }

            s.Stop();
            stats.TimeElapsedDuringInsertInto = s.Elapsed;
            response.BulkInsertStatistics.Add(new Tuple<Type, BulkInsertStatistics>(t, stats));

            if (ids.Length != newEntities.Count)
                throw new ArgumentException(
                    $@"Inserting {newEntities.Count} entities of type {t} generated {ids.Length} primary key identities. Weird shit. Please log a bug report.");

            for (int i = 0; i < newEntities.Count; i++)
            {
                SetProperty(pkProperty.Name, newEntities[i], ids[i]);

                if (hasComplexProperties)
                {
                    var dict = (IDictionary<string, object>)newEntities[i];
                    if (dict.ContainsKey("#OriginalEntity"))
                    {
                        SetProperty(pkProperty.Name, dict["#OriginalEntity"], ids[i]);
                    }
                }
            }

            return newEntities.Count;
        }

        private static void AddEntitiesToTable(DataTable table, IList entities, BulkPropertyInfo[] properties, Type t,
            IncludeRowNumber includeRowNumber)
        {
            if (entities.Count == 0) return;

            if (entities[0] is ExpandoObject)
            {
                long i = 1;
                foreach (var entity in entities)
                {
                    var e = (ExpandoObject)entity;
                    var columnValues = properties.Select(p => GetProperty(p.Name, e)).ToList();
                    if (includeRowNumber == IncludeRowNumber.Yes) columnValues.Add(i++);
                    table.Rows.Add(columnValues.ToArray());
                }
            }
            else
            {
                long i = 1;
                foreach (var entity in entities)
                {
                    var e = entity;
                    var columnValues = properties.Select(p => GetProperty(t, p.Name, e, DBNull.Value)).ToList();
                    if (includeRowNumber == IncludeRowNumber.Yes) columnValues.Add(i++);
                    table.Rows.Add(columnValues.ToArray());
                }
            }
        }

        /// <summary>
        /// The purpose of this method is to figure out which of the supplied
        /// entities are new to the database. For tables with database
        /// generated primary keys this is fairly straight forward but for other
        /// tables we need to actually have a look in the database table.
        /// We can use our BulkSelectNotExisting to do this but it requires
        /// some extra magic since we must invoke the generic method with
        /// run-time types. It gets a little messy but it works.
        /// </summary>
        /// <param name="entities"></param>
        /// <param name="pkProperty"></param>
        /// <param name="t"></param>
        /// <returns></returns>
        private static ArrayList SelectNewEntities(IList entities, EdmProperty pkProperty, Type t)
        {
            var newEntities = new ArrayList();

            if (entities[0] is ExpandoObject)
            {
                foreach (var entity in entities)
                {
                    var e = (ExpandoObject)entity;
                    var pk = GetProperty(pkProperty.Name, e);
                    var isGuid = IsGuidProperty(pkProperty);
                    if ((!isGuid && pk == 0) ||
                        isGuid && pk == Guid.Empty)
                        newEntities.Add(entity);
                }
            }
            else
            {
                foreach (var entity in entities)
                {
                    var pk = GetProperty(t, pkProperty.Name, entity);
                    var isGuid = IsGuidProperty(pkProperty);
                    if ((!isGuid && pk == 0) ||
                        isGuid && pk == Guid.Empty)
                        newEntities.Add(entity);
                }
            }

            return newEntities;
        }

        private static void Flatten(IDictionary<string, object> flatEntity, object entity, Mappings mappings)
        {
            var navigationPropertyNames = new List<string>();

            // Flatten uses a recursive pattern but the initial call needs to 
            // save the original entity, untouched, so that we can later update
            // generated identity columns after the bulk insert has finished.
            // The mappings argument must NEVER be set in recursive calls.
            if (mappings != null)
            {
                flatEntity.Add("#OriginalEntity", entity);
                navigationPropertyNames.AddRange(mappings.ToForeignKeyMappings.Select(m => m.NavigationPropertyName));
                navigationPropertyNames.AddRange(mappings.FromForeignKeyMappings.Select(m => m.NavigationPropertyName));
            }


            Type t = entity.GetType();
            var properties = t.GetProperties();
            foreach (var property in properties.Where(p => !navigationPropertyNames.Contains(p.Name)))
            {
                var val = property.GetValue(entity);

                // We should only have a mapping instance in the very first call
                // to this method. All consecutive recursive calls should set
                // the mappings argument to null.
                if (mappings != null)
                {
                    var complexPropertyNames = mappings.ComplexPropertyNames;
                    if (complexPropertyNames.Any(n => n == property.Name))
                    {
                        Flatten(flatEntity, val, null);
                    }
                    else
                    {
                        flatEntity.Add(property.Name, val);
                    }
                }
                // The only way that we could get here is if we have been called 
                // recursivly and that should ONLY happen if we are traversing a
                // hierarchy of complex types.
                else
                {
                    var t0 = property.PropertyType;
                    if (t0.IsValueType || t0.UnderlyingSystemType.Name == "String")
                    {
                        flatEntity.Add(property.Name, val);
                    }
                    else
                    {
                        Flatten(flatEntity, val, null);
                    }
                }
            }
        }


        private static string[] GetClusteredIndexColumns(
            DbContext ctx,
            string tableName,
            SqlTransaction sqlTransaction,
            Mappings mappings)
        {
            var connection = GetSqlConnection(ctx);

            string query = $@"
                    SELECT  col.name
                    FROM sys.indexes ind
                    INNER JOIN sys.index_columns ic ON ind.object_id = ic.object_id and ind.index_id = ic.index_id
                    INNER JOIN sys.columns col ON ic.object_id = col.object_id and ic.column_id = col.column_id
                    INNER JOIN sys.tables t ON ind.object_id = t.object_id
                    WHERE t.name = '{tableName}' AND ind.type_desc = 'CLUSTERED'
                    ORDER BY ic.index_column_id;";
            var cmd = CreateSqlCommand(query, connection, sqlTransaction, TimeSpan.FromSeconds(30));

            string[] clusteredColumns = null;
            using (var reader = cmd.ExecuteReader())
            {
                clusteredColumns = (
                        from IDataRecord r in reader
                        select (string)r[0])
                    .ToArray();
            }

            // Property names might not be identical to column 
            // names and we need the property names.
            return clusteredColumns.Select(c => mappings.ColumnMappingByColumnName[c].EntityProperty.Name).ToArray();
        }

        private static IList<T> Sort<T>(IList<T> entities, string[] sortColumns)
        {
            if (sortColumns.Any())
            {
                var t = entities[0].GetType();
                var sortedEntities = entities.OrderBy(u => t.GetProperty(sortColumns[0]).GetValue(u));
                foreach (var col in sortColumns.Skip(1))
                {
                    sortedEntities = sortedEntities.ThenBy(u => t.GetProperty(col).GetValue(u));
                }

                return sortedEntities.ToList();
            }

            return entities;
        }

        private static string FillTempTable(
            SqlConnection conn,
            IList entities,
            TableName tableName,
            Dictionary<string, TableColumnMapping> columnMappings,
            TableColumnMapping[] keyColumnMappings,
            TableColumnMapping[] nonKeyColumnMappings,
            SqlTransaction sqlTransaction)
        {
            var columnNames = keyColumnMappings.Select(m => m.TableColumn.Name)
                .Concat(nonKeyColumnMappings.Select(m => m.TableColumn.Name)).ToArray();

            var tempTableName = CreateTempTable(conn, sqlTransaction, tableName, columnNames, IncludeRowNumber.Yes);

            if (keyColumnMappings.Length == 1 &&
                ((keyColumnMappings[0].TableColumn.IsStoreGeneratedIdentity &&
                  keyColumnMappings[0].TableColumn.TypeName != "uniqueidentifier") ||
                 keyColumnMappings[0].TableColumn.IsStoreGeneratedComputed))
            {
                EnableIdentityInsert(tempTableName, conn, sqlTransaction);
            }

            var allProperties = GetProperties(entities[0]);
            //
            // Select the primary key clr properties 
            //
            var pkColumnProperties = allProperties
                .Where(p => keyColumnMappings.Any(m => m.EntityProperty.Name == p.Name))
                .ToArray();
            //
            // Select the clr properties for the selected non primary key columns.
            //
            var selectedColumnProperties = allProperties
                .Where(p => nonKeyColumnMappings.Any(m => m.EntityProperty.Name == p.Name))
                .ToArray();
            var properties = pkColumnProperties.Concat(selectedColumnProperties).ToArray();

            var table = new DataTable();
            var bulkCopy = CreateBulkCopy(
                table,
                properties,
                columnMappings,
                conn,
                sqlTransaction,
                tempTableName,
                SqlBulkCopyOptions.KeepIdentity,
                IncludeRowNumber.Yes);

            var type = entities[0].GetType();
            AddEntitiesToTable(table, entities, properties, type, IncludeRowNumber.Yes);

            //
            // Fill the temp table.
            //
            bulkCopy.WriteToServer(table.CreateDataReader());

            return tempTableName;
        }

        private static void EnableIdentityInsert(string tableName, SqlConnection conn, SqlTransaction sqlTransaction)
        {
            var query = $@"SET IDENTITY_INSERT {tableName} ON";
            var cmd = CreateSqlCommand(query, conn, sqlTransaction, TimeSpan.FromSeconds(30));
            cmd.ExecuteNonQuery();
        }

        private static void DisableIdentityInsert(string tableName, SqlConnection conn, SqlTransaction sqlTransaction)
        {
            var query = $@"SET IDENTITY_INSERT {tableName} OFF";
            var cmd = CreateSqlCommand(query, conn, sqlTransaction, TimeSpan.FromSeconds(30));
            cmd.ExecuteNonQuery();
        }

        private static BulkPropertyInfo[] GetProperties(Type t)
        {
            var bulkProperties = t.GetProperties().Select(p => new RegularBulkPropertyInfo
            {
                PropertyInfo = p
            }).ToArray();

            return bulkProperties.Cast<BulkPropertyInfo>().ToArray();
        }

        private static BulkPropertyInfo[] GetProperties(object o)
        {
            if (o is ExpandoObject)
            {
                var props = new List<ExpandoBulkPropertyInfo>();
                var dict = (IDictionary<string, object>)o;

                // Since we cannot get the type for expando properties
                // with null values we skip them. Doing so is safe since
                // we are not really concerned with storing null values
                // in table columns. They tend to store themselves just,
                // fine. If we have a null value for a non-null column
                // we have a problem but then the problem is that we have
                // a null value in our expando object, not that we skip
                // it here.
                foreach (var kvp in dict.Where(kvp => kvp.Value != null))
                {
                    props.Add(new ExpandoBulkPropertyInfo
                    {
                        Name = kvp.Key,
                        Type = kvp.Value.GetType()
                    });
                }

                return props.Cast<BulkPropertyInfo>().ToArray();
            }

            Type t = o.GetType();
            var bulkProperties = t.GetProperties().Select(p => new RegularBulkPropertyInfo
            {
                PropertyInfo = p
            }).ToArray();

            return bulkProperties.Cast<BulkPropertyInfo>().ToArray();
        }

        private static void AddProperty(ExpandoObject expando, string propertyName, object propertyValue)
        {
            // ExpandoObject supports IDictionary so we can extend it like this
            var expandoDict = expando as IDictionary<string, object>;
            if (expandoDict.ContainsKey(propertyName))
                expandoDict[propertyName] = propertyValue;
            else
                expandoDict.Add(propertyName, propertyValue);
        }

        public static SqlConnection GetSqlConnection(this DbContext ctx)
        {
            var conn = (SqlConnection)ctx.Database.Connection;
            if (conn.State == ConnectionState.Closed)
                conn.Open();

            return conn;
        }

        public static TableName GetTableName(this DbContext ctx, Type t)
        {
            var dbSet = ctx.Set(t);
            var sql = dbSet.ToString();
            var regex = new Regex(@"FROM (?<table>.*) AS");
            var match = regex.Match(sql);
            var name = match.Groups["table"].Value;

            var n = name.Replace("[", "").Replace("]", "");
            var m = Regex.Match(n, @"(.*)\.(.*)");
            if (m.Success)
            {
                return new TableName { Schema = m.Groups[1].Value, Name = m.Groups[2].Value };
            }

            m = Regex.Match(n, @"(.*)");
            if (m.Success)
            {
                return new TableName { Schema = "dbo", Name = m.Groups[1].Value };
            }

            throw new ArgumentException($"Failed to parse tablename {name}. Bulk operation failed.");
        }

        private static IEnumerable<TableColumnMapping> GetTableColumnMappings(ICollection<PropertyMapping> properties,
            bool isIncludedFromComplexType)
        {
            if (!properties.Any()) yield break;

            var scalarPropertyMappings =
                properties
                    .Where(p => p is ScalarPropertyMapping)
                    .Cast<ScalarPropertyMapping>()
                    .Select(p => new TableColumnMapping
                    {
                        IsIncludedFromComplexType = isIncludedFromComplexType,
                        EntityProperty = p.Property,
                        TableColumn = p.Column
                    });
            foreach (var mapping in scalarPropertyMappings)
            {
                yield return mapping;
            }

            var complexPropertyMappings =
                properties
                    .Where(p => p is ComplexPropertyMapping)
                    .Cast<ComplexPropertyMapping>()
                    .SelectMany(m => m.TypeMappings.SelectMany(tm => tm.PropertyMappings)).ToArray();
            ;
            foreach (var p in GetTableColumnMappings(complexPropertyMappings, true))
            {
                yield return p;
            }
        }

        private static Mappings GetMappings(DbContext ctx, Type t)
        {
            var objectContext = ((IObjectContextAdapter)ctx).ObjectContext;
            var workspace = objectContext.MetadataWorkspace;
            var containerName = objectContext.DefaultContainerName;
            t = ObjectContext.GetObjectType(t);
            var entityName = t.Name;

            var storageMapping =
                (EntityContainerMapping)workspace.GetItem<GlobalItem>(containerName, DataSpace.CSSpace);
            var entitySetMaps = storageMapping.EntitySetMappings.ToList();
            var associationSetMaps = storageMapping.AssociationSetMappings.ToList();

            //
            // Add mappings for all scalar properties. That is, for all properties  
            // that do not represent other entities (navigation properties).
            //
            var entitySetMap = entitySetMaps.Single(m => m.EntitySet.ElementType.Name == entityName);
            var typeMappings = entitySetMap.EntityTypeMappings;
            EntityTypeMapping typeMapping = typeMappings[0];
            var fragments = typeMapping.Fragments;
            var fragment = fragments[0];
            var propertyMappings = fragment.PropertyMappings;

            var columnMappings = new List<TableColumnMapping>();
            columnMappings.AddRange(
                propertyMappings
                    .Where(p => p is ScalarPropertyMapping)
                    .Cast<ScalarPropertyMapping>()
                    .Select(p => new TableColumnMapping
                    {
                        EntityProperty = p.Property,
                        TableColumn = p.Column
                    }));
            var complexPropertyMappings = propertyMappings
                .Where(p => p is ComplexPropertyMapping)
                .Cast<ComplexPropertyMapping>()
                .ToArray();
            if (complexPropertyMappings.Any())
            {
                columnMappings.AddRange(GetTableColumnMappings(complexPropertyMappings, true));
            }

            var columnMappingByPropertyName = columnMappings.ToDictionary(m => m.EntityProperty.Name, m => m);
            var columnMappingByColumnName = columnMappings.ToDictionary(m => m.TableColumn.Name, m => m);

            //
            // Add mappings for all navigation properties.
            //
            //
            var foreignKeyMappings = new List<ForeignKeyMapping>();
            var navigationProperties =
                typeMapping.EntityType.DeclaredMembers
                    .Where(m => m.BuiltInTypeKind == BuiltInTypeKind.NavigationProperty)
                    .Cast<NavigationProperty>()
                    .Where(p => p.RelationshipType is AssociationType)
                    .ToArray();

            foreach (var navigationProperty in navigationProperties)
            {
                var relType = (AssociationType)navigationProperty.RelationshipType;

                // Only bother with unknown relationships
                if (foreignKeyMappings.All(m => m.NavigationPropertyName != navigationProperty.Name))
                {
                    var fkMapping = new ForeignKeyMapping
                    {
                        NavigationPropertyName = navigationProperty.Name,
                        BuiltInTypeKind = navigationProperty.TypeUsage.EdmType.BuiltInTypeKind,
                    };

                    //
                    // Many-To-Many
                    //
                    if (associationSetMaps.Any() &&
                        associationSetMaps.Any(m => m.AssociationSet.Name == relType.Name))
                    {
                        var map = associationSetMaps.Single(m => m.AssociationSet.Name == relType.Name);
                        var sourceMapping =
                            new TableColumnMapping
                            {
                                TableColumn = map.SourceEndMapping.PropertyMappings[0].Column,
                                EntityProperty = map.SourceEndMapping.PropertyMappings[0].Property,
                            };
                        var targetMapping =
                            new TableColumnMapping
                            {
                                TableColumn = map.TargetEndMapping.PropertyMappings[0].Column,
                                EntityProperty = map.TargetEndMapping.PropertyMappings[0].Property,
                            };

                        fkMapping.FromType = (map.SourceEndMapping.AssociationEnd.TypeUsage.EdmType as RefType)
                            ?.ElementType.Name;
                        fkMapping.ToType = (map.TargetEndMapping.AssociationEnd.TypeUsage.EdmType as RefType)
                            ?.ElementType.Name;
                        var schema = map.StoreEntitySet.Schema;
                        var name = map.StoreEntitySet.Table ?? map.StoreEntitySet.Name;

                        fkMapping.AssociationMapping = new AssociationMapping
                        {
                            TableName = new TableName
                            {
                                Name = name,
                                Schema = schema,
                            },
                            Source = sourceMapping,
                            Target = targetMapping
                        };
                    }
                    //
                    // One-To-One or One-to-Many
                    //
                    else
                    {
                        fkMapping.FromType = relType.Constraint.FromProperties.First().DeclaringType.Name;
                        fkMapping.ToType = relType.Constraint.ToProperties.First().DeclaringType.Name;

                        var foreignKeyRelations = new List<ForeignKeyRelation>();
                        for (int i = 0; i < relType.Constraint.FromProperties.Count; i++)
                        {
                            foreignKeyRelations.Add(new ForeignKeyRelation
                            {
                                FromProperty = relType.Constraint.FromProperties[i].Name,
                                ToProperty = relType.Constraint.ToProperties[i].Name,
                            });
                        }

                        fkMapping.ForeignKeyRelations = foreignKeyRelations.ToArray();
                    }

                    foreignKeyMappings.Add(fkMapping);
                }
            }

            var tableName = GetTableName(ctx, t);

            var mappings = new Mappings
            {
                TableName = tableName,
                ComplexPropertyNames = complexPropertyMappings.Select(m => m.Property.Name).ToArray(),
                ColumnMappingByPropertyName = columnMappingByPropertyName,
                ColumnMappingByColumnName = columnMappingByColumnName,
                ToForeignKeyMappings = foreignKeyMappings.Where(m => m.ToType == entityName).ToArray(),
                FromForeignKeyMappings = foreignKeyMappings.Where(m => m.FromType == entityName).ToArray()
            };

            foreach (var toPropertyName in mappings.ToForeignKeyMappings.SelectMany(m =>
                m.ForeignKeyRelations.Select(r => r.ToProperty)))
            {
                if (mappings.ColumnMappingByPropertyName.ContainsKey(toPropertyName))
                {
                    var tableColumnMapping = mappings.ColumnMappingByPropertyName[toPropertyName];
                    tableColumnMapping.IsForeignKey = true;
                }
            }

            foreach (var toPropertyName in mappings.FromForeignKeyMappings.SelectMany(m =>
                m.ForeignKeyRelations.Select(r => r.ToProperty)))
            {
                if (mappings.ColumnMappingByPropertyName.ContainsKey(toPropertyName))
                {
                    var tableColumnMapping = mappings.ColumnMappingByPropertyName[toPropertyName];
                    tableColumnMapping.IsForeignKey = true;
                }
            }

            var associationMappings = mappings.ToForeignKeyMappings
                .Where(m => m.AssociationMapping != null)
                .Select(m => m.AssociationMapping);
            foreach (var associationMapping in associationMappings)
            {
                associationMapping.Source.IsForeignKey = true;
                associationMapping.Target.IsForeignKey = true;
            }

            associationMappings = mappings.FromForeignKeyMappings
                .Where(m => m.AssociationMapping != null)
                .Select(m => m.AssociationMapping);
            foreach (var associationMapping in associationMappings)
            {
                associationMapping.Source.IsForeignKey = true;
                associationMapping.Target.IsForeignKey = true;
            }

            return mappings;
        }

        /// <summary>
        /// Use reflection to get the property value by its property 
        /// name from an object instance.
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="instance"></param>
        /// <param name="def"></param>
        /// <returns></returns>
        private static dynamic GetProperty(string propertyName, object instance, object def = null)
        {
            var t = instance.GetType();
            return GetProperty(t, propertyName, instance, def);
        }

        private static dynamic GetProperty(Type t, string propertyName, object instance, object def = null)
        {
            if (t.IsPrimitive) return instance;
            if (t == typeof(string)) return instance;

            var property = t.GetProperty(propertyName);
            return GetProperty(property, instance, def);
        }

        private static dynamic GetProperty(string propertyName, ExpandoObject instance)
        {
            var dict = (IDictionary<string, object>)instance;
            return dict[propertyName];
        }

        private static dynamic GetProperty(PropertyInfo property, object instance, object def = null)
        {
            var val = property.GetValue(instance);
            return val ?? def;
        }

        private static bool IsGuidProperty(dynamic p)
        {
            var isGuid = p.TypeName == "Guid" || p.TypeName == "uniqueidentifier";
            return isGuid;
        }

        private static bool IsGuid(Type t)
        {
            var isGuid = (t == typeof(Guid) || t == typeof(Guid?));
            return isGuid;
        }

        /// <summary>
        /// Use reflection to set a property value by its property 
        /// name to an object instance.
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="instance"></param>
        /// <param name="value"></param>
        private static void SetProperty(string propertyName, object instance, object value)
        {
            if (value == DBNull.Value) return;

            if (instance is ExpandoObject)
            {
                var dict = (IDictionary<string, object>)instance;
                dict[propertyName] = value;
            }
            else
            {
                var type = instance.GetType();
                var property = type.GetProperty(propertyName);
                property.SetValue(instance, value);
            }
        }

        private static void SetProperty(BulkPropertyInfo property, object instance, object value)
        {
            if (value == DBNull.Value) return;

            if (property is RegularBulkPropertyInfo) property.PropertyInfo.SetValue(instance, value);
            else
            {
                var dict = (IDictionary<string, object>)instance;
                dict[property.Name] = value;
            }
        }

        private static SqlCommand CreateSqlCommand(
            string query,
            SqlConnection connection,
            SqlTransaction transaction,
            TimeSpan timeout)
        {
            var cmd = new SqlCommand(query, connection, transaction);
            cmd.CommandTimeout = (int)timeout.TotalSeconds;
            return cmd;
        }
        #endregion
    }
}