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

namespace Tanneryd.BulkOperations.EFCore.Tests.Models.EF.ComplexTph
{
    public class ComplexTphLocation
    {
        public string City { get; set; }
    }

    /// <summary>
    /// TPH base with a complex property so insert flattens to ExpandoObject
    /// while still requiring a discriminator column on the bulk-copy reader.
    /// </summary>
    public abstract class ComplexTphItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public ComplexTphLocation Location { get; set; }
    }

    public class ComplexTphRed : ComplexTphItem
    {
    }

    public class ComplexTphBlue : ComplexTphItem
    {
    }

    public class ComplexTphContext : DbContext
    {
        public const string ConnectionString =
            @"data source=(localdb)\MSSQLLocalDB;initial catalog=Tanneryd.BulkOperations.EFCore.Tests.ComplexTph;persist security info=True;Integrated Security=SSPI;MultipleActiveResultSets=True;TrustServerCertificate=true";

        public ComplexTphContext(DbContextOptions<ComplexTphContext> options)
            : base(options)
        {
        }

        public DbSet<ComplexTphItem> Items { get; set; }
        public DbSet<ComplexTphRed> Reds { get; set; }
        public DbSet<ComplexTphBlue> Blues { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ComplexTphItem>(e =>
            {
                e.ToTable("ComplexTphItem");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).UseIdentityColumn();
                e.Property(x => x.Name).HasMaxLength(100).IsRequired();
                e.ComplexProperty(x => x.Location, location =>
                {
                    location.IsRequired();
                    location.Property(l => l.City).HasColumnName("City").HasMaxLength(100).IsRequired();
                });
                e.HasDiscriminator<string>("ItemType")
                    .HasValue<ComplexTphRed>("Red")
                    .HasValue<ComplexTphBlue>("Blue");
            });
        }
    }
}
