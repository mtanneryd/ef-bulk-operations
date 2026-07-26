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

namespace Tanneryd.BulkOperations.EFCore.Tests.Models.EF.CompositeNavDot
{
    public class CompositeNavParent
    {
        public int TenantId { get; set; }
        public int LocalId { get; set; }
        public string Code { get; set; }
        public ICollection<CompositeNavChild> Children { get; set; } = new List<CompositeNavChild>();
    }

    public class CompositeNavChild
    {
        public int Id { get; set; }
        public string Label { get; set; }
        public int ParentTenantId { get; set; }
        public int ParentLocalId { get; set; }
        public CompositeNavParent Parent { get; set; }
    }

    /// <summary>
    /// Composite FK parent/child for nav-dot SelectExisting. Sharing one FK
    /// column across parents exposes single-pair join false positives.
    /// </summary>
    public class CompositeNavDotContext : DbContext
    {
        public const string ConnectionString =
            @"data source=(localdb)\MSSQLLocalDB;initial catalog=Tanneryd.BulkOperations.EFCore.Tests.CompositeNavDot;persist security info=True;Integrated Security=SSPI;MultipleActiveResultSets=True;TrustServerCertificate=true";

        public CompositeNavDotContext(DbContextOptions<CompositeNavDotContext> options)
            : base(options)
        {
        }

        public DbSet<CompositeNavParent> Parents { get; set; }
        public DbSet<CompositeNavChild> Children { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CompositeNavParent>(e =>
            {
                e.ToTable("CompositeNavParent");
                e.HasKey(x => new { x.TenantId, x.LocalId });
                e.Property(x => x.Code).HasMaxLength(50).IsRequired();
            });

            modelBuilder.Entity<CompositeNavChild>(e =>
            {
                e.ToTable("CompositeNavChild");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).UseIdentityColumn();
                e.Property(x => x.Label).HasMaxLength(50).IsRequired();
                e.HasOne(x => x.Parent)
                    .WithMany(p => p.Children)
                    .HasForeignKey(x => new { x.ParentTenantId, x.ParentLocalId })
                    .HasPrincipalKey(p => new { p.TenantId, p.LocalId })
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
