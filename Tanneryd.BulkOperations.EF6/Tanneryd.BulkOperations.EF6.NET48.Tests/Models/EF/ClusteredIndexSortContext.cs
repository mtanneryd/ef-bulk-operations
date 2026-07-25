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

using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity;
using System.Data.Entity.ModelConfiguration;

namespace Tanneryd.BulkOperations.EF6.NET48.Tests.Models.EF.ClusteredIndexSort
{
    /// <summary>
    /// Entity whose clustered index includes a rowversion column that is kept
    /// out of ColumnMappingByColumnName (concurrency token only).
    /// </summary>
    public class SortProbe
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public byte[] RowVersion { get; set; }
    }

    /// <summary>
    /// Complex type whose City column can appear in a clustered index. Sort
    /// resolves EntityProperty.Name ("City"), which is not a root CLR property.
    /// </summary>
    public class SortLocation
    {
        public string City { get; set; }
    }

    public class ComplexSortProbe
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public SortLocation Location { get; set; }
    }

    public class ClusteredIndexSortContext : DbContext
    {
        public const string ConnectionString =
            @"data source=(localdb)\MSSQLLocalDB;initial catalog=Tanneryd.BulkOperations.EF6.NET48.Tests.ClusteredIndexSort;persist security info=True;Integrated Security=SSPI;MultipleActiveResultSets=True;TrustServerCertificate=true";

        public ClusteredIndexSortContext()
            : base(ConnectionString)
        {
        }

        public DbSet<SortProbe> SortProbes { get; set; }
        public DbSet<ComplexSortProbe> ComplexSortProbes { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Configurations.Add(new SortProbeConfiguration());
            modelBuilder.Configurations.Add(new ComplexSortProbeConfiguration());
            modelBuilder.ComplexType<SortLocation>()
                .Property(p => p.City)
                .HasColumnName("City")
                .HasMaxLength(100)
                .IsRequired();
            base.OnModelCreating(modelBuilder);
        }
    }

    public class SortProbeConfiguration : EntityTypeConfiguration<SortProbe>
    {
        public SortProbeConfiguration()
        {
            ToTable("SortProbe");
            HasKey(e => e.Id);
            Property(e => e.Id).HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            Property(e => e.Name).HasMaxLength(100).IsRequired();
            Property(e => e.RowVersion)
                .IsConcurrencyToken()
                .IsRequired()
                .IsFixedLength()
                .HasMaxLength(8)
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Computed);
        }
    }

    public class ComplexSortProbeConfiguration : EntityTypeConfiguration<ComplexSortProbe>
    {
        public ComplexSortProbeConfiguration()
        {
            ToTable("ComplexSortProbe");
            HasKey(e => e.Id);
            Property(e => e.Id).HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            Property(e => e.Name).HasMaxLength(100).IsRequired();
        }
    }
}
