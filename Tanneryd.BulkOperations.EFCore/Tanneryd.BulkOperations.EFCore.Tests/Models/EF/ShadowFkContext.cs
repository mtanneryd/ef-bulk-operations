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

using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace Tanneryd.BulkOperations.EFCore.Tests.Models.EF.ShadowFk
{
    public class ShadowAuthor
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public ICollection<ShadowBook> Books { get; set; } = new List<ShadowBook>();
    }

    /// <summary>
    /// Dependent with navigation only — AuthorId is a shadow FK property.
    /// </summary>
    public class ShadowBook
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public ShadowAuthor Author { get; set; }
    }

    public class ShadowFkContext : DbContext
    {
        public const string ConnectionString =
            @"data source=(localdb)\MSSQLLocalDB;initial catalog=Tanneryd.BulkOperations.EFCore.Tests.ShadowFk;persist security info=True;Integrated Security=SSPI;MultipleActiveResultSets=True;TrustServerCertificate=true";

        public ShadowFkContext(DbContextOptions<ShadowFkContext> options)
            : base(options)
        {
        }

        public DbSet<ShadowAuthor> Authors { get; set; }
        public DbSet<ShadowBook> Books { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ShadowAuthor>(e =>
            {
                e.ToTable("ShadowAuthor");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).UseIdentityColumn();
                e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            });

            modelBuilder.Entity<ShadowBook>(e =>
            {
                e.ToTable("ShadowBook");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).UseIdentityColumn();
                e.Property(x => x.Title).HasMaxLength(200).IsRequired();
                e.HasOne(x => x.Author)
                    .WithMany(a => a.Books)
                    .HasForeignKey("AuthorId")
                    .IsRequired();
            });
        }
    }
}
