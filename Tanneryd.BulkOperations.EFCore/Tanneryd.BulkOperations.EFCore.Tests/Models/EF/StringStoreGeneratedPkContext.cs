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

namespace Tanneryd.BulkOperations.EFCore.Tests.Models.EF.StringStoreGeneratedPk
{
    /// <summary>
    /// String PK with a store DEFAULT so BulkInsert takes the
    /// IsPrimaryKeyStoreGenerated branch (SelectNewEntities).
    /// </summary>
    public class CatalogItem
    {
        public string Code { get; set; }
        public string Name { get; set; }
    }

    public class StringStoreGeneratedPkContext : DbContext
    {
        public const string ConnectionString =
            @"data source=(localdb)\MSSQLLocalDB;initial catalog=Tanneryd.BulkOperations.EFCore.Tests.StringStoreGeneratedPk;persist security info=True;Integrated Security=SSPI;MultipleActiveResultSets=True;TrustServerCertificate=true";

        public StringStoreGeneratedPkContext(DbContextOptions<StringStoreGeneratedPkContext> options)
            : base(options)
        {
        }

        public DbSet<CatalogItem> CatalogItems { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CatalogItem>(e =>
            {
                e.ToTable("CatalogItem");
                e.HasKey(x => x.Code);
                e.Property(x => x.Code)
                    .HasMaxLength(36)
                    .HasDefaultValueSql("CONVERT(nvarchar(36), NEWID())")
                    .ValueGeneratedOnAdd();
                e.Property(x => x.Name).IsRequired(false);
            });
        }
    }
}
