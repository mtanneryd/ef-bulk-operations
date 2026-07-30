using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Core.EntityClient;
using System.Data.Entity.Core.Mapping;
using System.Data.Entity.Core.Metadata.Edm;
using System.Data.Entity.Core.Objects;
using System.Data.Entity.Infrastructure;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Tanneryd.BulkOperations.EF6.Model;

namespace Tanneryd.BulkOperations.EF6
{
    public class MappingsExtractor
    {
        public bool HasMappings(DbContext ctx, Type type)
        {
            if (IsDatabaseView(ctx, type))
                return false;

            try
            {
                GetMappings(ctx, type);
                return true;
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException)
            {
                return false;
            }
        }

        public Mappings GetMappings(DbContext ctx, Type t)
        {
            Discriminator discriminator = null;
            var objectContext = ((IObjectContextAdapter)ctx).ObjectContext;
            var workspace = objectContext.MetadataWorkspace;
            var containerName = objectContext.DefaultContainerName;
            t = ResolveMappedClrType(ctx, t);
            var entityName = t.Name;

            // If we are dealing with table inheritance we need the base type name as well.
            var baseEntityName = t.BaseType?.Name;

            var storageMapping =
                (EntityContainerMapping)workspace.GetItem<GlobalItem>(containerName, DataSpace.CSSpace);
            var entitySetMaps = storageMapping.EntitySetMappings.ToList();
            var associationSetMaps = storageMapping.AssociationSetMappings.ToList();

            //
            // Add mappings for all scalar properties. That is, for all properties  
            // that do not represent other entities (navigation properties).
            //
            var entitySetMap = entitySetMaps
                .Single(m =>
                    m.EntitySet.ElementType.Name == entityName ||
                    m.EntitySet.ElementType.Name == baseEntityName);
            var typeMappings = entitySetMap.EntityTypeMappings;

            var propertyMappings = new List<PropertyMapping>();
            NavigationProperty[] navigationProperties = Array.Empty<NavigationProperty>();

            // As long as we do not deal with table inheritance
            // we assume there is only one type mapping available.
            if (typeMappings.Count() == 1)
            {
                var typeMapping = typeMappings[0];
                var fragments = typeMapping.Fragments;
                var fragment = fragments[0];

                propertyMappings.AddRange(fragment.PropertyMappings);

                navigationProperties =
                    typeMapping.EntityType.DeclaredMembers
                        .Where(m => m.BuiltInTypeKind == BuiltInTypeKind.NavigationProperty)
                        .Cast<NavigationProperty>()
                        .Where(p => p.RelationshipType is AssociationType)
                        .ToArray();
            }
            // If we have more than one type mapping we assume that we are
            // dealing with table inheritance.
            else
            {
                foreach (var tm in typeMappings)
                {
                    var name = tm.EntityType != null ? tm.EntityType.Name : tm.IsOfEntityTypes[0].Name;

                    if (name == baseEntityName ||
                        name == entityName)
                    {
                        var fragments = tm.Fragments;
                        var fragment = fragments[0];
                        if (fragment.Conditions.Any())
                        {
                            var valueConditionMapping =
                                (ValueConditionMapping)fragment.Conditions[0];
                            discriminator = new Discriminator
                            {
                                Column = valueConditionMapping.Column,
                                Value = valueConditionMapping.Value
                            };
                        }
                        var uknownMappings = fragment.PropertyMappings
                            .Where(m => propertyMappings.All(pm => pm.Property.Name != m.Property.Name));

                        propertyMappings.AddRange(uknownMappings);
                    }
                }

                //var typeMapping = typeMappings.Single(tm => tm.IsOfEntityTypes[0].Name == entityName);
            }


            var columnMappings = new List<TableColumnMapping>();
            columnMappings.AddRange(
                propertyMappings
                    .Where(p => p is ScalarPropertyMapping)
                    .Cast<ScalarPropertyMapping>()
                    .Where(m => !m.Column.IsStoreGeneratedComputed)
                    .Select(p => new TableColumnMapping
                    {
                        EntityProperty = p.Property,
                        TableColumn = p.Column
                    }));
            var complexPropertyMappings = propertyMappings
                .Where(p => p is ComplexPropertyMapping)
                .Cast<ComplexPropertyMapping>()
                .ToArray();
            var complexLeafColumnNameByPath = new Dictionary<string, string>(StringComparer.Ordinal);
            if (complexPropertyMappings.Any())
            {
                foreach (var complexPropertyMapping in complexPropertyMappings)
                {
                    columnMappings.AddRange(GetTableColumnMappingsFromComplex(
                        complexPropertyMapping,
                        complexPropertyMapping.Property.Name,
                        complexLeafColumnNameByPath));
                }
            }

            // Complex leaves are keyed by store column name so sibling complex
            // properties that share a leaf CLR name remain unique.
            var columnMappingByPropertyName = columnMappings.ToDictionary(
                m => m.IsIncludedFromComplexType ? m.TableColumn.Name : m.EntityProperty.Name,
                m => m);
            var columnMappingByColumnName = columnMappings.ToDictionary(m => m.TableColumn.Name, m => m);

            // Concurrency tokens (often store-generated rowversion) are tracked separately
            // so BulkUpdate can join on them without BulkInsert trying to write them.
            var concurrencyTokenMappings = propertyMappings
                .Where(p => p is ScalarPropertyMapping)
                .Cast<ScalarPropertyMapping>()
                .Where(m => m.Property.ConcurrencyMode == ConcurrencyMode.Fixed)
                .Select(p => new TableColumnMapping
                {
                    EntityProperty = p.Property,
                    TableColumn = p.Column
                })
                .ToArray();

            //
            // Add mappings for all navigation properties.
            //
            //
            var foreignKeyMappings = new List<ForeignKeyMapping>();

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
                    // Many-To-Many: both ends Many and a join-table AssociationSetMapping.
                    // Independent associations (MapKey) also appear in AssociationSetMappings
                    // with Constraint == null — do not treat those as M2M.
                    //
                    var associationSetMap = associationSetMaps.FirstOrDefault(m =>
                        m.AssociationSet.Name == relType.Name ||
                        m.AssociationSet.ElementType.Name == relType.Name);
                    var isManyToMany = relType.AssociationEndMembers.Count == 2 &&
                        relType.AssociationEndMembers.All(e =>
                            e.RelationshipMultiplicity == RelationshipMultiplicity.Many);

                    if (associationSetMap != null && isManyToMany)
                    {
                        var map = associationSetMap;
                        // Map every PropertyMapping on each end so composite
                        // principal keys produce a full join-table column set.
                        var sourceMappings = map.SourceEndMapping.PropertyMappings
                            .Select(pm => new TableColumnMapping
                            {
                                TableColumn = pm.Column,
                                EntityProperty = pm.Property,
                            })
                            .ToArray();
                        var targetMappings = map.TargetEndMapping.PropertyMappings
                            .Select(pm => new TableColumnMapping
                            {
                                TableColumn = pm.Column,
                                EntityProperty = pm.Property,
                            })
                            .ToArray();

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
                            Sources = sourceMappings,
                            Targets = targetMappings
                        };
                    }
                    //
                    // One-To-One or One-to-Many (FK association with ReferentialConstraint)
                    //
                    else if (relType.Constraint != null)
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
                    else
                    {
                        // Independent association (MapKey / no CLR FK) or unmatched
                        // association metadata — cannot wire FKs for recursive insert.
                        continue;
                    }

