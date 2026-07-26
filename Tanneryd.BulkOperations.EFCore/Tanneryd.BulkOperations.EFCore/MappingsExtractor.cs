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
            return _mappingsByType.ContainsKey(ResolveMappedClrType(type));
        }


        public Mappings GetMappings(Type type)
        {
            return _mappingsByType[ResolveMappedClrType(type)];
        }

        /// <summary>
        /// Unwraps proxy / unmapped subclass CLR types to the most-derived
        /// type present in the mappings cache. Does not touch <see cref="_ctx"/>
        /// because extractors are cached by <see cref="IModel"/> and that context
        /// instance may already be disposed.
        /// </summary>
        public Type ResolveMappedClrType(Type type)
        {
            for (var current = type; current != null && current != typeof(object); current = current.BaseType)
            {
                if (_mappingsByType.ContainsKey(current))
                    return current;
            }

            return type;
        }

        private void LoadMappings()
        {
            _mappingsByType = new Dictionary<Type, Mappings>();

            var entityTypes = _ctx.Model.GetEntityTypes()
                .Where(t => t.GetViewName() == null)
                .ToArray();

            foreach (var entityType in entityTypes)
            {
                // Shadow many-to-many join entities use Dictionary<string, object>;
                // AssociationMapping on the principal types covers those tables.
                if (entityType.ClrType == typeof(Dictionary<string, object>))
                    continue;
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

            // Include client-side OnAdd columns (no store default). Only omit
            // columns SQL Server fills when the INSERT list leaves them out.
            foreach (var property in entityType.GetProperties()
                         .Where(p => (!IsStoreGeneratedProperty(p) || p.IsPrimaryKey())
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

            // Pure many-to-many (skip navigations) — same role as EF6 AssociationSetMappings.
            foreach (var skipNavigation in entityType.GetSkipNavigations())
            {
                // Unidirectional relationships still expose a shadow inverse skip
                // navigation with no CLR property; those cannot be walked from entities.
                if (skipNavigation.PropertyInfo == null)
                    continue;
                if (foreignKeyMappings.Any(m => m.NavigationPropertyName == skipNavigation.Name))
                    continue;

                var associationMapping = CreateAssociationMapping(skipNavigation);
                if (associationMapping == null)
                    continue;

                foreignKeyMappings.Add(new ForeignKeyMapping
                {
                    NavigationPropertyName = skipNavigation.Name,
                    IsCollection = skipNavigation.IsCollection,
                    // Declaring entity is "from", target is "to" so this navigation lands in
                    // FromForeignKeyMappings for the declaring type (and the inverse skip
                    // nav does the same on the other side).
                    FromType = entityType.Name,
                    ToType = skipNavigation.TargetEntityType.Name,
                    AssociationMapping = associationMapping,
                    ForeignKeyRelations = Array.Empty<ForeignKeyRelation>(),
                });
            }

            mappings.ToForeignKeyMappings = foreignKeyMappings.Where(m => m.FromType == entityType.Name).ToArray();
            mappings.FromForeignKeyMappings = foreignKeyMappings.Where(m => m.ToType == entityType.Name).ToArray();

            foreach (var associationMapping in mappings.ToForeignKeyMappings
                         .Concat(mappings.FromForeignKeyMappings)
                         .Where(m => m.AssociationMapping != null)
                         .Select(m => m.AssociationMapping))
            {
                foreach (var end in associationMapping.Sources.Concat(associationMapping.Targets))
                    end.IsForeignKey = true;
            }

            return mappings;
        }

        /// <summary>
        /// Build join-table Source/Target mappings for a skip navigation.
        /// Supports multi-column association ends when an entity uses a composite PK.
        /// </summary>
        private static AssociationMapping CreateAssociationMapping(ISkipNavigation skipNavigation)
        {
            var inverse = skipNavigation.Inverse;
            if (inverse == null)
                return null;

            var thisFk = skipNavigation.ForeignKey;
            var otherFk = inverse.ForeignKey;
            if (thisFk.Properties.Count != thisFk.PrincipalKey.Properties.Count ||
                otherFk.Properties.Count != otherFk.PrincipalKey.Properties.Count)
                return null;

            var joinEntityType = skipNavigation.JoinEntityType;
            var sources = CreateAssociationEndMappings(thisFk);
            var targets = CreateAssociationEndMappings(otherFk);
            if (sources == null || targets == null)
                return null;

            return new AssociationMapping
            {
                TableName = new TableName
                {
                    Name = joinEntityType.GetTableName(),
                    Schema = joinEntityType.GetSchema(),
                },
                // Sources = declaring entity end; Targets = other end.
                // Together they form the composite PK of the join table.
                Sources = sources,
                Targets = targets,
            };
        }

        private static TableColumnMapping[] CreateAssociationEndMappings(IForeignKey foreignKey)
        {
            var mappings = new TableColumnMapping[foreignKey.Properties.Count];
            for (var i = 0; i < foreignKey.Properties.Count; i++)
            {
                var joinProperty = foreignKey.Properties[i];
                var principalProperty = foreignKey.PrincipalKey.Properties[i];
                var column = joinProperty.GetTableColumnMappings().FirstOrDefault();
                if (column == null)
                    return null;

                mappings[i] = new TableColumnMapping
                {
                    EntityProperty = principalProperty,
                    TableColumn = column,
                    IsPrimaryKey = true,
                    IsForeignKey = true,
                };
            }

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
                IsStoreGenerated = IsStoreGeneratedProperty(property),
                IsIncludedFromComplexType = isIncludedFromComplexType
            };
        }

        /// <summary>
        /// True when omitting the column from INSERT lets SQL Server supply the value
        /// (IDENTITY, DEFAULT constraints such as NEWSEQUENTIALID, computed columns).
        /// Client-side generators (SequenceHiLo, bare Guid ValueGeneratedOnAdd) are excluded.
        /// Aligned with EF6 identity-or-computed PK detection for the MERGE … OUTPUT path.
        /// </summary>
        private static bool IsStoreGeneratedProperty(IProperty property)
        {
            var strategy = property.GetValueGenerationStrategy();
            if (strategy == SqlServerValueGenerationStrategy.IdentityColumn)
                return true;
            if (strategy == SqlServerValueGenerationStrategy.SequenceHiLo)
                return false;

            // Explicit SQL default (e.g. NEWSEQUENTIALID()) => store generates on INSERT.
            if (!string.IsNullOrEmpty(property.GetDefaultValueSql()))
                return true;

            // Computed / rowversion-style store generation.
            if (property.ValueGenerated == ValueGenerated.OnAddOrUpdate)
                return true;

            // Bare ValueGeneratedOnAdd without a store default is typically a client
            // value generator (e.g. SequentialGuidValueGenerator) — not store-generated.
            return false;
        }

        private static void AddComplexTypeColumnMappings(
            List<TableColumnMapping> mappings,
            IComplexProperty complexProperty)
        {
            foreach (var property in complexProperty.ComplexType.GetProperties())
            {
                if (!IsStoreGeneratedProperty(property) || property.IsPrimaryKey())
                    mappings.Add(CreateTableColumnMapping(property, true));
            }

            foreach (var nestedComplexProperty in complexProperty.ComplexType.GetComplexProperties())
                AddComplexTypeColumnMappings(mappings, nestedComplexProperty);
        }

        public TableName GetTableName(DbContext ctx, Type t)
        {
            // Prefer the mappings cache (safe with a disposed extractor context),
            // then fall back to walking the live ctx model (covers database views
            // which are intentionally omitted from the cache).
            for (var current = t; current != null && current != typeof(object); current = current.BaseType)
            {
                if (_mappingsByType.TryGetValue(current, out var mappings))
                    return mappings.TableName;

                var entityType = ctx.Model.FindEntityType(current);
                if (entityType != null)
                {
                    return new TableName
                    {
                        Name = entityType.GetTableName(),
                        Schema = entityType.GetSchema()
                    };
                }
            }

            var fallback = ctx.Model.GetEntityTypes().Single(et => et.ClrType == t);
            return new TableName
            {
                Name = fallback.GetTableName(),
                Schema = fallback.GetSchema()
            };
        }
    }
}
