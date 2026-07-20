using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;
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

            var entityTypes = _ctx.Model.GetEntityTypes()
                .Where(t => t.GetViewName() == null)
                .ToArray();

            foreach (var entityType in entityTypes)
            {
                if (_mappingsByType.ContainsKey(entityType.ClrType))
                    continue;

                _mappingsByType.Add(entityType.ClrType, BuildMappings(entityType));
            }
        }

        private static Mappings BuildMappings(IEntityType entityType)
        {
            var mappings = new Mappings
            {
                TableName = new TableName
                {
                    Name = entityType.GetTableName(),
                    Schema = entityType.GetSchema()
                }
            };

            var tableColumnMappings = new List<TableColumnMapping>();

            var discriminatorProperty = entityType.BaseType != null
                ? entityType.FindDiscriminatorProperty()
                : null;

            foreach (var property in entityType.GetProperties()
                         .Where(p => (p.ValueGenerated == ValueGenerated.Never || p.IsPrimaryKey())
                                     && p != discriminatorProperty))
            {
                tableColumnMappings.Add(CreateTableColumnMapping(property, false));
            }

            // Concurrency tokens (often rowversion) are tracked separately so BulkUpdate
            // can join on them without BulkInsert trying to write them.
            mappings.ConcurrencyTokenMappings = entityType.GetProperties()
                .Where(p => p.IsConcurrencyToken)
                .Select(p => CreateTableColumnMapping(p, false))
                .ToArray();

            var complexPropertyNames = new List<string>();
            foreach (var complexProperty in entityType.GetComplexProperties())
            {
                complexPropertyNames.Add(complexProperty.Name);
                AddComplexTypeColumnMappings(tableColumnMappings, complexProperty);
            }

            mappings.ComplexPropertyNames = complexPropertyNames.ToArray();

            if (discriminatorProperty != null)
            {
                mappings.Discriminator = new Discriminator
                {
                    Column = discriminatorProperty,
                    Value = entityType.GetDiscriminatorValue()
                };
            }

            mappings.ColumnMappingByPropertyName =
                tableColumnMappings.ToDictionary(m => m.EntityProperty.Name, m => m);
            mappings.ColumnMappingByColumnName =
                tableColumnMappings.ToDictionary(m => m.TableColumn.Column.Name, m => m);

            var foreignKeyMappings = new List<ForeignKeyMapping>();

            foreach (var navigation in entityType.GetNavigations())
            {
                if (foreignKeyMappings.All(m => m.NavigationPropertyName != navigation.Name))
                {
                    var fkMapping = new ForeignKeyMapping
                    {
                        NavigationPropertyName = navigation.Name,
                        IsCollection = navigation.IsCollection,
                        // Use entity type Name (not ClrType.ToString()) so filters below
                        // stay consistent for shared-type / short-named entity types.
                        FromType = navigation.ForeignKey.DeclaringEntityType.Name,
                        ToType = navigation.ForeignKey.PrincipalEntityType.Name,
                    };
                    foreignKeyMappings.Add(fkMapping);

                    var foreignKeyRelations = new List<ForeignKeyRelation>();
                    for (int i = 0; i < navigation.ForeignKey.Properties.Count; i++)
                    {
                        foreignKeyRelations.Add(new ForeignKeyRelation
                        {
                            FromProperty = navigation.ForeignKey.PrincipalKey.Properties[i].Name,
                            ToProperty = navigation.ForeignKey.Properties[i].Name,
                        });
                    }

                    fkMapping.ForeignKeyRelations = foreignKeyRelations.ToArray();
                }
            }

            mappings.ToForeignKeyMappings = foreignKeyMappings.Where(m => m.FromType == entityType.Name).ToArray();
            mappings.FromForeignKeyMappings = foreignKeyMappings.Where(m => m.ToType == entityType.Name).ToArray();

            return mappings;
        }

        private static TableColumnMapping CreateTableColumnMapping(IProperty property, bool isIncludedFromComplexType)
        {
            return new TableColumnMapping
            {
                EntityProperty = property,
                TableColumn = property.GetTableColumnMappings().First(),
                IsPrimaryKey = property.IsPrimaryKey(),
                IsIdentity = property.GetValueGenerationStrategy() == SqlServerValueGenerationStrategy.IdentityColumn,
                IsIncludedFromComplexType = isIncludedFromComplexType
            };
        }

        private static void AddComplexTypeColumnMappings(
            List<TableColumnMapping> mappings,
            IComplexProperty complexProperty)
        {
            foreach (var property in complexProperty.ComplexType.GetProperties())
            {
                if (property.ValueGenerated == ValueGenerated.Never || property.IsPrimaryKey())
                    mappings.Add(CreateTableColumnMapping(property, true));
            }

            foreach (var nestedComplexProperty in complexProperty.ComplexType.GetComplexProperties())
                AddComplexTypeColumnMappings(mappings, nestedComplexProperty);
        }

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