                    foreignKeyMappings.Add(fkMapping);
                }
            }

            var tableName = GetTableName(ctx, t);

            var mappings = new Mappings
            {
                TableName = tableName,
                Discriminator = discriminator,
                ComplexPropertyNames = complexPropertyMappings.Select(m => m.Property.Name).ToArray(),
                ComplexLeafColumnNameByPath = complexLeafColumnNameByPath,
                ColumnMappingByPropertyName = columnMappingByPropertyName,
                ColumnMappingByColumnName = columnMappingByColumnName,
                ConcurrencyTokenMappings = concurrencyTokenMappings,
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
                foreach (var end in associationMapping.Sources.Concat(associationMapping.Targets))
                    end.IsForeignKey = true;
            }

            associationMappings = mappings.FromForeignKeyMappings
                .Where(m => m.AssociationMapping != null)
                .Select(m => m.AssociationMapping);
            foreach (var associationMapping in associationMappings)
            {
                foreach (var end in associationMapping.Sources.Concat(associationMapping.Targets))
                    end.IsForeignKey = true;
            }

            return mappings;
        }

        private static IEnumerable<TableColumnMapping> GetTableColumnMappingsFromComplex(
            ComplexPropertyMapping complexPropertyMapping,
            string pathPrefix,
            Dictionary<string, string> leafColumnNameByPath)
        {
            foreach (var typeMapping in complexPropertyMapping.TypeMappings)
            {
                foreach (var propertyMapping in typeMapping.PropertyMappings)
                {
                    if (propertyMapping is ScalarPropertyMapping scalar)
                    {
                        if (scalar.Column.IsStoreGeneratedComputed)
                            continue;

                        var mapping = new TableColumnMapping
                        {
                            IsIncludedFromComplexType = true,
                            EntityProperty = scalar.Property,
                            TableColumn = scalar.Column,
                        };
                        leafColumnNameByPath[pathPrefix + "." + scalar.Property.Name] = scalar.Column.Name;
                        yield return mapping;
                    }
                    else if (propertyMapping is ComplexPropertyMapping nested)
                    {
                        foreach (var mapping in GetTableColumnMappingsFromComplex(
                                     nested,
                                     pathPrefix + "." + nested.Property.Name,
                                     leafColumnNameByPath))
                        {
                            yield return mapping;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Unwraps EF dynamic proxies and unmapped subclasses to the most-derived
        /// CLR type that participates in the EF model (EntitySet / TPH mapping).
        /// Required so <see cref="DbContext.Set(Type)"/> and FK name matching work
        /// for proxy-like runtime types.
        /// </summary>
        public Type ResolveMappedClrType(DbContext ctx, Type type)
        {
            type = ObjectContext.GetObjectType(type);

            var objectContext = ((IObjectContextAdapter)ctx).ObjectContext;
            var workspace = objectContext.MetadataWorkspace;
            var containerName = objectContext.DefaultContainerName;
            var storageMapping =
                (EntityContainerMapping)workspace.GetItem<GlobalItem>(containerName, DataSpace.CSSpace);

            var mappedTypeNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entitySetMap in storageMapping.EntitySetMappings)
            {
                mappedTypeNames.Add(entitySetMap.EntitySet.ElementType.Name);
                foreach (var typeMapping in entitySetMap.EntityTypeMappings)
                {
                    if (typeMapping.EntityType != null)
                        mappedTypeNames.Add(typeMapping.EntityType.Name);
                    foreach (var isOfType in typeMapping.IsOfEntityTypes)
                        mappedTypeNames.Add(isOfType.Name);
                }
            }

            for (var current = type; current != null && current != typeof(object); current = current.BaseType)
            {
                if (mappedTypeNames.Contains(current.Name))
                    return current;
            }

            return type;
        }

        public TableName GetTableName(DbContext ctx, Type t)
        {
            t = ResolveMappedClrType(ctx, t);
            var dbSet = ctx.Set(t);
            var sql = dbSet.ToString();
            return ParseTableName(sql);
        }

        public TableName ParseTableName(string sql)
        {
            var pattern = @"FROM\s+\[(?<schema>[\w@$#_\. -]+)\]\.\[(?<table>[\w@$#_\. -]+)\]";
            var regex = new Regex(pattern);
            var match = regex.Match(sql);

            if (match.Success)
            {
                var schema = match.Groups["schema"].Value;
                var table = match.Groups["table"].Value;

                return new TableName { Schema = schema, Name = table };
            }

            pattern = @"FROM\s+\[(?<table>[\w@$#_\. -]+)\]";
            regex = new Regex(pattern);
            match = regex.Match(sql);

            if (match.Success)
            {
                var table = match.Groups["table"].Value;
                return new TableName { Schema = null, Name = table };
            }

            throw new ArgumentException($"Failed to parse table name from {sql}. Bulk operation failed.");
        }

        private TableName GetStorageTableName(DbContext ctx, Type t)
        {
            var objectContext = ((IObjectContextAdapter)ctx).ObjectContext;
            var workspace = objectContext.MetadataWorkspace;
            var containerName = objectContext.DefaultContainerName;
            t = ResolveMappedClrType(ctx, t);
            var entityName = t.Name;
            var baseEntityName = t.BaseType?.Name;

            var storageMapping =
                (EntityContainerMapping)workspace.GetItem<GlobalItem>(containerName, DataSpace.CSSpace);
            var entitySetMap = storageMapping.EntitySetMappings.Single(m =>
                m.EntitySet.ElementType.Name == entityName ||
                m.EntitySet.ElementType.Name == baseEntityName);
            var storeEntitySet = entitySetMap.EntityTypeMappings[0].Fragments[0].StoreEntitySet;
            return new TableName
            {
                Schema = storeEntitySet.Schema,
                Name = storeEntitySet.Table ?? storeEntitySet.Name
            };
        }

        private bool IsDatabaseView(DbContext ctx, Type type)
        {
            try
            {
                var tableName = GetStorageTableName(ctx, type);
                var connection = ctx.Database.Connection;
                if (connection is EntityConnection entityConnection)
                    connection = entityConnection.StoreConnection;
                var mustClose = connection.State != ConnectionState.Open;
                if (mustClose)
                    connection.Open();

                try
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText =
                            "SELECT COUNT(1) FROM INFORMATION_SCHEMA.VIEWS WHERE TABLE_SCHEMA = @p0 AND TABLE_NAME = @p1";
                        var schemaParam = command.CreateParameter();
                        schemaParam.ParameterName = "@p0";
                        schemaParam.Value = tableName.Schema;
                        command.Parameters.Add(schemaParam);
                        var nameParam = command.CreateParameter();
                        nameParam.ParameterName = "@p1";
                        nameParam.Value = tableName.Name;
                        command.Parameters.Add(nameParam);
                        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) > 0;
                    }
                }
                finally
                {
                    if (mustClose)
                        connection.Close();
                }
            }
            catch
            {
                return false;
            }
        }

    }
}
