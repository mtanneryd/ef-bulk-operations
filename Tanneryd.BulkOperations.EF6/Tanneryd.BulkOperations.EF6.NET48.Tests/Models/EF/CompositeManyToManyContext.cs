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
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity;
using System.Data.Entity.ModelConfiguration;

namespace Tanneryd.BulkOperations.EF6.NET48.Tests.Models.EF.CompositeManyToMany
{
    /// <summary>
    /// Entity with a composite PK participating in a many-to-many. The join
    /// table must carry both TenantId and ItemId for the left end.
    /// </summary>
    public class TenantItem
    {
        public TenantItem()
        {
            Labels = new HashSet<ItemLabel>();
        }

        public int TenantId { get; set; }
        public int ItemId { get; set; }
        public string Name { get; set; }
        public ICollection<ItemLabel> Labels { get; set; }
    }

    public class ItemLabel
    {
        public ItemLabel()
        {
            Items = new HashSet<TenantItem>();
        }

        public int Id { get; set; }
        public string Text { get; set; }
        public ICollection<TenantItem> Items { get; set; }
    }

    public class CompositeManyToManyContext : DbContext
    {
        public const string ConnectionString =
            @"data source=(localdb)\MSSQLLocalDB;initial catalog=Tanneryd.BulkOperations.EF6.NET48.Tests.CompositeManyToMany;persist security info=True;Integrated Security=SSPI;MultipleActiveResultSets=True;TrustServerCertificate=true";

        public CompositeManyToManyContext()
            : base(ConnectionString)
        {
        }

        public DbSet<TenantItem> TenantItems { get; set; }
        public DbSet<ItemLabel> ItemLabels { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Configurations.Add(new TenantItemConfiguration());
            modelBuilder.Configurations.Add(new ItemLabelConfiguration());
            base.OnModelCreating(modelBuilder);
        }
    }

    public class TenantItemConfiguration : EntityTypeConfiguration<TenantItem>
    {
        public TenantItemConfiguration()
        {
            ToTable("TenantItem");
            HasKey(e => new { e.TenantId, e.ItemId });
            Property(e => e.TenantId).HasDatabaseGeneratedOption(DatabaseGeneratedOption.None);
            Property(e => e.ItemId).HasDatabaseGeneratedOption(DatabaseGeneratedOption.None);
            Property(e => e.Name).IsOptional();

            HasMany(e => e.Labels)
                .WithMany(e => e.Items)
                .Map(m =>
                {
                    m.ToTable("TenantItemLabels");
                    m.MapLeftKey("TenantId", "ItemId");
                    m.MapRightKey("LabelId");
                });
        }
    }

    public class ItemLabelConfiguration : EntityTypeConfiguration<ItemLabel>
    {
        public ItemLabelConfiguration()
        {
            ToTable("ItemLabel");
            HasKey(e => e.Id);
            Property(e => e.Id).HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            Property(e => e.Text).IsOptional();
        }
    }
}
