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

namespace Tanneryd.BulkOperations.EF6.NET48.Tests.Models.EF.CompositeNavDot
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
            @"data source=(localdb)\MSSQLLocalDB;initial catalog=Tanneryd.BulkOperations.EF6.NET48.Tests.CompositeNavDot;persist security info=True;Integrated Security=SSPI;MultipleActiveResultSets=True;TrustServerCertificate=true";

        public CompositeNavDotContext()
            : base(ConnectionString)
        {
        }

        public DbSet<CompositeNavParent> Parents { get; set; }
        public DbSet<CompositeNavChild> Children { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Configurations.Add(new CompositeNavParentConfiguration());
            modelBuilder.Configurations.Add(new CompositeNavChildConfiguration());
            base.OnModelCreating(modelBuilder);
        }
    }

    public class CompositeNavParentConfiguration : EntityTypeConfiguration<CompositeNavParent>
    {
        public CompositeNavParentConfiguration()
        {
            ToTable("CompositeNavParent");
            HasKey(e => new { e.TenantId, e.LocalId });
            Property(e => e.Code).HasMaxLength(50).IsRequired();
        }
    }

    public class CompositeNavChildConfiguration : EntityTypeConfiguration<CompositeNavChild>
    {
        public CompositeNavChildConfiguration()
        {
            ToTable("CompositeNavChild");
            HasKey(e => e.Id);
            Property(e => e.Id).HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            Property(e => e.Label).HasMaxLength(50).IsRequired();
            HasRequired(e => e.Parent)
                .WithMany(p => p.Children)
                .HasForeignKey(e => new { e.ParentTenantId, e.ParentLocalId });
        }
    }
}
