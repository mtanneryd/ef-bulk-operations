using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Core.Mapping;
using System.Data.Entity.Core.Metadata.Edm;
using System.Data.Entity.Core.Objects;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Text.RegularExpressions;
using Tanneryd.BulkOperations.EF6.Model;

namespace Tanneryd.BulkOperations.EF6
{
    public class MappingsExtractor
    {
        public Mappings GetMappings(DbContext ctx, Type t)
        {
            Discriminator discriminator = null;
            var objectContext = ((IObjectContextAdapter)ctx).ObjectContext;
            var workspace = objectContext.MetadataWorkspace;
            var containerName = objectContext.DefaultContainerName;
            t = ObjectContext.GetObjectType(t);
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
            NavigationProperty[] navigationProperties = new NavigationProperty[0];

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
                Discriminator = discriminator,
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

        public TableName GetTableName(DbContext ctx, Type t)
        {
            var dbSet = ctx.Set(t);
            var sql = dbSet.ToString();
            return ParseTableName(sql);
        }

        public TableName ParseTableName(string sql)
        {
            var pattern = @"FROM\s+\[(?<schema>[\w@$#_\. ]+)\]\.\[(?<table>[\w@$#_\. ]+)\]";
            var regex = new Regex(pattern);
            var match = regex.Match(sql);

            if (match.Success)
            {
                var schema = match.Groups["schema"].Value;
                var table = match.Groups["table"].Value;

                return new TableName { Schema = schema, Name = table };
            }

            pattern = @"FROM\s+\[(?<table>[\w@$#_\. ]+)\]";
            regex = new Regex(pattern);
            match = regex.Match(sql);

            if (match.Success)
            {
                var table = match.Groups["table"].Value;
                return new TableName { Schema = "dbo", Name = table };
            }

            throw new ArgumentException($"Failed to parse table name from {sql}. Bulk operation failed.");
        }

    }
}