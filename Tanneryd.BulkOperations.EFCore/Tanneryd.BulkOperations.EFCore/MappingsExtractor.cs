using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore.Metadata;
using Tanneryd.BulkOperations.EFCore.Model;


namespace Tanneryd.BulkOperations.EFCore
{
    public class MappingsExtractor
    {
        private readonly DbContext _ctx;
        private Dictionary<Type, Mappings> _mappingsByType;


        public MappingsExtractor(DbContext ctx)
        {
            _ctx = ctx;
            LoadMappings();
        }

        public bool HasMappings(Type type)
        {
            return _mappingsByType.ContainsKey(type);
        }


        public Mappings GetMappings(Type type)
        {
            return _mappingsByType[type];
        }

        private void LoadMappings()
        {
            _mappingsByType = new Dictionary<Type, Mappings>();

            // Skip all views.
            var entityTypes = _ctx.Model.GetEntityTypes()
                .Where(t=>t.GetViewName() == null)
                .ToArray();
            foreach (var entityType in entityTypes)
            {
                var m = new Mappings();
                if (_mappingsByType.ContainsKey(entityType.ClrType))
                {
                    Console.WriteLine($@"Trying to add duplicate mapping for {entityType.ClrType}");
                    continue;
                }

                _mappingsByType.Add(entityType.ClrType, m);

                m.TableName = new TableName
                {
                    Name = entityType.GetTableName(),
                    Schema = entityType.GetSchema()
                };

                //
                // Add mappings for all scalar properties. That is, for all properties  
                // that do not represent other entities (navigation properties).
                //
                var tableColumnMappings = new List<TableColumnMapping>();
                var properties = entityType
                    .GetProperties()
                    .Where(p => p.ValueGenerated == ValueGenerated.Never || p.IsPrimaryKey())
                    ;
                foreach (var p in properties)
                {
                    var tableColumnMapping = new TableColumnMapping();
                    tableColumnMappings.Add(tableColumnMapping);
                    tableColumnMapping.EntityProperty = p;
                    tableColumnMapping.TableColumn = p.GetTableColumnMappings().First(); // When would this contain more than one entry?
                    tableColumnMapping.IsPrimaryKey = p.IsPrimaryKey();
                    tableColumnMapping.IsIdentity = p.GetValueGenerationStrategy() == SqlServerValueGenerationStrategy.IdentityColumn;
                }

                var columnMappingByPropertyName = tableColumnMappings.ToDictionary(m => m.EntityProperty.Name, m => m);
                var columnMappingByColumnName =
                    tableColumnMappings.ToDictionary(m => m.TableColumn.Column.Name, m => m);

                m.ColumnMappingByColumnName = columnMappingByColumnName;
                m.ColumnMappingByPropertyName = columnMappingByPropertyName;

                var foreignKeyMappings = new List<ForeignKeyMapping>();

                foreach (var n in entityType.GetNavigations())
                {
                    // Only bother with unknown relationships
                    if (foreignKeyMappings.All(m => m.NavigationPropertyName != n.Name))
                    {
                        var fkMapping = new ForeignKeyMapping
                        {
                            NavigationPropertyName = n.Name,
                            IsCollection = n.IsCollection,
                            FromType = n.ForeignKey.DeclaringEntityType.ClrType.ToString(),
                            ToType = n.ForeignKey.PrincipalEntityType.ClrType.ToString(),
                        };
                        foreignKeyMappings.Add(fkMapping);

                        var foreignKeyRelations = new List<ForeignKeyRelation>();
                        for (int i = 0; i < n.ForeignKey.Properties.Count; i++)
                        {
                            foreignKeyRelations.Add(new ForeignKeyRelation
                            {
                                FromProperty = n.ForeignKey.PrincipalKey.Properties[i].Name,
                                ToProperty = n.ForeignKey.Properties[i].Name,
                            });
                        }

                        fkMapping.ForeignKeyRelations = foreignKeyRelations.ToArray();
                    }
                }

                m.ToForeignKeyMappings = foreignKeyMappings.Where(m => m.FromType == entityType.Name).ToArray();
                m.FromForeignKeyMappings = foreignKeyMappings.Where(m => m.ToType == entityType.Name).ToArray();
            }
        }

