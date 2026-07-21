/*
 * Copyright ©  2017-2026 Tånneryd IT AB
 * Licensed under the Apache License, Version 2.0.
 */

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Tanneryd.BulkOperations.Common.Sql;
using Tanneryd.BulkOperations.EFCore.Model;
using IColumnMapping = Microsoft.EntityFrameworkCore.Metadata.IColumnMapping;

namespace Tanneryd.BulkOperations.EFCore
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
            DoBulkInsertAllAsync(
                ctx,
                entities,
                sqlTransaction,
                enableRecursiveInsert,
                allowNotNullSelfReferences,
                commandTimeout,
                savedEntities,
                mappingsByType,
                response).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        private static async Task DoBulkInsertAllAsync(
            this DbContext ctx,
            IList<dynamic> entities,
            SqlTransaction sqlTransaction,
            EnableRecursiveInsert enableRecursiveInsert,
            AllowNotNullSelfReferences allowNotNullSelfReferences,
            TimeSpan commandTimeout,
            Dictionary<object, object> savedEntities,
            Dictionary<Type, Mappings> mappingsByType,
            BulkInsertResponse response,
            CancellationToken cancellationToken = default)
        {
            if (entities.Count == 0) return;

            Type t = entities[0].GetType();
            if (!mappingsByType.ContainsKey(t))
            {
                mappingsByType.Add(t, GetMappingExtractor(ctx).GetMappings(t));
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
                    if (fkMapping.IsCollection) continue;

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

                    await DoBulkInsertAllAsync(
                        ctx,
                        navProperties.ToList(),
                        sqlTransaction,
                        enableRecursiveInsert,
                        allowNotNullSelfReferences,
                        commandTimeout,
                        savedEntities,
                        mappingsByType,
                        response,
                        cancellationToken).ConfigureAwait(false);
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

            await DoBulkCopyAsync(
                ctx,
                validEntities,
                t,
                mappings,
                sqlTransaction,
                allowNotNullSelfReferences,
                enableRecursiveInsert,
                commandTimeout,
                response,
                cancellationToken).ConfigureAwait(false);

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
                    var isCollection = fkMapping.IsCollection;

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
                        var notExistingNavProperties = await BulkSelectNotExistingByTypeAsync(
                            ctx,
                            navPropertyType,
                            joinTableNavProperties,
                            pkColumnMappings,
                            sqlTransaction,
                            cancellationToken).ConfigureAwait(false);
                        await DoBulkInsertAllAsync(ctx,
                            notExistingNavProperties.ToArray(navPropertyType),
                            sqlTransaction,
                            enableRecursiveInsert,
                            allowNotNullSelfReferences,
                            commandTimeout,
                            savedEntities,
                            mappingsByType,
                            response,
                            cancellationToken).ConfigureAwait(false);

                        foreach (var joinTableNavPropertiesForEntity in joinTableNavPropertiesByEntity)
                        {
                            var entity = joinTableNavPropertiesForEntity.Key;
                            if (fkMapping.AssociationMapping.Source.EntityProperty.DeclaringType.Name ==
                                entity.GetType().Name)
                            {
                                foreach (var navProperty in joinTableNavPropertiesForEntity.Value)
                                {
                                    dynamic np = new ExpandoObject();
                                    AddProperty(np, fkMapping.AssociationMapping.Source.TableColumn.Column.Name,
                                        GetProperty(t, fkMapping.AssociationMapping.Source.EntityProperty.Name,
                                            entity));
                                    AddProperty(np, fkMapping.AssociationMapping.Target.TableColumn.Column.Name,
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
                                    AddProperty(np, fkMapping.AssociationMapping.Source.TableColumn.Column.Name,
                                        GetProperty(navPropertyType,
                                            fkMapping.AssociationMapping.Source.EntityProperty.Name, navProperty));
                                    AddProperty(np, fkMapping.AssociationMapping.Target.TableColumn.Column.Name,
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
                        await DoBulkUpdateAllAsync(
                            ctx,
                            request,
                            response,
                            cancellationToken).ConfigureAwait(false);
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
                                fkMapping.AssociationMapping.Source.TableColumn.Column.Name,
                                new TableColumnMapping
                                {
                                    EntityProperty = fkMapping.AssociationMapping.Source.EntityProperty,
                                    TableColumn = fkMapping.AssociationMapping.Source.TableColumn
                                });
                            expandoMappings.ColumnMappingByPropertyName.Add(
                                fkMapping.AssociationMapping.Target.TableColumn.Column.Name,
                                new TableColumnMapping
                                {
                                    EntityProperty = fkMapping.AssociationMapping.Target.EntityProperty,
                                    TableColumn = fkMapping.AssociationMapping.Target.TableColumn
                                });
                            await DoBulkCopyAsync(
                                ctx,
                                navPropertyEntities.ToArray(),
                                typeof(ExpandoObject),
                                expandoMappings,
                                sqlTransaction,
                                allowNotNullSelfReferences,
                                enableRecursiveInsert,
                                commandTimeout,
                                response,
                                cancellationToken).ConfigureAwait(false);
                        }
                        else
                            await DoBulkInsertAllAsync(
                                ctx,
                                navPropertyEntities.ToArray(),
                                sqlTransaction,
                                enableRecursiveInsert,
                                allowNotNullSelfReferences,
                                commandTimeout,
                                savedEntities,
                                mappingsByType,
                                response,
                                cancellationToken).ConfigureAwait(false);
                    }
                }
            }
        }

        private static string[] GetPrimaryKeyMembers(Dictionary<string, TableColumnMapping> columnMappings)
        {
            return columnMappings.Values.Where(v=>v.IsPrimaryKey).Select(v=>v.TableColumn.Column.Name).ToArray();
        }

        private static TableColumnMapping[] GetPrimaryKeyColumnMappings(DbContext ctx, Type t,
            Dictionary<Type, Mappings> mappingsByType)
        {
            if (!mappingsByType.ContainsKey(t))
            {
                mappingsByType.Add(t, GetMappingExtractor(ctx).GetMappings(t));
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
                .Where(m => primaryKeyMembers.Contains(m.TableColumn.Column.Name))
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
            DoBulkCopyAsync(
                ctx,
                entities,
                t,
                mappings,
                transaction,
                allowNotNullSelfReferences,
                enableRecursiveInsert,
                commandTimeout,
                response).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        private static async Task DoBulkCopyAsync(
            this DbContext ctx,
            IList entities,
            Type t,
            Mappings mappings,
            SqlTransaction transaction,
            AllowNotNullSelfReferences allowNotNullSelfReferences,
            EnableRecursiveInsert enableRecursiveInsert,
            TimeSpan commandTimeout,
            BulkInsertResponse response,
            CancellationToken cancellationToken = default)
        {
            // If we for some reason are called with an empty list we return immediately.
            if (entities.Count == 0) return;

            var rowsAffected = 0;
            bool hasComplexProperties = mappings.ComplexPropertyNames.Any();
            var discriminatorExtraColumns = GetDiscriminatorExtraColumns(mappings.Discriminator);
            var tableName = mappings.TableName;
            var columnMappings = mappings.ColumnMappingByPropertyName;

            var conn = await GetSqlConnectionAsync(ctx, cancellationToken).ConfigureAwait(false);

            // Complex types are flattened into ExpandoObject so the same
            // bulk-copy path can be reused for nested CLR shapes.
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

            // Insert path selection:
            //
            // (1) No primary key — unsupported (EF Core requires one); throw.
            //
            // (2) Join tables — entity type is ExpandoObject; stage via temp
            //     table then INSERT the missing rows.
            //
            // (3a) Single store-generated PK and we need those values back —
            //     bulk-copy into a temp table, then MERGE … OUTPUT to insert
            //     and map generated keys back via temp rowno.
            // (3b) Same PK shape but EnableRecursiveInsert.NoAndIgnoreGeneratedPrimaryKeys —
            //     direct SqlBulkCopy into the target table (no key retrieval).
            //
            // (4) All other key shapes — direct SqlBulkCopy; "new vs existing"
            //     is determined by a database lookup when needed.

            if (pkColumnMappings.Length == 0)
            {
                throw new ArgumentException(
                    "No primary key found. This should not be possible since EF Core has no support for tables without a primary key.");
            }

            // Join tables: stage keys in a temp table and insert only missing rows.
            if (t == typeof(ExpandoObject))
            {
                var nonPrimaryKeyColumnMappings = columnMappings
                    .Values
                    .Except(pkColumnMappings)
                    .ToArray();
                string tempTableName = null;
                try
                {
                    tempTableName = await FillTempTableAsync(
                        conn,
                        entities,
                        tableName,
                        columnMappings,
                        pkColumnMappings,
                        nonPrimaryKeyColumnMappings,
                        transaction,
                        cancellationToken).ConfigureAwait(false);

                    var conditionStatements =
                        pkColumnMappings.Select(c => $"[t0].[{c.TableColumn.Column.Name}] = [t1].[{c.TableColumn.Column.Name}]");
                    var conditionStatementsSql = string.Join(" AND ", conditionStatements);

                    string listOfPrimaryKeyColumns = string.Join(",",
                        pkColumnMappings.Select(c => $"[{c.TableColumn.Column.Name}]"));
                    string listOfColumns = string.Join(",",
                        pkColumnMappings.Concat(nonPrimaryKeyColumnMappings).Select(c => $"[{c.TableColumn.Column.Name}]"));

                    var cmdBody = $@"INSERT INTO {tableName.Fullname} ({listOfColumns})
                                     SELECT {listOfColumns} 
                                     FROM {tempTableName} AS [t0]
                                     WHERE NOT EXISTS (
                                        SELECT {listOfPrimaryKeyColumns}
                                        FROM {tableName.Fullname} AS [t1]
                                        WHERE {conditionStatementsSql}
                                     )
                                        ";
                    using var cmd = CreateSqlCommand(cmdBody, conn, transaction, commandTimeout);
                    rowsAffected += await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    if (tempTableName != null)
                        await DropTempTableAsync(conn, transaction, tempTableName, CancellationToken.None).ConfigureAwait(false);
                }
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
                        using var bulkCopy = CreateBulkCopy(
                            table,
                            properties,
                            columnMappings,
                            conn,
                            transaction,
                            tableName.Fullname,
                            discriminatorExtraColumns,
                            SqlBulkCopyOptions.Default,
                            IncludeRowNumber.No);

                        AddEntitiesToTable(table, newEntities, properties, t, mappings.Discriminator, IncludeRowNumber.No);
                        rowsAffected += newEntities.Count;

                        var s = new Stopwatch();
                        s.Start();
                        await bulkCopy.WriteToServerAsync(table.CreateDataReader(), cancellationToken).ConfigureAwait(false);
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
                        string tempTableName = null;
                        try
                        {
                            var allColumnNames = columnMappings.Values.Select(v => v.TableColumn.Column.Name).ToArray();
                            tempTableName =
                                await CreateTempTableAsync(
                                    conn,
                                    transaction,
                                    tableName,
                                    allColumnNames,
                                    discriminatorExtraColumns,
                                    IncludeRowNumber.Yes,
                                    cancellationToken).ConfigureAwait(false);

                            using var bulkCopy = CreateBulkCopy(
                                table,
                                properties,
                                columnMappings,
                                conn,
                                transaction,
                                tempTableName,
                                discriminatorExtraColumns,
                                SqlBulkCopyOptions.Default,
                                IncludeRowNumber.Yes);

                            AddEntitiesToTable(table, newEntities, properties, t, mappings.Discriminator, IncludeRowNumber.Yes);

                            var s = new Stopwatch();
                            s.Start();
                            await bulkCopy.WriteToServerAsync(table.CreateDataReader(), cancellationToken).ConfigureAwait(false);
                            s.Stop();
                            var stats = new BulkInsertStatistics
                            {
                                TimeElapsedDuringBulkCopy = s.Elapsed
                            };

                            var pkColumnType = Type.GetType(pkColumn.Property.ClrType.FullName);

                            var nonPrimaryKeyColumnMappings = columnMappings.Values
                                .Where(m => !primaryKeyMembers.Contains(m.TableColumn.Column.Name))
                                .ToArray();

                            rowsAffected += await SelectIntoUsingOutputClauseAsync(
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
                                t,
                                cancellationToken).ConfigureAwait(false);
                        }
                        finally
                        {
                            if (tempTableName != null)
                                await DropTempTableAsync(conn, transaction, tempTableName, CancellationToken.None).ConfigureAwait(false);
                        }
                    }
                }
            }
            else
            {
                using var bulkCopy = CreateBulkCopy(
                    table,
                    properties,
                    columnMappings,
                    conn,
                    transaction,
                    tableName.Fullname,
                    discriminatorExtraColumns,
                    SqlBulkCopyOptions.Default,
                    IncludeRowNumber.No);

                // Make sure that we only insert entities not already in the database.
                var notExistingEntities = await BulkSelectNotExistingByTypeAsync(
                    ctx, t, entities, pkColumnMappings, transaction, cancellationToken).ConfigureAwait(false);
                AddEntitiesToTable(table, notExistingEntities, properties, t, mappings.Discriminator, IncludeRowNumber.No);
                rowsAffected += notExistingEntities.Count;

                var s = new Stopwatch();
                s.Start();
                await bulkCopy.WriteToServerAsync(table.CreateDataReader(), cancellationToken).ConfigureAwait(false);
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
                   (pkColumnMappings[0].IsIdentity);
        }

        private static int SelectIntoForIntegerTypePrimaryKey(
            SqlConnection conn,
            SqlTransaction transaction,
            TableName tableName,
            Type pkColumnType,
            AllowNotNullSelfReferences allowNotNullSelfReferences,
            BulkInsertResponse response,
            TableColumnMapping[] nonPrimaryKeyColumnMappings,
            IProperty pkColumn,
            string tempTableName,
            Stopwatch s,
            BulkInsertStatistics stats,
            ArrayList newEntities,
            IProperty pkProperty,
            bool hasComplexProperties,
            Type t)
        {
            return SelectIntoForIntegerTypePrimaryKeyAsync(
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
                t).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        // Legacy identity-range approach (IDENT_CURRENT ± count). Unsafe under
        // concurrent inserts; superseded by SelectIntoUsingOutputClauseAsync.
        // Kept for reference; no current call sites.
        private static async Task<int> SelectIntoForIntegerTypePrimaryKeyAsync(
            SqlConnection conn,
            SqlTransaction transaction,
            TableName tableName,
            Type pkColumnType,
            AllowNotNullSelfReferences allowNotNullSelfReferences,
            BulkInsertResponse response,
            TableColumnMapping[] nonPrimaryKeyColumnMappings,
            IProperty pkColumn,
            string tempTableName,
            Stopwatch s,
            BulkInsertStatistics stats,
            ArrayList newEntities,
            IProperty pkProperty,
            bool hasComplexProperties,
            Type t,
            CancellationToken cancellationToken = default)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandTimeout = (int)TimeSpan.FromMinutes(30).TotalSeconds;
            cmd.Transaction = transaction;

            // Get the number of existing rows in the table.
            cmd.CommandText = $@"SELECT CASE WHEN EXISTS (SELECT TOP 1 * FROM {tableName.Fullname}) THEN 1 ELSE 0 END";
            var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            var count = Convert.ToInt64(result);

            // Get the identity increment value
            cmd.CommandText = $"SELECT IDENT_INCR('{tableName.Fullname}')";
            result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            dynamic identIncrement = Convert.ChangeType(result, pkColumnType);

            // Get the last identity value generated for our table
            cmd.CommandText = $"SELECT IDENT_CURRENT('{tableName.Fullname}')";
            result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            dynamic identcurrent = Convert.ChangeType(result, pkColumnType);

            var nextId = identcurrent + (count > 0 ? identIncrement : 0);

            string query;
            if (allowNotNullSelfReferences == AllowNotNullSelfReferences.Yes)
            {
                query = $"ALTER TABLE {tableName.Fullname} NOCHECK CONSTRAINT ALL";
                cmd.CommandText = query;
                await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                response.TablesWithNoCheckConstraints.Add(tableName.Fullname);
            }

            var columnNames = string.Join(",", nonPrimaryKeyColumnMappings.Select(p => $"[{p.TableColumn.Column.Name}]"));
            query = $@"                                  
                        INSERT INTO {tableName.Fullname} ({columnNames})                        
                        SELECT {columnNames}
                        FROM   {tempTableName}
                        ORDER BY rowno
                      ";
            cmd.CommandText = query;
            s.Restart();
            int rowsAffected = await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            s.Stop();
            stats.TimeElapsedDuringInsertInto = s.Elapsed;
            response.BulkInsertStatistics.Add(new Tuple<Type, BulkInsertStatistics>(t, stats));

            cmd.CommandText = $"SELECT SCOPE_IDENTITY()";
            result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            dynamic lastId = Convert.ChangeType(result, pkColumnType);

            cmd.CommandText =
                $"SELECT [{pkColumn.Name}] From {tableName.Fullname} WHERE [{pkColumn.Name}] >= {nextId} and [{pkColumn.Name}] <= {lastId}";

            object[] ids = null;
            using (var sqlDataReader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
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
            AllowNotNullSelfReferences allowNotNullSelfReferences,
            BulkInsertResponse response,
            TableColumnMapping[] nonPrimaryKeyColumnMappings,
            IColumnMapping pkColumn,
            string tempTableName,
            Stopwatch s,
            BulkInsertStatistics stats,
            ArrayList newEntities,
            IProperty pkProperty,
            bool hasComplexProperties,
            Discriminator discriminator,
            Type t)
        {
            return SelectIntoUsingOutputClauseAsync(
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
                discriminator,
                t).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Inserts from the temp table using MERGE ON 1=0 … OUTPUT so SQL Server
        /// returns inserted identity values ordered by our temp-table rowno.
        /// Required because SqlBulkCopy into a real table cannot reliably return
        /// per-row identities. When AllowNotNullSelfReferences is Yes, CHECK/FK
        /// constraints are temporarily disabled (re-enabled by the outer finally).
        /// </summary>
        private static async Task<int> SelectIntoUsingOutputClauseAsync(
            SqlConnection conn,
            SqlTransaction transaction,
            TableName tableName,
            Type pkColumnType,
            AllowNotNullSelfReferences allowNotNullSelfReferences,
            BulkInsertResponse response,
            TableColumnMapping[] nonPrimaryKeyColumnMappings,
            IColumnMapping pkColumn,
            string tempTableName,
            Stopwatch s,
            BulkInsertStatistics stats,
            ArrayList newEntities,
            IProperty pkProperty,
            bool hasComplexProperties,
            Discriminator discriminator,
            Type t,
            CancellationToken cancellationToken = default)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandTimeout = (int)TimeSpan.FromMinutes(30).TotalSeconds;
            cmd.Transaction = transaction;

            string query;
            if (allowNotNullSelfReferences == AllowNotNullSelfReferences.Yes)
            {
                // Allows inserting not-null self-FK graphs; re-enabled in BulkInsertAllAsync finally.
                query = $"ALTER TABLE {tableName.Fullname} NOCHECK CONSTRAINT ALL";
                cmd.CommandText = query;
                await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                response.TablesWithNoCheckConstraints.Add(tableName.Fullname);
            }

            if (nonPrimaryKeyColumnMappings.Any())
            {
                var columnNames = string.Join(",", nonPrimaryKeyColumnMappings.Select(p => $"[{p.TableColumn.Column.Name}]"));
                if (discriminator != null)
                    columnNames += $", [{discriminator.Column.Name}]";

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
                               inserted.[{pkColumn.Column.Name}]; 
                                 ";
            }
            else if (discriminator != null)
            {
                // Identity PK + discriminator only (TPH with no other columns).
                // Alias list must match SELECT; insert the discriminator value.
                var discriminatorName = $"[{discriminator.Column.Name}]";
                query = $@"  
                        MERGE {tableName.Fullname}
                        USING 
                            (SELECT {discriminatorName}, rowno
                             FROM   {tempTableName}) t ({discriminatorName}, rowno)
                        ON 1 = 0
                        WHEN NOT MATCHED THEN
                        INSERT ({discriminatorName})
                        VALUES ({discriminatorName})
                        OUTPUT t.rowno,
                               inserted.[{pkColumn.Column.Name}]; 
                                 ";
            }
            else
            {
                query = $@"  
                        MERGE {tableName.Fullname}
                        USING 
                            (SELECT rowno
                             FROM   {tempTableName}) t (rowno)
                        ON 1 = 0
                        WHEN NOT MATCHED THEN
                        INSERT DEFAULT VALUES
                        OUTPUT t.rowno,
                               inserted.[{pkColumn.Column.Name}]; 
                                 ";
            }

            cmd.CommandText = query;
            s.Restart();
            object[] ids = null;
            using (var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
            {
                ids = (
                    from IDataRecord r in reader
                    let pk = r[pkColumn.Column.Name]
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
        private static ArrayList SelectNewEntities(IList entities, IProperty pkProperty, Type t)
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
            return GetClusteredIndexColumnsAsync(ctx, schema, tableName, sqlTransaction, mappings)
                .ConfigureAwait(false).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Reads clustered-index column order so rows can be sorted before bulk
        /// load to reduce page splits. Schema/table identifiers come from EF mappings.
        /// </summary>
        private static async Task<string[]> GetClusteredIndexColumnsAsync(
            DbContext ctx,
            string schema,
            string tableName,
            SqlTransaction sqlTransaction,
            Mappings mappings,
            CancellationToken cancellationToken = default)
        {
            var connection = await GetSqlConnectionAsync(ctx, cancellationToken).ConfigureAwait(false);

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

            using var cmd = CreateSqlCommand(query, connection, sqlTransaction, TimeSpan.FromSeconds(30));

            string[] clusteredColumns = null;
            using (var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
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
            SqlConnection conn,
            IList entities,
            TableName tableName,
            Dictionary<string, TableColumnMapping> columnMappings,
            TableColumnMapping[] keyColumnMappings,
            TableColumnMapping[] nonKeyColumnMappings,
            SqlTransaction sqlTransaction)
        {
            return FillTempTableAsync(
                conn,
                entities,
                tableName,
                columnMappings,
                keyColumnMappings,
                nonKeyColumnMappings,
                sqlTransaction).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        private static async Task<string> FillTempTableAsync(
            SqlConnection conn,
            IList entities,
            TableName tableName,
            Dictionary<string, TableColumnMapping> columnMappings,
            TableColumnMapping[] keyColumnMappings,
            TableColumnMapping[] nonKeyColumnMappings,
            SqlTransaction sqlTransaction,
            CancellationToken cancellationToken = default,
            TableColumnMapping[] concurrencyTokenMappings = null)
        {
            concurrencyTokenMappings = concurrencyTokenMappings ?? Array.Empty<TableColumnMapping>();

            var columnNames = keyColumnMappings.Select(m => m.TableColumn.Column.Name)
                .Concat(nonKeyColumnMappings.Select(m => m.TableColumn.Column.Name))
                .Concat(concurrencyTokenMappings.Select(m => m.TableColumn.Column.Name))
                .ToArray();

            // timestamp/rowversion columns are not insertable; stage them as varbinary(8).
            var castToVarBinary8 = new HashSet<string>(
                concurrencyTokenMappings
                    .Where(RequiresVarBinary8TempColumn)
                    .Select(m => m.TableColumn.Column.Name),
                StringComparer.OrdinalIgnoreCase);

            var tempTableName = await CreateTempTableAsync(
                conn,
                sqlTransaction,
                tableName,
                columnNames,
                new TableColumn[0],
                IncludeRowNumber.Yes,
                cancellationToken,
                castToVarBinary8).ConfigureAwait(false);

            var identityInsertEnabled = false;
            try
            {
                // Temp table inherits identity metadata from the source; KeepIdentity
                // bulk-copy of explicit key values requires IDENTITY_INSERT ON.
                if (keyColumnMappings.Length == 1 &&
                    keyColumnMappings[0].IsIdentity)
                {
                    await EnableIdentityInsertAsync(tempTableName, conn, sqlTransaction, cancellationToken).ConfigureAwait(false);
                    identityInsertEnabled = true;
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
                var concurrencyColumnProperties = allProperties
                    .Where(p => concurrencyTokenMappings.Any(m => m.EntityProperty.Name == p.Name))
                    .ToArray();
                var properties = pkColumnProperties
                    .Concat(selectedColumnProperties)
                    .Concat(concurrencyColumnProperties)
                    .ToArray();

                var effectiveColumnMappings = columnMappings;
                if (concurrencyTokenMappings.Length > 0)
                {
                    effectiveColumnMappings = new Dictionary<string, TableColumnMapping>(columnMappings);
                    foreach (var token in concurrencyTokenMappings)
                    {
                        if (!effectiveColumnMappings.ContainsKey(token.EntityProperty.Name))
                            effectiveColumnMappings[token.EntityProperty.Name] = token;
                    }
                }

                var table = new DataTable();
                using var bulkCopy = CreateBulkCopy(
                    table,
                    properties,
                    effectiveColumnMappings,
                    conn,
                    sqlTransaction,
                    tempTableName,
                    new TableColumn[0],
                    SqlBulkCopyOptions.KeepIdentity,
                    IncludeRowNumber.Yes);

                var type = entities[0].GetType();
                AddEntitiesToTable(table, entities, properties, type, null, IncludeRowNumber.Yes);

                //
                // Fill the temp table.
                //
                await bulkCopy.WriteToServerAsync(table.CreateDataReader(), cancellationToken).ConfigureAwait(false);

                return tempTableName;
            }
            finally
            {
                if (identityInsertEnabled)
                {
                    await DisableIdentityInsertAsync(
                        tempTableName,
                        conn,
                        sqlTransaction,
                        CancellationToken.None).ConfigureAwait(false);
                }
            }
        }

        private static bool IsRowVersionStoreType(string storeType)
        {
            if (string.IsNullOrEmpty(storeType))
                return false;
            var typeName = storeType;
            var paren = typeName.IndexOf('(');
            if (paren >= 0)
                typeName = typeName.Substring(0, paren);
            return typeName.Equals("timestamp", StringComparison.OrdinalIgnoreCase) ||
                   typeName.Equals("rowversion", StringComparison.OrdinalIgnoreCase);
        }

        private static bool RequiresVarBinary8TempColumn(TableColumnMapping mapping)
        {
            if (IsRowVersionStoreType(mapping.TableColumn.Column.StoreType))
                return true;

            var clrType = mapping.EntityProperty.ClrType;
            if (clrType == typeof(byte[]) || Nullable.GetUnderlyingType(clrType) == typeof(byte[]))
                return mapping.EntityProperty.ValueGenerated != Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never;

            return false;
        }

        private static void EnableIdentityInsert(string tableName, SqlConnection conn, SqlTransaction sqlTransaction)
        {
            EnableIdentityInsertAsync(tableName, conn, sqlTransaction).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        private static Task EnableIdentityInsertAsync(
            string tableName,
            SqlConnection conn,
            SqlTransaction sqlTransaction,
            CancellationToken cancellationToken = default)
        {
            return TempTableSqlHelper.EnableIdentityInsertAsync(tableName, conn, sqlTransaction, cancellationToken);
        }

        private static void DisableIdentityInsert(string tableName, SqlConnection conn, SqlTransaction sqlTransaction)
        {
            DisableIdentityInsertAsync(tableName, conn, sqlTransaction).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        private static Task DisableIdentityInsertAsync(
            string tableName,
            SqlConnection conn,
            SqlTransaction sqlTransaction,
            CancellationToken cancellationToken = default)
        {
            return TempTableSqlHelper.DisableIdentityInsertAsync(tableName, conn, sqlTransaction, cancellationToken);
        }
    }
}
