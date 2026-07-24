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

namespace Tanneryd.BulkOperations.EF6.NET48.Tests.Models.EF.StringStoreGeneratedPk
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
            @"data source=(localdb)\MSSQLLocalDB;initial catalog=Tanneryd.BulkOperations.EF6.NET48.Tests.StringStoreGeneratedPk;persist security info=True;Integrated Security=SSPI;MultipleActiveResultSets=True;TrustServerCertificate=true";

        public StringStoreGeneratedPkContext()
            : base(ConnectionString)
        {
        }

        public DbSet<CatalogItem> CatalogItems { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Configurations.Add(new CatalogItemConfiguration());
            base.OnModelCreating(modelBuilder);
        }
    }

    public class CatalogItemConfiguration : EntityTypeConfiguration<CatalogItem>
    {
        public CatalogItemConfiguration()
        {
            ToTable("CatalogItem");
            HasKey(e => e.Code);
            Property(e => e.Code)
                .HasMaxLength(36)
                .IsRequired()
                // Identity in the model so the column stays in ColumnMapping*
                // and IsPrimaryKeyStoreGenerated is true. SQL Server cannot use
                // IDENTITY on nvarchar; Initialize adds a DEFAULT instead.
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            Property(e => e.Name).IsOptional();
        }
    }
}
