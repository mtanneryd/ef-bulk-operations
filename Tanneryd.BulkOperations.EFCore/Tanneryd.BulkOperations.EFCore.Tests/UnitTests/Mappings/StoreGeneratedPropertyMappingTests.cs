/*
 * Copyright ©  2017-2026 Tånneryd IT AB
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
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.EFCore.Model;

namespace Tanneryd.BulkOperations.EFCore.Tests.UnitTests.Mappings
{
    /// <summary>
    /// Regression for 4ace4af / ba8db4d: IsStoreGeneratedProperty must classify
    /// IDENTITY / SQL defaults / OnAddOrUpdate as store-generated (MERGE path),
    /// while SequenceHiLo and bare ValueGeneratedOnAdd stay client-side so their
    /// values are bulk-copied. Model-only — no database connection required.
    /// </summary>
    [TestClass]
    public class StoreGeneratedPropertyMappingTests
    {
        private const string DummyConnectionString =
            @"data source=(localdb)\MSSQLLocalDB;initial catalog=StoreGeneratedPropertyMappingTests;Integrated Security=SSPI;TrustServerCertificate=true";

        private static readonly DbContextOptions<StoreGeneratedProbeContext> Options =
            new DbContextOptionsBuilder<StoreGeneratedProbeContext>()
                .UseSqlServer(DummyConnectionString)
                .Options;

        [TestMethod]
        public void IdentityPrimaryKey_ShouldBeStoreGeneratedAndIdentity()
        {
            using var ctx = new StoreGeneratedProbeContext(Options);
            var mapping = Mapping(ctx, typeof(IdentityPkEntity), nameof(IdentityPkEntity.Id));

            Assert.IsTrue(mapping.IsStoreGenerated);
            Assert.IsTrue(mapping.IsIdentity);
            Assert.IsTrue(DbContextExtensions.IsPrimaryKeyStoreGenerated(new[] { mapping }));
        }

        [TestMethod]
        public void GuidPrimaryKeyWithSqlDefault_ShouldBeStoreGeneratedButNotIdentity()
        {
            using var ctx = new StoreGeneratedProbeContext(Options);
            var mapping = Mapping(ctx, typeof(GuidDefaultPkEntity), nameof(GuidDefaultPkEntity.Id));

            Assert.IsTrue(mapping.IsStoreGenerated,
                "NEWSEQUENTIALID() / HasDefaultValueSql must select the MERGE … OUTPUT path.");
            Assert.IsFalse(mapping.IsIdentity,
                "SQL defaults must not enable IDENTITY_INSERT.");
            Assert.IsTrue(DbContextExtensions.IsPrimaryKeyStoreGenerated(new[] { mapping }));
        }

        [TestMethod]
        public void ClientGuidPrimaryKey_ShouldNotBeStoreGenerated()
        {
            using var ctx = new StoreGeneratedProbeContext(Options);
            var mapping = Mapping(ctx, typeof(ClientGuidPkEntity), nameof(ClientGuidPkEntity.Id));

            Assert.IsFalse(mapping.IsStoreGenerated,
                "Bare Guid ValueGeneratedOnAdd is client-side; MERGE must not be chosen.");
            Assert.IsFalse(mapping.IsIdentity);
            Assert.IsFalse(DbContextExtensions.IsPrimaryKeyStoreGenerated(new[] { mapping }));
        }

        [TestMethod]
        public void HiLoPrimaryKey_ShouldNotBeStoreGenerated()
        {
            using var ctx = new StoreGeneratedProbeContext(Options);
            var mapping = Mapping(ctx, typeof(HiLoPkEntity), nameof(HiLoPkEntity.Id));

            Assert.IsFalse(mapping.IsStoreGenerated,
                "SequenceHiLo values are assigned by the client before INSERT.");
            Assert.IsFalse(DbContextExtensions.IsPrimaryKeyStoreGenerated(new[] { mapping }));
        }

        [TestMethod]
        public void BareValueGeneratedOnAddNonPk_ShouldStayMappedAndNotStoreGenerated()
        {
            using var ctx = new StoreGeneratedProbeContext(Options);
            var mappings = new MappingsExtractor(ctx).GetMappings(typeof(ClientOnAddNonPkEntity));

            Assert.IsTrue(
                mappings.ColumnMappingByPropertyName.ContainsKey(nameof(ClientOnAddNonPkEntity.CreatedAt)),
                "Client OnAdd non-PK columns must remain in the bulk-copy column set.");
            Assert.IsFalse(
                mappings.ColumnMappingByPropertyName[nameof(ClientOnAddNonPkEntity.CreatedAt)].IsStoreGenerated);
        }

        [TestMethod]
        public void RowVersion_ShouldBeStoreGeneratedAndOmittedFromInsertMappings()
        {
            using var ctx = new StoreGeneratedProbeContext(Options);
            var mappings = new MappingsExtractor(ctx).GetMappings(typeof(RowVersionEntity));

            Assert.IsFalse(
                mappings.ColumnMappingByPropertyName.ContainsKey(nameof(RowVersionEntity.RowVersion)),
                "Store-generated non-PK columns must be omitted from INSERT mappings.");

            var token = mappings.ConcurrencyTokenMappings
                .Single(m => m.EntityProperty.Name == nameof(RowVersionEntity.RowVersion));
            Assert.IsTrue(token.IsStoreGenerated);
        }

        [TestMethod]
        public void IsPrimaryKeyStoreGenerated_ShouldRequireSingleStoreGeneratedKey()
        {
            var storeGenerated = new TableColumnMapping { IsStoreGenerated = true };
            var client = new TableColumnMapping { IsStoreGenerated = false };

            Assert.IsFalse(DbContextExtensions.IsPrimaryKeyStoreGenerated(Array.Empty<TableColumnMapping>()));
            Assert.IsFalse(DbContextExtensions.IsPrimaryKeyStoreGenerated(new[] { client }));
            Assert.IsTrue(DbContextExtensions.IsPrimaryKeyStoreGenerated(new[] { storeGenerated }));
            Assert.IsFalse(
                DbContextExtensions.IsPrimaryKeyStoreGenerated(new[] { storeGenerated, storeGenerated }),
                "Composite keys must not use the single-column MERGE … OUTPUT path.");
        }

        private static TableColumnMapping Mapping(DbContext ctx, Type entityType, string propertyName)
        {
            var mappings = new MappingsExtractor(ctx).GetMappings(entityType);
            Assert.IsTrue(
                mappings.ColumnMappingByPropertyName.ContainsKey(propertyName),
                $"Expected mapping for {entityType.Name}.{propertyName}.");
            return mappings.ColumnMappingByPropertyName[propertyName];
        }

        public class IdentityPkEntity
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        public class GuidDefaultPkEntity
        {
            public Guid Id { get; set; }
            public string Name { get; set; }
        }

        public class ClientGuidPkEntity
        {
            public Guid Id { get; set; }
            public string Name { get; set; }
        }

        public class HiLoPkEntity
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        public class ClientOnAddNonPkEntity
        {
            public int Id { get; set; }
            public DateTime? CreatedAt { get; set; }
            public string Name { get; set; }
        }

        public class RowVersionEntity
        {
            public int Id { get; set; }
            public byte[] RowVersion { get; set; }
            public string Name { get; set; }
        }

        public class StoreGeneratedProbeContext : DbContext
        {
            public StoreGeneratedProbeContext(DbContextOptions<StoreGeneratedProbeContext> options)
                : base(options)
            {
            }

            public DbSet<IdentityPkEntity> IdentityPks { get; set; }
            public DbSet<GuidDefaultPkEntity> GuidDefaultPks { get; set; }
            public DbSet<ClientGuidPkEntity> ClientGuidPks { get; set; }
            public DbSet<HiLoPkEntity> HiLoPks { get; set; }
            public DbSet<ClientOnAddNonPkEntity> ClientOnAddNonPks { get; set; }
            public DbSet<RowVersionEntity> RowVersions { get; set; }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<IdentityPkEntity>(e =>
                {
                    e.ToTable("IdentityPk");
                    e.HasKey(x => x.Id);
                    e.Property(x => x.Id).UseIdentityColumn();
                    e.Property(x => x.Name).HasMaxLength(50);
                });

                modelBuilder.Entity<GuidDefaultPkEntity>(e =>
                {
                    e.ToTable("GuidDefaultPk");
                    e.HasKey(x => x.Id);
                    e.Property(x => x.Id)
                        .HasDefaultValueSql("NEWSEQUENTIALID()")
                        .ValueGeneratedOnAdd();
                    e.Property(x => x.Name).HasMaxLength(50);
                });

                modelBuilder.Entity<ClientGuidPkEntity>(e =>
                {
                    e.ToTable("ClientGuidPk");
                    e.HasKey(x => x.Id);
                    // Bare OnAdd: client SequentialGuidValueGenerator, no store default.
                    e.Property(x => x.Id).ValueGeneratedOnAdd();
                    e.Property(x => x.Name).HasMaxLength(50);
                });

                modelBuilder.Entity<HiLoPkEntity>(e =>
                {
                    e.ToTable("HiLoPk");
                    e.HasKey(x => x.Id);
                    e.Property(x => x.Id).UseHiLo("HiLoPkSequence");
                    e.Property(x => x.Name).HasMaxLength(50);
                });

                modelBuilder.Entity<ClientOnAddNonPkEntity>(e =>
                {
                    e.ToTable("ClientOnAddNonPk");
                    e.HasKey(x => x.Id);
                    e.Property(x => x.Id).UseIdentityColumn();
                    e.Property(x => x.CreatedAt).ValueGeneratedOnAdd();
                    e.Property(x => x.Name).HasMaxLength(50);
                });

                modelBuilder.Entity<RowVersionEntity>(e =>
                {
                    e.ToTable("RowVersionEntity");
                    e.HasKey(x => x.Id);
                    e.Property(x => x.Id).UseIdentityColumn();
                    e.Property(x => x.RowVersion).IsRowVersion();
                    e.Property(x => x.Name).HasMaxLength(50);
                });
            }
        }
    }
}