        //public Mappings GetMappings(DbContext ctx, Type t)
        //{
        //    Discriminator discriminator = null;
        //    var objectContext = ((IObjectContextAdapter)ctx).ObjectContext;
        //    var workspace = objectContext.MetadataWorkspace;
        //    var containerName = objectContext.DefaultContainerName;
        //    t = ObjectContext.GetObjectType(t);
        //    var entityName = t.Name;

        //    // If we are dealing with table inheritance we need the base type name as well.
        //    var baseEntityName = t.BaseType?.Name;

        //    var storageMapping =
        //        (EntityContainerMapping)workspace.GetItem<GlobalItem>(containerName, DataSpace.CSSpace);
        //    var entitySetMaps = storageMapping.EntitySetMappings.ToList();
        //    var associationSetMaps = storageMapping.AssociationSetMappings.ToList();

        //    //
        //    // Add mappings for all scalar properties. That is, for all properties  
        //    // that do not represent other entities (navigation properties).
        //    //
        //    var entitySetMap = entitySetMaps
        //        .Single(m =>
        //            m.EntitySet.ElementType.Name == entityName ||
        //            m.EntitySet.ElementType.Name == baseEntityName);
        //    var typeMappings = entitySetMap.EntityTypeMappings;

        //    var propertyMappings = new List<PropertyMapping>();
        //    NavigationProperty[] navigationProperties = new NavigationProperty[0];

        //    // As long as we do not deal with table inheritance
        //    // we assume there is only one type mapping available.
        //    if (typeMappings.Count() == 1)
        //    {
        //        var typeMapping = typeMappings[0];
        //        var fragments = typeMapping.Fragments;
        //        var fragment = fragments[0];

        //        propertyMappings.AddRange(fragment.PropertyMappings);

        //        navigationProperties =
        //            typeMapping.EntityType.DeclaredMembers
        //                .Where(m => m.BuiltInTypeKind == BuiltInTypeKind.NavigationProperty)
        //                .Cast<NavigationProperty>()
        //                .Where(p => p.RelationshipType is AssociationType)
        //                .ToArray();
        //    }
        //    // If we have more than one type mapping we assume that we are
        //    // dealing with table inheritance.
        //    else
        //    {
        //        foreach (var tm in typeMappings)
        //        {
        //            var name = tm.EntityType != null ? tm.EntityType.Name : tm.IsOfEntityTypes[0].Name;

        //            if (name == baseEntityName ||
        //                name == entityName)
        //            {
        //                var fragments = tm.Fragments;
        //                var fragment = fragments[0];
        //                if (fragment.Conditions.Any())
        //                {
        //                    var valueConditionMapping =
        //                        (ValueConditionMapping)fragment.Conditions[0];
        //                    discriminator = new Discriminator
        //                    {
        //                        Column = valueConditionMapping.Column,
        //                        Value = valueConditionMapping.Value
        //                    };
        //                }
        //                var uknownMappings = fragment.PropertyMappings
        //                    .Where(m => propertyMappings.All(pm => pm.Property.Name != m.Property.Name));

        //                propertyMappings.AddRange(uknownMappings);
        //            }
        //        }

        //        //var typeMapping = typeMappings.Single(tm => tm.IsOfEntityTypes[0].Name == entityName);
        //    }


