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

namespace Tanneryd.BulkOperations.EFCore.Tests.Models.EF.CompositeManyToMany
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
            @"data source=(localdb)\MSSQLLocalDB;initial catalog=Tanneryd.BulkOperations.EFCore.Tests.CompositeManyToMany;persist security info=True;Integrated Security=SSPI;MultipleActiveResultSets=True;TrustServerCertificate=true";

        public CompositeManyToManyContext(DbContextOptions<CompositeManyToManyContext> options)
            : base(options)
        {
        }

        public DbSet<TenantItem> TenantItems { get; set; }
        public DbSet<ItemLabel> ItemLabels { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TenantItem>(e =>
            {
                e.ToTable("TenantItem");
                e.HasKey(x => new { x.TenantId, x.ItemId });
                e.Property(x => x.TenantId).ValueGeneratedNever();
                e.Property(x => x.ItemId).ValueGeneratedNever();
                e.Property(x => x.Name).IsRequired(false);

                e.HasMany(x => x.Labels)
                    .WithMany(x => x.Items)
                    .UsingEntity<Dictionary<string, object>>(
                        "TenantItemLabels",
                        r => r.HasOne<ItemLabel>().WithMany().HasForeignKey("LabelId"),
                        l => l.HasOne<TenantItem>().WithMany().HasForeignKey("TenantId", "ItemId"),
                        j =>
                        {
                            j.ToTable("TenantItemLabels");
                            j.HasKey("TenantId", "ItemId", "LabelId");
                        });
            });

            modelBuilder.Entity<ItemLabel>(e =>
            {
                e.ToTable("ItemLabel");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).UseIdentityColumn();
                e.Property(x => x.Text).IsRequired(false);
            });
        }
    }
}
