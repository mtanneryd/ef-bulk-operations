/*
 * Copyright ©  2017, 2018 Tånneryd IT AB
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
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Core.Metadata.Edm;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using Tanneryd.BulkInsert.Model;

namespace Tanneryd.BulkInsert
{
    public static class DbContextExtensions
    {
        #region Public API

        /// <summary>
        /// All columns of the entities' corresponding table rows 
        /// will be updated using the table primary key. If a 
        /// transaction object is provided the update will be made
        /// within that transaction. Tables witn no primary key will
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

            var globalId = CreateGlobalId(ctx);

            using (var mutex = new Mutex(false, globalId))
            {
                if (mutex.WaitOne())
                {
                    try
                    {
                        DoBulkUpdateAll(ctx, request, response);
                        
                    }
                    finally
                    {
                        mutex.ReleaseMutex();
                    }
                }
            }

            return response;
        }



        /// <summary>
        /// Insert all entities using System.Data.SqlClient.SqlBulkCopy. 
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="entities"></param>
        /// <param name="transaction"></param>
        /// <param name="recursive">True if the entire entity graph should be inserted, false otherwise.</param>
        public static BulkOperationResponse BulkInsertAll(
            this DbContext ctx,
            IList entities,
            SqlTransaction transaction = null,
            bool recursive = false)
        {
            var response = new BulkOperationResponse();
            if (entities.Count == 0) return response;

            var s = new Stopwatch();
            s.Start();

            var globalId = CreateGlobalId(ctx);

            using (var mutex = new Mutex(false, globalId))
            {
                if (mutex.WaitOne())
                {
                    try
                    {
                        DoBulkInsertAll(
                            ctx, 
                            entities, 
                            transaction, 
                            recursive,
                            new Dictionary<object, object>(new IdentityEqualityComparer<object>()),
                            response);
                    }
                    finally
                    {
                        mutex.ReleaseMutex();

                    }
                }
            }

            s.Stop();
            response.Elapsed = s.Elapsed;


            return response;
        }

        #endregion

        #region Private methods

        //
        // Private methods
        //
        private static void DoBulkUpdateAll(
            this DbContext ctx,
            BulkUpdateRequest request,
            BulkOperationResponse response)
        {
            var rowsAffected = 0;

            var keyMemberNames = request.KeyMemberNames;
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
            dynamic declaringType = columnMappings
                .Values
                .First().TableColumn.DeclaringType;

            var primaryKeyMembers = new List<string>();
            foreach (var keyMember in declaringType.KeyMembers)
                primaryKeyMembers.Add(keyMember.ToString());

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
                    modifiedColumnMappingCandidates = modifiedColumnMappingCandidates.Where(c => updatedColumnNames.Contains(c.EntityProperty.Name)).ToArray();
                }
                var modifiedColumnMappings = modifiedColumnMappingCandidates.ToArray();

                //
                // Create and populate a temp table to hold the updated values.
                //
                var conn = GetSqlConnection(ctx);
                var tempTableName = FillTempTable(conn, entities, tableName, columnMappings, selectedKeyMappings, modifiedColumnMappings, transaction);

                //
                // Update the target table using the temp table we just created.
                //
                var setStatements = modifiedColumnMappings.Select(c => $"t0.{c.TableColumn.Name} = t1.{c.TableColumn.Name}");
                var setStatementsSql = string.Join(" , ", setStatements);
                var conditionStatements = selectedKeyMappings.Select(c => $"t0.{c.TableColumn.Name} = t1.{c.TableColumn.Name}");
                var conditionStatementsSql = string.Join(" AND ", conditionStatements);
                var cmdBody = $@"UPDATE t0 SET {setStatementsSql}
                                 FROM {tableName.Fullname} AS t0
                                 INNER JOIN #{tableName.Name} AS t1 ON {conditionStatementsSql}
                                ";
                var cmd = new SqlCommand(cmdBody, conn, transaction);
                rowsAffected += cmd.ExecuteNonQuery();

                if (request.InsertIfNew)
                {
                    var columns = columnMappings.Values
                        .Where(m => !primaryKeyMembers.Contains(m.TableColumn.Name))
                        .Select(m => m.TableColumn.Name)
                        .ToArray();
                    var columnNames = string.Join(",", columns.Select(c => c));
                    var t0ColumnNames = string.Join(",", columns.Select(c => $"[t0].{c}"));
                    cmdBody = $@"INSERT INTO {tableName.Fullname}
                             SELECT {columnNames}
                             FROM #{tableName.Name}
                             EXCEPT
                             SELECT {t0ColumnNames}
                             FROM #{tableName.Name} AS t0
                             INNER JOIN {tableName.Fullname} AS t1 ON {conditionStatementsSql}            
                            ";
                    cmd = new SqlCommand(cmdBody, conn, transaction);
                    rowsAffected += cmd.ExecuteNonQuery();
                }

                //
                // Clean up. Delete the temp table.
                //
                var cmdFooter = $@"DROP TABLE {tempTableName}";
                cmd = new SqlCommand(cmdFooter, conn, transaction);
                cmd.ExecuteNonQuery();
            }

            response.AffectedRows.Add(new Tuple<Type, long>(t, rowsAffected));
        }

        private static void DoBulkInsertAll(
            this DbContext ctx, 
            IList entities, 
            SqlTransaction transaction, 
            bool recursive, 
            Dictionary<object, object> savedEntities,
            BulkOperationResponse response)
        {
            if (entities.Count == 0) return;

            Type t = entities[0].GetType();
            var mappings = GetMappings(ctx, t);

            if (recursive)
            {
                foreach (var fkMapping in mappings.ToForeignKeyMappings)
                {
                    var navProperties = new HashSet<object>();
                    var modifiedEntities = new List<object[]>();
                    foreach (var entity in entities)
                    {
                        var navProperty = GetProperty(fkMapping.NavigationPropertyName, entity);
                        if (navProperty != null)
                        {
                            foreach (var foreignKeyRelation in fkMapping.ForeignKeyRelations)
                            {
                                var navPropertyKey = GetProperty(foreignKeyRelation.ToProperty, entity);

                                if (navPropertyKey == 0)
                                {
                                    var currentValue = GetProperty(foreignKeyRelation.FromProperty, navProperty);
                                    if (currentValue > 0)
                                    {
                                        SetProperty(foreignKeyRelation.ToProperty, entity, currentValue);
                                    }
                                    else
                                    {
                                        if (navProperty != entity)
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

                    DoBulkInsertAll(ctx, navProperties.ToArray(), transaction, true, savedEntities, response);
                    foreach (var modifiedEntity in modifiedEntities)
                    {
                        var e = modifiedEntity[0];
                        var p = modifiedEntity[1];
                        foreach (var foreignKeyRelation in fkMapping.ForeignKeyRelations)
                        {
                            SetProperty(foreignKeyRelation.ToProperty, e, GetProperty(foreignKeyRelation.FromProperty, p));
                        }
                    }
                }
            }

            var validEntities = new ArrayList();
            var ignoredEntities = new ArrayList();
            foreach (dynamic entity in entities)
            {
                if (savedEntities.ContainsKey(entity))
                {
                    ignoredEntities.Add(entity);
                    continue;
                }
                validEntities.Add(entity);
                savedEntities.Add(entity, entity);
            }
            DoBulkInsertAll(ctx, validEntities, t, mappings, transaction, response);

            if (recursive)
            {
                foreach (var fkMapping in mappings.FromForeignKeyMappings)
                {
                    var navigationPropertyName = fkMapping.NavigationPropertyName;

                    var navPropertyEntities = new List<dynamic>();
                    var navPropertySelfReferences = new List<SelfReference>();
                    foreach (var entity in entities)
                    {
                        if (fkMapping.BuiltInTypeKind == BuiltInTypeKind.CollectionType ||
                            fkMapping.BuiltInTypeKind == BuiltInTypeKind.CollectionKind)
                        {
                            var navProperties = GetProperty(navigationPropertyName, entity);

                            if (fkMapping.ForeignKeyRelations != null)
                            {
                                foreach (var navProperty in navProperties)
                                {
                                    foreach (var foreignKeyRelation in fkMapping.ForeignKeyRelations)
                                    {
                                        SetProperty(foreignKeyRelation.ToProperty, navProperty,
                                            GetProperty(foreignKeyRelation.FromProperty, entity));
                                    }

                                    navPropertyEntities.Add(navProperty);
                                }
                            }
                            else if (fkMapping.AssociationMapping != null)
                            {
                                foreach (var navProperty in navProperties)
                                {
                                    dynamic np = new ExpandoObject();
                                    AddProperty(np, fkMapping.AssociationMapping.Source.TableColumn.Name, GetProperty(fkMapping.AssociationMapping.Source.EntityProperty.Name, entity));
                                    AddProperty(np, fkMapping.AssociationMapping.Target.TableColumn.Name, GetProperty(fkMapping.AssociationMapping.Target.EntityProperty.Name, navProperty));
                                    navPropertyEntities.Add(np);
                                }
                            }
                        }
                        else
                        {
                            var navProperty = GetProperty(navigationPropertyName, entity);
                            if (navProperty != null)
                            {
                                foreach (var foreignKeyRelation in fkMapping.ForeignKeyRelations)
                                {
                                    SetProperty(foreignKeyRelation.ToProperty, navProperty, GetProperty(foreignKeyRelation.FromProperty, entity));
                                }

                                if (navProperty != entity)
                                    navPropertyEntities.Add(navProperty);
                                else
                                    navPropertySelfReferences.Add(new SelfReference
                                    {
                                        Entity = entity,
                                        ForeignKeyProperties = fkMapping.ForeignKeyRelations.Select(p => p.ToProperty).ToArray()
                                    });
                            }
                        }
                    }

                    if (navPropertySelfReferences.Any())
                    {
                        var request = new BulkUpdateRequest
                        {
                            Entities = navPropertySelfReferences.Select(e => e.Entity).Distinct().ToArray(),
                            UpdatedColumnNames = navPropertySelfReferences.SelectMany(e => e.ForeignKeyProperties).Distinct().ToArray(),
                            Transaction = transaction
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
                            DoBulkInsertAll(ctx, navPropertyEntities.ToArray(), typeof(ExpandoObject), expandoMappings, transaction, response);
                        }
                        else
                            DoBulkInsertAll(ctx, navPropertyEntities.ToArray(), transaction, true, savedEntities, response);
                    }
                }
            }
        }

        private static void DoBulkInsertAll(
            this DbContext ctx, 
            IList entities, 
            Type t, 
            Mappings mappings, 
            SqlTransaction transaction,
            BulkOperationResponse response)
        {
            // If we for some reason are called with an empty list we return immediately.
            if (entities.Count == 0) return;

            var rowsAffected = 0;
            bool hasComplexProperties = mappings.ComplexPropertyNames.Any();
            var tableName = mappings.TableName;
            var columnMappings = mappings.ColumnMappingByPropertyName;

            var conn = GetSqlConnection(ctx);

            var bulkCopy = new SqlBulkCopy(conn, SqlBulkCopyOptions.Default, transaction) { DestinationTableName = tableName.Fullname };
            var table = new DataTable();

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
            foreach (var property in properties)
            {
                Type propertyType = property.Type;

                // Nullable properties need special treatment.
                if (propertyType.IsGenericType &&
                    propertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    propertyType = Nullable.GetUnderlyingType(propertyType);
                }

                // Since we cannot trust the CLR type properties to be in the same order as
                // the table columns we use the SqlBulkCopy column mappings.
                table.Columns.Add(new DataColumn(property.Name, propertyType));
                var clrPropertyName = property.Name;
                var tableColumnName = columnMappings[property.Name].TableColumn.Name;
                bulkCopy.ColumnMappings.Add(new SqlBulkCopyColumnMapping(clrPropertyName, tableColumnName));
            }

            // Check to see if the table has a primary key.
            dynamic declaringType = columnMappings
                .Values
                .First().TableColumn.DeclaringType;
            var keyMembers = declaringType.KeyMembers;
            var pkColumnMappings = columnMappings.Values
                .Where(m => keyMembers.Contains(m.TableColumn.Name))
                .ToArray();
            //var pkColumns = pkColumnMappings.Select(m => m.TableColumn).ToArray();

            // We have no primary key/s. Just add it all.
            if (pkColumnMappings.Length == 0)
            {
                if (entities[0] is ExpandoObject)
                {
                    foreach (var entity in entities)
                    {
                        var e = (ExpandoObject)entity;
                        table.Rows.Add(properties.Select(p => GetProperty(p.Name, e, DBNull.Value))
                            .ToArray());
                    }
                }
                else
                {
                    var props = properties.Select(p => t.GetProperty(p.Name)).ToArray();
                    foreach (var entity in entities)
                    {
                        var e = entity;
                        table.Rows.Add(props.Select(p => GetProperty(p.Name, e, DBNull.Value))
                            .ToArray());
                    }
                }

                bulkCopy.BulkCopyTimeout = 5 * 60;
                bulkCopy.WriteToServer(table);
                rowsAffected += table.Rows.Count;
            }
            // We have a non composite primary key that is either computed or an identity key
            else if (pkColumnMappings.Length == 1 &&
                     (pkColumnMappings[0].TableColumn.IsStoreGeneratedIdentity || pkColumnMappings[0].TableColumn.IsStoreGeneratedComputed))
            {
                var pkColumn = pkColumnMappings[0].TableColumn;
                var pkProperty = pkColumnMappings[0].EntityProperty;

                var newEntities = new ArrayList();

                if (entities[0] is ExpandoObject)
                {
                    foreach (var entity in entities)
                    {
                        var e = (ExpandoObject)entity;
                        var pk = GetProperty(pkProperty.Name, e);
                        if (pk == 0)
                            newEntities.Add(entity);
                    }
                }
                else
                {
                    foreach (var entity in entities)
                    {
                        var pk = GetProperty(pkProperty.Name, entity);
                        if (pk == 0)
                            newEntities.Add(entity);
                    }
                }
                   

                if (newEntities.Count > 0)
                {
                    var pkColumnType = Type.GetType(pkColumn.PrimitiveType.ClrEquivalentType.FullName);
                    var cmd = conn.CreateCommand();
                    cmd.CommandTimeout = (int)TimeSpan.FromMinutes(30).TotalSeconds;
                    cmd.Transaction = transaction;

                    // Get the number of existing rows in the table.
                    cmd.CommandText = $@"SELECT COUNT(*) FROM {tableName.Fullname}";
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

                    // Add all our new entities to our data table
                    if (newEntities[0] is ExpandoObject)
                    {
                        foreach (var entity in newEntities)
                        {
                            var e = (ExpandoObject)entity;
                            table.Rows.Add(properties.Select(p => GetProperty(p.Name, e))
                                .ToArray());
                        }
                    }
                    else
                    {
                        var props = properties.Select(p => t.GetProperty(p.Name)).ToArray();
                        foreach (var entity in newEntities)
                        {
                            var e = entity;
                            table.Rows.Add(props.Select(p => GetProperty(p.Name, e, DBNull.Value))
                                .ToArray());
                        }
                    }


                    bulkCopy.BulkCopyTimeout = 5 * 60;
                    bulkCopy.WriteToServer(table);
                    rowsAffected += table.Rows.Count;

                    cmd.CommandText = $"SELECT SCOPE_IDENTITY()";
                    result = cmd.ExecuteScalar();
                    dynamic lastId = Convert.ChangeType(result, pkColumnType);

                    cmd.CommandText =
                        $"SELECT [{pkColumn.Name}] From {tableName.Fullname} WHERE [{pkColumn.Name}] >= {nextId} and [{pkColumn.Name}] <= {lastId}";
                    var reader = cmd.ExecuteReader();
                    var ids = (from IDataRecord r in reader
                               let pk = r[pkColumn.Name]
                               select pk)
                        .OrderBy(i => i)
                        .ToArray();
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
                }

            }
            // We have a composite primary key.
            else
            {
                var nonPrimaryKeyColumnMappings = columnMappings
                    .Values
                    .Except(pkColumnMappings)
                    .ToArray();
                var tempTableName = FillTempTable(conn, entities, tableName, columnMappings, pkColumnMappings, nonPrimaryKeyColumnMappings, transaction);


                var conditionStatements =
                    pkColumnMappings.Select(c => $"t0.{c.TableColumn.Name} = t1.{c.TableColumn.Name}");
                var conditionStatementsSql = string.Join(" AND ", conditionStatements);

                string cmdBody;
                SqlCommand cmd;

                //
                // Update existing entities in the target table using the temp 
                // table we just created.
                //
                if (nonPrimaryKeyColumnMappings.Any())
                {
                    var setStatements = nonPrimaryKeyColumnMappings.Select(c =>
                        $"t0.{c.TableColumn.Name} = t1.{c.TableColumn.Name}");
                    var setStatementsSql = string.Join(" , ", setStatements);
                    cmdBody = $@"UPDATE t0 SET {setStatementsSql}
                                 FROM {tableName.Fullname} AS t0
                                 INNER JOIN #{tableName.Name} AS t1 ON {conditionStatementsSql}
                                ";
                    cmd = new SqlCommand(cmdBody, conn, transaction);
                    cmd.ExecuteNonQuery();
                }

                //
                //  Insert any new entities.
                //
                string listOfPrimaryKeyColumns = string.Join(",",
                    pkColumnMappings.Select(c => c.TableColumn));
                string listOfColumns = string.Join(",",
                    pkColumnMappings.Concat(nonPrimaryKeyColumnMappings).Select(c => c.TableColumn));
                cmdBody = $@"INSERT INTO {tableName.Fullname} ({listOfColumns})
                             SELECT {listOfColumns} 
                             FROM #{tableName.Name} AS t0
                             WHERE NOT EXISTS (
                                SELECT {listOfPrimaryKeyColumns}
                                FROM {tableName.Fullname} AS t1
                                WHERE {conditionStatementsSql}
                             )
                                ";
                cmd = new SqlCommand(cmdBody, conn, transaction);
                rowsAffected += cmd.ExecuteNonQuery();

                //
                // Clean up. Delete the temp table.
                //
                var cmdFooter = $@"DROP TABLE {tempTableName}";
                cmd = new SqlCommand(cmdFooter, conn, transaction);
                cmd.ExecuteNonQuery();
            }

            response.AffectedRows.Add(new Tuple<Type, long>(t, rowsAffected));
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


        private static string CreateGlobalId(DbContext ctx)
        {
            var ds = ctx.Database.Connection.DataSource.Replace(@"\", "_");
            var dbname = ctx.Database.Connection.Database.Replace(@"\", "_");
            var globalId = $@"Global\{ds}_{dbname}";

            return globalId;
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
            var tempTableName = $@"#{tableName.Name}";

            var columns = keyColumnMappings.Select(m => m.TableColumn.Name)
                .Concat(nonKeyColumnMappings.Select(m => m.TableColumn.Name)).ToArray();
            var columnNames = string.Join(",", columns.Select(c => c));
            var cmdHeader = $@"   
                                    IF OBJECT_ID('tempdb..#{tableName.Name}') IS NOT NULL DROP TABLE #{tableName.Name}

                                    SELECT {columnNames}
                                    INTO #{tableName.Name}
                                    FROM {tableName.Fullname}
                                    WHERE 1=0
                                ";
            if (keyColumnMappings.Length == 1 &&
                (keyColumnMappings[0].TableColumn.IsStoreGeneratedIdentity ||
                 keyColumnMappings[0].TableColumn.IsStoreGeneratedComputed))
            {
                cmdHeader += $@"SET IDENTITY_INSERT #{tableName.Name} ON";
            }

            var cmd = new SqlCommand(cmdHeader, conn, sqlTransaction);
            cmd.ExecuteNonQuery();

            //
            // Setup a bulk copy instance to populate the temp table.
            //
            var bulkCopy =
                new SqlBulkCopy(conn, SqlBulkCopyOptions.KeepIdentity, sqlTransaction)
                {
                    DestinationTableName = tempTableName,
                    BulkCopyTimeout = 5 * 60,

                };

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

            //
            // Configure a data table to use for the bulk copy 
            // operation into the temp table.
            //
            var table = new DataTable();
            foreach (var property in properties)
            {
                Type propertyType = property.Type;

                // Nullable properties need special treatment.
                if (propertyType.IsGenericType &&
                    propertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    propertyType = Nullable.GetUnderlyingType(propertyType);
                }

                // Ignore all properties that we have no mappings for.
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

            //
            // Fill the data table with our entities.
            //
            if (entities[0] is ExpandoObject)
            {
                foreach (var entity in entities)
                {
                    var e = (ExpandoObject)entity;
                    table.Rows.Add(properties.Select(p => GetProperty(p.Name, e)).ToArray());
                }
            }
            else
            {
                foreach (var entity in entities)
                {
                    var e = entity;
                    table.Rows.Add(properties.Select(p => GetProperty(p.Name, e, DBNull.Value)).ToArray());
                }
            }


            //
            // Fill the temp table.
            //
            bulkCopy.WriteToServer(table);

            return tempTableName;
        }

        private static PropInfo[] GetProperties(object o)
        {
            if (o is ExpandoObject)
            {
                var props = new List<PropInfo>();
                var dict = (IDictionary<string, object>)o;

                // Since we cannot get the type for expando properties
                // with null values we skip them. Doing so is safe since
                // we are not really concerned with storing null values
                // in table columns. They tend to store themselves just,
                // fine. If we have a null value for a non-null column
                // we have a problem but then the problem is that we have
                // a null value in our expando object, not that we skip
                // it here.
                foreach (var kvp in dict.Where(kvp=>kvp.Value != null))
                {
                    props.Add(new PropInfo
                    {
                        Name = kvp.Key,
                        Type = kvp.Value.GetType()
                    });
                }

                return props.ToArray();
            }

            Type t = o.GetType();
            return t.GetProperties().Select(p => new PropInfo
            {
                Type = p.PropertyType,
                Name = p.Name,
            }).ToArray();
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

        private static SqlConnection GetSqlConnection(this DbContext ctx)
        {
            var conn = (SqlConnection)ctx.Database.Connection;
            if (conn.State == ConnectionState.Closed)
                conn.Open();

            return conn;
        }

        private static TableName GetTableName(this DbContext ctx, Type t)
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

        private static IEnumerable<TableColumnMapping> GetTableColumnMappings(ICollection<PropertyMapping> properties, bool isIncludedFromComplexType)
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
            var entityName = t.Name;

            var storageMapping = (EntityContainerMapping)workspace.GetItem<GlobalItem>(containerName, DataSpace.CSSpace);
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

            //
            // Add mappings for all navigation properties.
            //
            //
            var foreignKeyMappings = new List<ForeignKeyMapping>();
            var navigationProperties =
                typeMapping.EntityType.DeclaredMembers.Where(m => m.BuiltInTypeKind == BuiltInTypeKind.NavigationProperty)
                    .Cast<NavigationProperty>()
                    .Where(p => p.RelationshipType is AssociationType)
                    .ToArray();

            foreach (var navigationProperty in navigationProperties)
            {
                var relType = (AssociationType)navigationProperty.RelationshipType;

                // Only bother with unknown relationships
                if (foreignKeyMappings.All(m => m.Name != relType.Name))
                {
                    var fkMapping = new ForeignKeyMapping
                    {
                        NavigationPropertyName = navigationProperty.Name,
                        BuiltInTypeKind = navigationProperty.TypeUsage.EdmType.BuiltInTypeKind,
                        Name = relType.Name,
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

                        fkMapping.FromType = (map.SourceEndMapping.AssociationEnd.TypeUsage.EdmType as RefType)?.ElementType.Name;
                        fkMapping.ToType = (map.TargetEndMapping.AssociationEnd.TypeUsage.EdmType as RefType)?.ElementType.Name;

                        fkMapping.AssociationMapping = new AssociationMapping
                        {
                            TableName = new TableName
                            {
                                Name = map.StoreEntitySet.Table,
                                Schema = map.StoreEntitySet.Schema,
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

            return new Mappings
            {
                TableName = tableName,
                ComplexPropertyNames = complexPropertyMappings.Select(m=>m.Property.Name).ToArray(),
                ColumnMappingByPropertyName = columnMappingByPropertyName,
                ToForeignKeyMappings = foreignKeyMappings.Where(m => m.ToType == entityName).ToArray(),
                FromForeignKeyMappings = foreignKeyMappings.Where(m => m.FromType == entityName).ToArray()
            };
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

        /// <summary>
        /// Use reflection to set a property value by its property 
        /// name to an object instance.
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="instance"></param>
        /// <param name="value"></param>
        private static void SetProperty(string propertyName, object instance, object value)
        {
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

        #endregion
    }
}