        //    var columnMappings = new List<TableColumnMapping>();
        //    columnMappings.AddRange(
        //        propertyMappings
        //            .Where(p => p is ScalarPropertyMapping)
        //            .Cast<ScalarPropertyMapping>()
        //            .Where(m => !m.Column.IsStoreGeneratedComputed)
        //            .Select(p => new TableColumnMapping
        //            {
        //                EntityProperty = p.Property,
        //                TableColumn = p.Column
        //            }));
        //    var complexPropertyMappings = propertyMappings
        //        .Where(p => p is ComplexPropertyMapping)
        //        .Cast<ComplexPropertyMapping>()
        //        .ToArray();
        //    if (complexPropertyMappings.Any())
        //    {
        //        columnMappings.AddRange(GetTableColumnMappings(complexPropertyMappings, true));
        //    }

        //    var columnMappingByPropertyName = columnMappings.ToDictionary(m => m.EntityProperty.Name, m => m);
        //    var columnMappingByColumnName = columnMappings.ToDictionary(m => m.TableColumn.Name, m => m);

        //    //
        //    // Add mappings for all navigation properties.
        //    //
        //    //
        //    var foreignKeyMappings = new List<ForeignKeyMapping>();

        //    foreach (var navigationProperty in navigationProperties)
        //    {
        //        var relType = (AssociationType)navigationProperty.RelationshipType;

        //        // Only bother with unknown relationships
        //        if (foreignKeyMappings.All(m => m.NavigationPropertyName != navigationProperty.Name))
        //        {
        //            var fkMapping = new ForeignKeyMapping
        //            {
        //                NavigationPropertyName = navigationProperty.Name,
        //                BuiltInTypeKind = navigationProperty.TypeUsage.EdmType.BuiltInTypeKind,
        //            };

        //            //
        //            // Many-To-Many
        //            //
        //            if (associationSetMaps.Any() &&
        //                associationSetMaps.Any(m => m.AssociationSet.Name == relType.Name))
        //            {
        //                var map = associationSetMaps.Single(m => m.AssociationSet.Name == relType.Name);
        //                var sourceMapping =
        //                    new TableColumnMapping
        //                    {
        //                        TableColumn = map.SourceEndMapping.PropertyMappings[0].Column,
        //                        EntityProperty = map.SourceEndMapping.PropertyMappings[0].Property,
        //                    };
        //                var targetMapping =
        //                    new TableColumnMapping
        //                    {
        //                        TableColumn = map.TargetEndMapping.PropertyMappings[0].Column,
        //                        EntityProperty = map.TargetEndMapping.PropertyMappings[0].Property,
        //                    };

        //                fkMapping.FromType = (map.SourceEndMapping.AssociationEnd.TypeUsage.EdmType as RefType)
        //                    ?.ElementType.Name;
        //                fkMapping.ToType = (map.TargetEndMapping.AssociationEnd.TypeUsage.EdmType as RefType)
        //                    ?.ElementType.Name;
        //                var schema = map.StoreEntitySet.Schema;
        //                var name = map.StoreEntitySet.Table ?? map.StoreEntitySet.Name;

        //                fkMapping.AssociationMapping = new AssociationMapping
        //                {
        //                    TableName = new TableName
        //                    {
        //                        Name = name,
        //                        Schema = schema,
        //                    },
        //                    Source = sourceMapping,
        //                    Target = targetMapping
        //                };
        //            }
        //            //
        //            // One-To-One or One-to-Many
        //            //
        //            else
        //            {
        //                fkMapping.FromType = relType.Constraint.FromProperties.First().DeclaringType.Name;
        //                fkMapping.ToType = relType.Constraint.ToProperties.First().DeclaringType.Name;

        //                var foreignKeyRelations = new List<ForeignKeyRelation>();
        //                for (int i = 0; i < relType.Constraint.FromProperties.Count; i++)
        //                {
        //                    foreignKeyRelations.Add(new ForeignKeyRelation
        //                    {
        //                        FromProperty = relType.Constraint.FromProperties[i].Name,
        //                        ToProperty = relType.Constraint.ToProperties[i].Name,
        //                    });
        //                }

        //                fkMapping.ForeignKeyRelations = foreignKeyRelations.ToArray();
        //            }

