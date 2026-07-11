/*
 * Copyright ©  2017-2025 Tånneryd IT AB
 * Licensed under the Apache License, Version 2.0.
 */

using Microsoft.Data.SqlClient;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Core.Metadata.Edm;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using Tanneryd.BulkOperations.Common.Sql;
using Tanneryd.BulkOperations.EF6.Model;

namespace Tanneryd.BulkOperations.EF6
{
    public static partial class DbContextExtensions
    {
        private static void DoBulkInsertAll(
            this DbContext ctx,
            IList<dynamic> entities,
            SqlTransaction sqlTransaction,
            EnableRecursiveInsert enableRecursiveInsert,
            AllowNotNullSelfReferences allowNotNullSelfReferences,
            TimeSpan commandTimeout,
            Dictionary<object, object> savedEntities,
            Dictionary<Type, Mappings> mappingsByType,
            BulkInsertResponse response)
        {
            if (entities.Count == 0) return;

            Type t = entities[0].GetType();
            if (!mappingsByType.ContainsKey(t))
            {
                mappingsByType.Add(t, MappingExtractor.GetMappings(ctx, t));
            }

            var mappings = mappingsByType[t];

            // 
            // Find any 'one-or-zero to many' related entities. If found, those
            // entities should be persisted and their primary key values copied
            // to the foreign keys in these entities.
            //
            if (enableRecursiveInsert == EnableRecursiveInsert.Yes)
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
                                var isDateTime = IsDateTime(navPropertyKeyType);
                                var navPropertyKey = GetProperty(t, foreignKeyRelation.ToProperty, entity);

                                // we do nothing unless the one-to-one
                                // nav property in previously unknown
                                if (navPropertyKey == null ||                                    
                                    (isGuid && navPropertyKey == default(Guid)) ||
                                    (isDateTime && navPropertyKey == default(DateTime)) ||
                                    navPropertyKey == 0)
                                {
                                    var currentValue = GetProperty(navPropertyType, foreignKeyRelation.FromProperty,
                                        navProperty);
                                    if ((isGuid && navPropertyKey != default(Guid)) ||
                                        (isDateTime && navPropertyKey != default(DateTime)) ||
                                        (!(isGuid || isDateTime) && currentValue > 0))
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
                        enableRecursiveInsert,
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

            DoBulkCopy(ctx, validEntities, t, mappings, sqlTransaction, allowNotNullSelfReferences, enableRecursiveInsert, commandTimeout,
                response);

            //
            // Any many-to-one (parent-child) foreign key related entities are found here. 
            // We collect all the children, save the parents, update the foreign key in
            // the children and then save the children. When we have self references we need
            // to handle that with care.
            //
            if (enableRecursiveInsert == EnableRecursiveInsert.Yes)
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
                            enableRecursiveInsert,
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
                            UpdatedPropertyNames = navPropertySelfReferences.SelectMany(e => e.ForeignKeyProperties)
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
                                enableRecursiveInsert,
                                commandTimeout,
                                response);
                        }
                        else
                            DoBulkInsertAll(
                                ctx,
                                navPropertyEntities.ToArray(),
                                sqlTransaction,
                                enableRecursiveInsert,
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
                mappingsByType.Add(t, MappingExtractor.GetMappings(ctx, t));
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
            AllowNotNullSelfReferences allowNotNullSelfReferences,
            EnableRecursiveInsert enableRecursiveInsert,
            TimeSpan commandTimeout,
            BulkInsertResponse response)
        {
            // If we for some reason are called with an empty list we return immediately.
            if (entities.Count == 0) return;

            var rowsAffected = 0;
            bool hasComplexProperties = mappings.ComplexPropertyNames.Any();
            bool usingTableInheritance = mappings.Discriminator != null;
            var tableName = mappings.TableName;
            var columnMappings = mappings.ColumnMappingByPropertyName;

            var conn = ResolveSqlConnection(ctx);

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
                .Where(p => columnMappings.ContainsKey(p.Name))
                .ToArray();

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
            // (3a) The table has a single column primary key that is
            //     generated or computed by the database. In these
            //     cases we need to perform some black magic in order
            //     to reliably retrieve these primary key values and
            //     update the corresponding entity objects. Separating
            //     new entities from previously existing entities is
            //     easy. We simply look at the primary key property
            //     and if it has no value the entity is a new one and
            //     it should be written to the database table.
            // (3b) The table has a single column primary key that is
            //     generated or computed by the database but we are not
            //     interested in recursively inserting entities and we
            //     do not care about retrieving generated primary key
            //     values. So, we can save some time by simply doing
            //     a direct bulk copy without the previously mentioned
            //     black magic.
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

                if (enableRecursiveInsert == EnableRecursiveInsert.NoAndIgnoreGeneratedPrimaryKeys)
                {
                    if (newEntities.Count > 0)
                    {
                        var bulkCopy = CreateBulkCopy(
                            table,
                            properties,
                            columnMappings,
                            conn,
                            transaction,
                            tableName.Fullname,
                            mappings.Discriminator,
                            SqlBulkCopyOptions.Default,
                            IncludeRowNumber.No);

                        AddEntitiesToTable(table, newEntities, properties, t, mappings.Discriminator, IncludeRowNumber.No);
                        rowsAffected += newEntities.Count;

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
                }
                else
                {
                    //
                    // Save new entities to the database table making sure that the entities
                    // are updated with their generated or computed primary key.
                    //
                    if (newEntities.Count > 0)
                    {
                        var allColumnNames = columnMappings.Values.Select(v => v.TableColumn.Name).ToArray();
                        var tempTableName =
                            CreateTempTable(
                                conn,
                                transaction,
                                tableName,
                                mappings.Discriminator,
                                allColumnNames,
                                IncludeRowNumber.Yes);

                        var bulkCopy = CreateBulkCopy(
                            table,
                            properties,
                            columnMappings,
                            conn,
                            transaction,
                            tempTableName,
                            mappings.Discriminator,
                            SqlBulkCopyOptions.Default,
                            IncludeRowNumber.Yes);

                        AddEntitiesToTable(table, newEntities, properties, t, mappings.Discriminator, IncludeRowNumber.Yes);

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
                            mappings.Discriminator,
                            t);
                    }
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
                    mappings.Discriminator,
                    SqlBulkCopyOptions.Default,
                    IncludeRowNumber.No);

                // Make sure that we only insert entities not already in the database.
                var notExistingEntities = BulkSelectNotExisting(ctx, t, entities, pkColumnMappings, transaction);
                AddEntitiesToTable(table, notExistingEntities, properties, t, mappings.Discriminator, IncludeRowNumber.No);
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
            SqlServerConnection conn,
            SqlTransaction transaction,
            TableName tableName,
            Type pkColumnType,
            AllowNotNullSelfReferences allowNotNullSelfReferences,
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
            if (allowNotNullSelfReferences == AllowNotNullSelfReferences.Yes)
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
            SqlServerConnection conn,
            SqlTransaction transaction,
            TableName tableName,
            Type pkColumnType,
            AllowNotNullSelfReferences allowNotNullSelfReferences,
            BulkInsertResponse response,
            TableColumnMapping[] nonPrimaryKeyColumnMappings,
            EdmProperty pkColumn,
            string tempTableName,
            Stopwatch s,
            BulkInsertStatistics stats,
            ArrayList newEntities,
            EdmProperty pkProperty,
            bool hasComplexProperties,
            Discriminator discriminator,
            Type t)
        {
            var cmd = conn.CreateCommand();
            cmd.CommandTimeout = (int)TimeSpan.FromMinutes(30).TotalSeconds;
            cmd.Transaction = transaction;

            string query;
            if (allowNotNullSelfReferences == AllowNotNullSelfReferences.Yes)
            {
                query = $"ALTER TABLE {tableName.Fullname} NOCHECK CONSTRAINT ALL";
                cmd.CommandText = query;
                cmd.ExecuteNonQuery();
                response.TablesWithNoCheckConstraints.Add(tableName.Fullname);
            }

            if (nonPrimaryKeyColumnMappings.Any())
            {
                var columnNames = string.Join(",", nonPrimaryKeyColumnMappings.Select(p => $"[{p.TableColumn.Name}]"));
                if (discriminator != null)
                {
                    columnNames = columnNames + $", [{discriminator.Column.Name}]";
                }
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
            }
            else
            {
                var columnNames = "rowno";
                if (discriminator != null)
                {
                    columnNames = $"[{discriminator.Column.Name}]," + columnNames;
                }

                query = $@"  
                        MERGE {tableName.Fullname}
                        USING 
                            (SELECT {columnNames}
                             FROM   {tempTableName}) t (rowno)
                        ON 1 = 0
                        WHEN NOT MATCHED THEN
                        INSERT DEFAULT VALUES
                        OUTPUT t.rowno,
                               inserted.[{pkColumn.Name}]; 
                                 ";
            }

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

        private static void AddEntitiesToTable(
            DataTable table,
            IList entities,
            BulkPropertyInfo[] properties,
            Type t,
            Discriminator discriminator,
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

                    if (discriminator != null) columnValues.Add(discriminator.Value);
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
            var dataProperties = properties.Where(p => !navigationPropertyNames.Contains(p.Name));
            foreach (var property in dataProperties)
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
                // recursively and that should ONLY happen if we are traversing a
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
            string schema,
            string tableName,
            SqlTransaction sqlTransaction,
            Mappings mappings)
        {
            var connection = ResolveSqlConnection(ctx);

            string query = $@"
                    SELECT  col.name
                    FROM sys.indexes ind
                    INNER JOIN sys.index_columns ic ON ind.object_id = ic.object_id and ind.index_id = ic.index_id
                    INNER JOIN sys.columns col ON ic.object_id = col.object_id and ic.column_id = col.column_id
                    INNER JOIN sys.tables t ON ind.object_id = t.object_id
                    WHERE t.name = '{tableName}' AND ind.type_desc = 'CLUSTERED'";

            if (!string.IsNullOrEmpty(schema))
            {
                query += $" AND SCHEMA_NAME(t.schema_id) = '{schema}'";
            }

            query += " ORDER BY ic.index_column_id;";

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

        /// <summary>
        /// This method does NOT support table inheritance right now.
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="entities"></param>
        /// <param name="tableName"></param>
        /// <param name="columnMappings"></param>
        /// <param name="keyColumnMappings"></param>
        /// <param name="nonKeyColumnMappings"></param>
        /// <param name="sqlTransaction"></param>
        /// <returns></returns>
        private static string FillTempTable(
            SqlServerConnection conn,
            IList entities,
            TableName tableName,
            Dictionary<string, TableColumnMapping> columnMappings,
            TableColumnMapping[] keyColumnMappings,
            TableColumnMapping[] nonKeyColumnMappings,
            SqlTransaction sqlTransaction)
        {
            var columnNames = keyColumnMappings.Select(m => m.TableColumn.Name)
                .Concat(nonKeyColumnMappings.Select(m => m.TableColumn.Name)).ToArray();

            var tempTableName = CreateTempTable(
                conn,
                sqlTransaction,
                tableName,
                null,
                columnNames,
                IncludeRowNumber.Yes);

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
                null,
                SqlBulkCopyOptions.KeepIdentity,
                IncludeRowNumber.Yes);

            var type = entities[0].GetType();
            AddEntitiesToTable(table, entities, properties, type, null, IncludeRowNumber.Yes);

            //
            // Fill the temp table.
            //
            bulkCopy.WriteToServer(table.CreateDataReader());

            return tempTableName;
        }

        private static void EnableIdentityInsert(string tableName, SqlServerConnection conn, SqlTransaction sqlTransaction)
        {
            conn.EnableIdentityInsert(tableName, sqlTransaction);
        }

        private static void DisableIdentityInsert(string tableName, SqlServerConnection conn, SqlTransaction sqlTransaction)
        {
            conn.DisableIdentityInsert(tableName, sqlTransaction);
        }
    }
}
