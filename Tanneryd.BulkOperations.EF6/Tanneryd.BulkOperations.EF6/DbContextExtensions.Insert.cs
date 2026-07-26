/*
 * Copyright ©  2017-2026 Tånneryd IT AB
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
using System.Threading;
using System.Threading.Tasks;
using Tanneryd.BulkOperations.Common.Sql;
using Tanneryd.BulkOperations.EF6.Model;

namespace Tanneryd.BulkOperations.EF6
{
    public static partial class DbContextExtensions
    {

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
            bool useTableLock = false,
            CancellationToken cancellationToken = default)
        {
            if (entities.Count == 0) return;

            // One TPH discriminator is stamped per batch from entities[0]. Split
            // mixed concrete CLR types so each group gets the correct value.
            var typeGroups = entities
                .GroupBy(e => (Type)e.GetType())
                .ToList();
            if (typeGroups.Count > 1)
            {
                foreach (var typeGroup in typeGroups)
                {
                    await DoBulkInsertAllAsync(
                        ctx,
                        typeGroup.ToList(),
                        sqlTransaction,
                        enableRecursiveInsert,
                        allowNotNullSelfReferences,
                        commandTimeout,
                        savedEntities,
                        mappingsByType,
                        response,
                        useTableLock,
                        cancellationToken).ConfigureAwait(false);
                }

                return;
            }

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
                                var navPropertyKey = GetProperty(t, foreignKeyRelation.ToProperty, entity);

                                // Only act when the FK on this entity is still unset.
                                if (IsUnsetKeyValue(navPropertyKey, navPropertyKeyType))
                                {
                                    var fromPropertyInfo = navPropertyType.GetProperty(foreignKeyRelation.FromProperty);
                                    var currentValue = GetProperty(navPropertyType, foreignKeyRelation.FromProperty,
                                        navProperty);

                                    // Only treat the navigation as already persisted when we inserted
                                    // it earlier in this recursive walk. A non-default numeric PK alone
                                    // is not enough — leftover client IDs must not skip insert.
                                    if (savedEntities.ContainsKey(navProperty) &&
                                        fromPropertyInfo != null &&
                                        IsKeyValueSet(currentValue, fromPropertyInfo.PropertyType))
                                    {
                                        SetProperty(foreignKeyRelation.ToProperty, entity, currentValue);
                                    }
                                    else
                                    {
                                        var same = navProperty.GetType() == entity.GetType() &&
                                                   navProperty == entity;
                                        if (!same)
                                        {
                                            ClearLeftoverStoreGeneratedNavKey(
                                                navProperty,
                                                navPropertyType,
                                                foreignKeyRelation.FromProperty,
                                                fromPropertyInfo,
                                                currentValue,
                                                mappingsByType,
                                                ctx);
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
                        useTableLock,
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
                useTableLock,
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
                        var notExistingNavProperties = await BulkSelectNotExistingByTypeAsync(
                            ctx,
                            navPropertyType,
                            joinTableNavProperties,
                            pkColumnMappings,
                            sqlTransaction,
                            commandTimeout,
                            useTableLock,
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
                            useTableLock,
                            cancellationToken).ConfigureAwait(false);

                        foreach (var joinTableNavPropertiesForEntity in joinTableNavPropertiesByEntity)
                        {
                            var entity = joinTableNavPropertiesForEntity.Key;
                            if (fkMapping.AssociationMapping.Sources[0].EntityProperty.DeclaringType.Name ==
                                MappingExtractor.ResolveMappedClrType(ctx, entity.GetType()).Name)
                            {
                                foreach (var navProperty in joinTableNavPropertiesForEntity.Value)
                                {
                                    dynamic np = new ExpandoObject();
                                    foreach (var source in fkMapping.AssociationMapping.Sources)
                                    {
                                        AddProperty(np, source.TableColumn.Name,
                                            GetProperty(t, source.EntityProperty.Name, entity));
                                    }
                                    foreach (var target in fkMapping.AssociationMapping.Targets)
                                    {
                                        AddProperty(np, target.TableColumn.Name,
                                            GetProperty(navPropertyType, target.EntityProperty.Name, navProperty));
                                    }
                                    navPropertyEntities.Add(np);
                                }
                            }
                            else
                            {
                                foreach (var navProperty in joinTableNavPropertiesForEntity.Value)
                                {
                                    dynamic np = new ExpandoObject();
                                    foreach (var source in fkMapping.AssociationMapping.Sources)
                                    {
                                        AddProperty(np, source.TableColumn.Name,
                                            GetProperty(navPropertyType, source.EntityProperty.Name, navProperty));
                                    }
                                    foreach (var target in fkMapping.AssociationMapping.Targets)
                                    {
                                        AddProperty(np, target.TableColumn.Name,
                                            GetProperty(t, target.EntityProperty.Name, entity));
                                    }
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
                            Transaction = sqlTransaction,
                            UseTableLock = useTableLock,
                            CommandTimeout = commandTimeout
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
                            foreach (var end in fkMapping.AssociationMapping.Sources
                                         .Concat(fkMapping.AssociationMapping.Targets))
                            {
                                expandoMappings.ColumnMappingByPropertyName.Add(
                                    end.TableColumn.Name,
                                    new TableColumnMapping
                                    {
                                        EntityProperty = end.TableColumn,
                                        TableColumn = end.TableColumn
                                    });
                            }
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
                                useTableLock,
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
                                useTableLock,
                                cancellationToken).ConfigureAwait(false);
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
            bool useTableLock = false,
            CancellationToken cancellationToken = default)
        {
            // If we for some reason are called with an empty list we return immediately.
            if (entities.Count == 0) return;

            var rowsAffected = 0;
            bool hasComplexProperties = mappings.ComplexPropertyNames.Any();
            bool usingTableInheritance = mappings.Discriminator != null;
            var tableName = mappings.TableName;
            var columnMappings = mappings.ColumnMappingByPropertyName;

            var conn = await ResolveSqlConnectionAsync(ctx, cancellationToken).ConfigureAwait(false);

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
            // Pass mappings so all-null Expando keys still get a CLR type.
            var properties = GetProperties(entities, columnMappings)
                .Where(p => columnMappings.ContainsKey(p.Name))
                .ToArray();

            var table = new DataTable();

            // Check to see if the table has a primary key.
            var primaryKeyMembers = GetPrimaryKeyMembers(columnMappings);
            var pkColumnMappings = GetPrimaryKeyColumnMappings(columnMappings, primaryKeyMembers);

            // Insert path selection:
            //
            // (1) No primary key — unsupported (EF6 requires one); throw.
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
                    "No primary key found. This should not be possible since EF6 has no support for tables without a primary key.");
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
                        commandTimeout,
                        useTableLock,
                        cancellationToken).ConfigureAwait(false);

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
                            mappings.Discriminator,
                            SqlBulkCopyOptions.Default,
                            IncludeRowNumber.No,
                            commandTimeout,
                            useTableLock);

                        rowsAffected += newEntities.Count;

                        var s = new Stopwatch();
                        s.Start();
                        using (var reader = CreateEntitiesDataReader(table, newEntities, properties, t, mappings.Discriminator, IncludeRowNumber.No))
                            await bulkCopy.WriteToServerAsync(reader, cancellationToken).ConfigureAwait(false);
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
                            var allColumnNames = columnMappings.Values.Select(v => v.TableColumn.Name).ToArray();
                            tempTableName =
                                await CreateTempTableAsync(
                                    conn,
                                    transaction,
                                    tableName,
                                    mappings.Discriminator,
                                    allColumnNames,
                                    IncludeRowNumber.Yes,
                                    cancellationToken).ConfigureAwait(false);

                            using var bulkCopy = CreateBulkCopy(
                                table,
                                properties,
                                columnMappings,
                                conn,
                                transaction,
                                tempTableName,
                                mappings.Discriminator,
                                SqlBulkCopyOptions.Default,
                                IncludeRowNumber.Yes,
                                commandTimeout,
                                useTableLock);

                            var s = new Stopwatch();
                            s.Start();
                            using (var reader = CreateEntitiesDataReader(table, newEntities, properties, t, mappings.Discriminator, IncludeRowNumber.Yes))
                                await bulkCopy.WriteToServerAsync(reader, cancellationToken).ConfigureAwait(false);
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
                                commandTimeout,
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
                    mappings.Discriminator,
                    SqlBulkCopyOptions.Default,
                    IncludeRowNumber.No,
                    commandTimeout,
                    useTableLock);

                // Make sure that we only insert entities not already in the database.
                var notExistingEntities = await BulkSelectNotExistingByTypeAsync(
                    ctx, t, entities, pkColumnMappings, transaction, commandTimeout, useTableLock, cancellationToken).ConfigureAwait(false);
                rowsAffected += notExistingEntities.Count;

                var s = new Stopwatch();
                s.Start();
                using (var reader = CreateEntitiesDataReader(table, notExistingEntities, properties, t, mappings.Discriminator, IncludeRowNumber.No))
                    await bulkCopy.WriteToServerAsync(reader, cancellationToken).ConfigureAwait(false);
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
        /// <summary>
        /// True for a single primary key whose value is generated by the store
        /// (IDENTITY or computed / DatabaseGeneratedOption.Identity Guid defaults).
        /// Aligned with EF Core <c>IsStoreGenerated</c> for the MERGE … OUTPUT path.
        /// </summary>
        private static bool IsPrimaryKeyStoreGenerated(TableColumnMapping[] pkColumnMappings)
        {
            return pkColumnMappings.Length == 1 &&
                   (pkColumnMappings[0].TableColumn.IsStoreGeneratedIdentity ||
                    pkColumnMappings[0].TableColumn.IsStoreGeneratedComputed);
        }

        /// <summary>
        /// Inserts from the temp table using MERGE ON 1=0 … OUTPUT so SQL Server
        /// returns inserted identity values ordered by our temp-table rowno.
        /// Required because SqlBulkCopy into a real table cannot reliably return
        /// per-row identities. When AllowNotNullSelfReferences is Yes, CHECK/FK
        /// constraints are temporarily disabled (re-enabled by the outer finally).
        /// Command timeout comes from <see cref="BulkInsertRequest{T}.CommandTimeout"/>
        /// (default 30 minutes).
        /// </summary>
        private static async Task<int> SelectIntoUsingOutputClauseAsync(
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
            Type t,
            TimeSpan commandTimeout,
            CancellationToken cancellationToken = default)
        {
            using var cmd = CreateSqlCommand(string.Empty, conn, transaction, commandTimeout);

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
                               inserted.[{pkColumn.Name}]; 
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
                               inserted.[{pkColumn.Name}]; 
                                 ";
            }

            cmd.CommandText = query;
            s.Restart();
            object[] ids = null;
            using (var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
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
                    $"Bulk insert of {newEntities.Count} entities of type {t} returned {ids.Length} " +
                    "generated primary key values; the counts must match. " +
                    "This usually indicates a MERGE OUTPUT / staging mismatch. Please file a bug report with a repro.");

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

        private static ObjectListDataReader CreateEntitiesDataReader(
            DataTable schema,
            IList entities,
            BulkPropertyInfo[] properties,
            Type t,
            Discriminator discriminator,
            IncludeRowNumber includeRowNumber)
        {
            return new ObjectListDataReader(schema, entities, (entity, rowIndex) =>
            {
                var columnValues = new List<object>();
                if (entity is ExpandoObject e)
                {
                    columnValues.AddRange(properties.Select(p => (object)GetProperty(p.Name, e)));
                }
                else
                {
                    columnValues.AddRange(properties.Select(p => (object)GetProperty(t, p.Name, entity, DBNull.Value)));
                    if (discriminator != null)
                        columnValues.Add(discriminator.Value);
                }

                if (includeRowNumber == IncludeRowNumber.Yes)
                    columnValues.Add(rowIndex + 1);

                return columnValues.ToArray();
            });
        }

        /// <summary>
        /// The purpose of this method is to figure out which of the supplied
        /// entities are new to the database. For tables with database
        /// generated primary keys this is fairly straight forward but for other
        /// tables we need to actually have a look in the database table.
        /// We can use our BulkSelectNotExisting to do this but it requires
        /// some extra magic since we must invoke the generic method with
        /// run-time types. It gets a little messy but it works.
        /// Unset detection uses <see cref="IsUnsetKeyValue"/> (same as the
        /// recursive FK path) so string/Guid PKs do not hit dynamic == 0.
        /// </summary>
        /// <param name="entities"></param>
        /// <param name="pkProperty"></param>
        /// <param name="t"></param>
        /// <returns></returns>
        /// <summary>
        /// Before recursively inserting a navigation, clear leftover
        /// identity/store-generated PK values so <see cref="SelectNewEntities"/>
        /// does not treat them as already persisted. Guid/DateTime/string
        /// client-assigned keys are left alone. Existing DB parents should set
        /// the FK on the child rather than relying on nav.Id alone.
        /// </summary>
        private static void ClearLeftoverStoreGeneratedNavKey(
            object navProperty,
            Type navPropertyType,
            string fromPropertyName,
            PropertyInfo fromPropertyInfo,
            object currentValue,
            Dictionary<Type, Mappings> mappingsByType,
            DbContext ctx)
        {
            if (fromPropertyInfo == null || !IsKeyValueSet(currentValue, fromPropertyInfo.PropertyType))
                return;

            var keyType = fromPropertyInfo.PropertyType;
            if (IsGuid(keyType) || IsDateTime(keyType) || keyType == typeof(string))
                return;

            if (!mappingsByType.TryGetValue(navPropertyType, out var navMappings))
            {
                navMappings = MappingExtractor.GetMappings(ctx, navPropertyType);
                mappingsByType[navPropertyType] = navMappings;
            }

            if (!navMappings.ColumnMappingByPropertyName.TryGetValue(fromPropertyName, out var fromMapping))
                return;

            if (!fromMapping.TableColumn.IsStoreGeneratedIdentity &&
                !fromMapping.TableColumn.IsStoreGeneratedComputed)
                return;

            SetProperty(fromPropertyName, navProperty, CreateUnsetKeyValue(fromPropertyInfo.PropertyType));
        }

        private static ArrayList SelectNewEntities(IList entities, EdmProperty pkProperty, Type t)
        {
            var newEntities = new ArrayList();
            var pkClrType = pkProperty.PrimitiveType?.ClrEquivalentType ?? typeof(object);

            if (entities[0] is ExpandoObject)
            {
                foreach (var entity in entities)
                {
                    var e = (ExpandoObject)entity;
                    var pk = GetProperty(pkProperty.Name, e);
                    if (IsUnsetKeyValue(pk, pkClrType))
                        newEntities.Add(entity);
                }
            }
            else
            {
                foreach (var entity in entities)
                {
                    var pk = GetProperty(t, pkProperty.Name, entity);
                    if (IsUnsetKeyValue(pk, pkClrType))
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
                        if (val == null)
                        {
                            throw new ArgumentException(
                                $"Complex property '{property.Name}' on type '{t.Name}' is null. " +
                                "Bulk insert requires complex properties to be non-null so nested columns can be flattened.",
                                property.Name);
                        }

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
                        if (val == null)
                        {
                            throw new ArgumentException(
                                $"Complex property '{property.Name}' is null. " +
                                "Bulk insert requires nested complex properties to be non-null so columns can be flattened.",
                                property.Name);
                        }

                        Flatten(flatEntity, val, null);
                    }
                }
            }
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
            var connection = await ResolveSqlConnectionAsync(ctx, cancellationToken).ConfigureAwait(false);

            // Table/schema names come from EF mappings; pass as parameters so a
            // quote in an identifier cannot break (or alter) the catalog query.
            var query = @"
                    SELECT  col.name
                    FROM sys.indexes ind
                    INNER JOIN sys.index_columns ic ON ind.object_id = ic.object_id and ind.index_id = ic.index_id
                    INNER JOIN sys.columns col ON ic.object_id = col.object_id and ic.column_id = col.column_id
                    INNER JOIN sys.tables t ON ind.object_id = t.object_id
                    WHERE t.name = @tableName AND ind.type_desc = 'CLUSTERED'";

            if (!string.IsNullOrEmpty(schema))
            {
                query += " AND SCHEMA_NAME(t.schema_id) = @schema";
            }

            query += " ORDER BY ic.index_column_id;";

            using var cmd = CreateSqlCommand(query, connection, sqlTransaction, TimeSpan.FromSeconds(30));
            cmd.AddParameter(new SqlParameter("@tableName", SqlDbType.NVarChar, 128) { Value = tableName });
            if (!string.IsNullOrEmpty(schema))
            {
                cmd.AddParameter(new SqlParameter("@schema", SqlDbType.NVarChar, 128) { Value = schema });
            }

            string[] clusteredColumns = null;
            using (var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
            {
                clusteredColumns = (
                        from IDataRecord r in reader
                        select (string)r[0])
                    .ToArray();
            }

            // Property names might not be identical to column names; CI columns
            // that are concurrency tokens (or otherwise excluded from regular
            // mappings) must not KeyNotFound — resolve or skip them.
            return ResolveClusteredIndexPropertyNames(clusteredColumns, mappings);
        }

        private static string[] ResolveClusteredIndexPropertyNames(string[] clusteredColumns, Mappings mappings)
        {
            var propertyNames = new List<string>(clusteredColumns.Length);
            foreach (var columnName in clusteredColumns)
            {
                if (mappings.ColumnMappingByColumnName.TryGetValue(columnName, out var mapping))
                {
                    // Complex-type columns expose leaf CLR names (e.g. City) that are
                    // not properties on the root entity — Sort cannot OrderBy them.
                    if (!mapping.IsIncludedFromComplexType)
                        propertyNames.Add(mapping.EntityProperty.Name);
                    continue;
                }

                var concurrency = mappings.ConcurrencyTokenMappings?
                    .FirstOrDefault(m =>
                        string.Equals(m.TableColumn.Name, columnName, StringComparison.OrdinalIgnoreCase));
                if (concurrency != null)
                {
                    propertyNames.Add(concurrency.EntityProperty.Name);
                    continue;
                }
            }

            return propertyNames.ToArray();
        }

        private static IList<T> Sort<T>(IList<T> entities, string[] sortColumns)
        {
            if (entities == null || entities.Count == 0 || sortColumns == null || sortColumns.Length == 0)
                return entities;

            var t = entities[0].GetType();
            var properties = sortColumns
                .Select(name => t.GetProperty(name))
                .Where(p => p != null)
                .ToArray();
            if (properties.Length == 0)
                return entities;

            var sortedEntities = entities.OrderBy(u => properties[0].GetValue(u));
            for (var i = 1; i < properties.Length; i++)
            {
                var property = properties[i];
                sortedEntities = sortedEntities.ThenBy(u => property.GetValue(u));
            }

            return sortedEntities.ToList();
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

        private static async Task<string> FillTempTableAsync(
            SqlServerConnection conn,
            IList entities,
            TableName tableName,
            Dictionary<string, TableColumnMapping> columnMappings,
            TableColumnMapping[] keyColumnMappings,
            TableColumnMapping[] nonKeyColumnMappings,
            SqlTransaction sqlTransaction,
            TimeSpan commandTimeout,
            bool useTableLock = false,
            CancellationToken cancellationToken = default,
            TableColumnMapping[] concurrencyTokenMappings = null,
            Discriminator discriminator = null)
        {
            concurrencyTokenMappings = concurrencyTokenMappings ?? Array.Empty<TableColumnMapping>();

            var columnNames = keyColumnMappings.Select(m => m.TableColumn.Name)
                .Concat(nonKeyColumnMappings.Select(m => m.TableColumn.Name))
                .Concat(concurrencyTokenMappings.Select(m => m.TableColumn.Name))
                .ToArray();

            // timestamp/rowversion columns are not insertable; stage them as varbinary(8).
            // EF6 often exposes SQL rowversion as TypeName "varbinary(max)" in EDM metadata.
            var castToVarBinary8 = new HashSet<string>(
                concurrencyTokenMappings
                    .Where(RequiresVarBinary8TempColumn)
                    .Select(m => m.TableColumn.Name),
                StringComparer.OrdinalIgnoreCase);

            var tempTableName = await CreateTempTableAsync(
                conn,
                sqlTransaction,
                tableName,
                discriminator,
                columnNames,
                IncludeRowNumber.Yes,
                cancellationToken,
                castToVarBinary8).ConfigureAwait(false);

            var identityInsertEnabled = false;
            try
            {
                // Temp table inherits identity metadata from the source; KeepIdentity
                // bulk-copy of explicit key values requires IDENTITY_INSERT ON.
                if (keyColumnMappings.Length == 1 &&
                    ((keyColumnMappings[0].TableColumn.IsStoreGeneratedIdentity &&
                      keyColumnMappings[0].TableColumn.TypeName != "uniqueidentifier") ||
                     keyColumnMappings[0].TableColumn.IsStoreGeneratedComputed))
                {
                    await EnableIdentityInsertAsync(tempTableName, conn, sqlTransaction, cancellationToken).ConfigureAwait(false);
                    identityInsertEnabled = true;
                }

                var allProperties = GetProperties(entities, columnMappings);
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

                // FillTempTable also needs concurrency tokens in columnMappings for CreateBulkCopy.
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
                    discriminator,
                    SqlBulkCopyOptions.KeepIdentity,
                    IncludeRowNumber.Yes,
                    commandTimeout,
                    useTableLock);

                var type = entities[0].GetType();

                //
                // Fill the temp table.
                //
                using (var reader = CreateEntitiesDataReader(table, entities, properties, type, discriminator, IncludeRowNumber.Yes))
                    await bulkCopy.WriteToServerAsync(reader, cancellationToken).ConfigureAwait(false);

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

        private static bool IsRowVersionStoreType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return false;
            var name = typeName;
            var paren = name.IndexOf('(');
            if (paren >= 0)
                name = name.Substring(0, paren);
            return name.Equals("timestamp", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("rowversion", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// SQL rowversion columns must be staged as varbinary(8). EF6 metadata often
        /// reports them as varbinary(max) rather than timestamp/rowversion.
        /// </summary>
        private static bool RequiresVarBinary8TempColumn(TableColumnMapping mapping)
        {
            if (IsRowVersionStoreType(mapping.TableColumn.TypeName))
                return true;

            var clrType = mapping.EntityProperty.PrimitiveType?.ClrEquivalentType;
            return clrType == typeof(byte[]) && mapping.TableColumn.IsStoreGeneratedComputed;
        }

        private static Task EnableIdentityInsertAsync(
            string tableName,
            SqlServerConnection conn,
            SqlTransaction sqlTransaction,
            CancellationToken cancellationToken = default)
        {
            return conn.EnableIdentityInsertAsync(tableName, sqlTransaction, cancellationToken);
        }

        private static Task DisableIdentityInsertAsync(
            string tableName,
            SqlServerConnection conn,
            SqlTransaction sqlTransaction,
            CancellationToken cancellationToken = default)
        {
            return conn.DisableIdentityInsertAsync(tableName, sqlTransaction, cancellationToken);
        }
    }
}