        //            foreignKeyMappings.Add(fkMapping);
        //        }
        //    }

        //    var tableName = GetTableName(ctx, t);

        //    var mappings = new Mappings
        //    {
        //        TableName = tableName,
        //        Discriminator = discriminator,
        //        ComplexPropertyNames = complexPropertyMappings.Select(m => m.Property.Name).ToArray(),
        //        ColumnMappingByPropertyName = columnMappingByPropertyName,
        //        ColumnMappingByColumnName = columnMappingByColumnName,
        //        ToForeignKeyMappings = foreignKeyMappings.Where(m => m.ToType == entityName).ToArray(),
        //        FromForeignKeyMappings = foreignKeyMappings.Where(m => m.FromType == entityName).ToArray()
        //    };

        //    foreach (var toPropertyName in mappings.ToForeignKeyMappings.SelectMany(m =>
        //        m.ForeignKeyRelations.Select(r => r.ToProperty)))
        //    {
        //        if (mappings.ColumnMappingByPropertyName.ContainsKey(toPropertyName))
        //        {
        //            var tableColumnMapping = mappings.ColumnMappingByPropertyName[toPropertyName];
        //            tableColumnMapping.IsForeignKey = true;
        //        }
        //    }

        //    foreach (var toPropertyName in mappings.FromForeignKeyMappings.SelectMany(m =>
        //        m.ForeignKeyRelations.Select(r => r.ToProperty)))
        //    {
        //        if (mappings.ColumnMappingByPropertyName.ContainsKey(toPropertyName))
        //        {
        //            var tableColumnMapping = mappings.ColumnMappingByPropertyName[toPropertyName];
        //            tableColumnMapping.IsForeignKey = true;
        //        }
        //    }

        //    var associationMappings = mappings.ToForeignKeyMappings
        //        .Where(m => m.AssociationMapping != null)
        //        .Select(m => m.AssociationMapping);
        //    foreach (var associationMapping in associationMappings)
        //    {
        //        associationMapping.Source.IsForeignKey = true;
        //        associationMapping.Target.IsForeignKey = true;
        //    }

        //    associationMappings = mappings.FromForeignKeyMappings
        //        .Where(m => m.AssociationMapping != null)
        //        .Select(m => m.AssociationMapping);
        //    foreach (var associationMapping in associationMappings)
        //    {
        //        associationMapping.Source.IsForeignKey = true;
        //        associationMapping.Target.IsForeignKey = true;
        //    }

        //    return mappings;
        //}


        //private static IEnumerable<TableColumnMapping> GetTableColumnMappings(ICollection<PropertyMapping> properties,
        //    bool isIncludedFromComplexType)
        //{
        //    if (!properties.Any()) yield break;

        //    var scalarPropertyMappings =
        //        properties
        //            .Where(p => p is ScalarPropertyMapping)
        //            .Cast<ScalarPropertyMapping>()
        //            .Select(p => new TableColumnMapping
        //            {
        //                IsIncludedFromComplexType = isIncludedFromComplexType,
        //                EntityProperty = p.Property,
        //                TableColumn = p.Column
        //            });
        //    foreach (var mapping in scalarPropertyMappings)
        //    {
        //        yield return mapping;
        //    }

        //    var complexPropertyMappings =
        //        properties
        //            .Where(p => p is ComplexPropertyMapping)
        //            .Cast<ComplexPropertyMapping>()
        //            .SelectMany(m => m.TypeMappings.SelectMany(tm => tm.PropertyMappings)).ToArray();
        //    ;
        //    foreach (var p in GetTableColumnMappings(complexPropertyMappings, true))
        //    {
        //        yield return p;
        //    }
        //}

        public TableName GetTableName(DbContext ctx, Type t)
        {
            var entityType = ctx.Model.GetEntityTypes().Single(et => et.ClrType == t);
            return new TableName
            {
                Name = entityType.GetTableName(),
                Schema = entityType.GetSchema()
            };
        }
    }
}