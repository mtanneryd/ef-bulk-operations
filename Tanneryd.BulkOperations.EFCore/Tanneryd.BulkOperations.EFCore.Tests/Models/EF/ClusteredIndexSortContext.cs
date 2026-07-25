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

using Microsoft.EntityFrameworkCore;

namespace Tanneryd.BulkOperations.EFCore.Tests.Models.EF.ClusteredIndexSort
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
            @"data source=(localdb)\MSSQLLocalDB;initial catalog=Tanneryd.BulkOperations.EFCore.Tests.ClusteredIndexSort;persist security info=True;Integrated Security=SSPI;MultipleActiveResultSets=True;TrustServerCertificate=true";

        public ClusteredIndexSortContext(DbContextOptions<ClusteredIndexSortContext> options)
            : base(options)
        {
        }

        public DbSet<SortProbe> SortProbes { get; set; }
        public DbSet<ComplexSortProbe> ComplexSortProbes { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SortProbe>(e =>
            {
                e.ToTable("SortProbe");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).UseIdentityColumn();
                e.Property(x => x.Name).HasMaxLength(100).IsRequired();
                e.Property(x => x.RowVersion).IsRowVersion();
            });

            modelBuilder.Entity<ComplexSortProbe>(e =>
            {
                e.ToTable("ComplexSortProbe");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).UseIdentityColumn();
                e.Property(x => x.Name).HasMaxLength(100).IsRequired();
                e.ComplexProperty(x => x.Location, location =>
                {
                    location.IsRequired();
                    location.Property(l => l.City).HasColumnName("City").HasMaxLength(100).IsRequired();
                });
            });
        }
    }
}
