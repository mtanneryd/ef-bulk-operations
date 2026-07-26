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

namespace Tanneryd.BulkOperations.EFCore.Tests.Models.EF.OwnedType
{
    public class OwnedAddress
    {
        public string Street { get; set; }
        public string City { get; set; }
    }

    public class OwnedOrderLine
    {
        public string Sku { get; set; }
        public int Quantity { get; set; }
    }

    public class OwnedCustomer
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public OwnedAddress Address { get; set; }
        public ICollection<OwnedOrderLine> Lines { get; set; } = new List<OwnedOrderLine>();
    }

    public class OwnedTypeContext : DbContext
    {
        public const string ConnectionString =
            @"data source=(localdb)\MSSQLLocalDB;initial catalog=Tanneryd.BulkOperations.EFCore.Tests.OwnedType;persist security info=True;Integrated Security=SSPI;MultipleActiveResultSets=True;TrustServerCertificate=true";

        public OwnedTypeContext(DbContextOptions<OwnedTypeContext> options)
            : base(options)
        {
        }

        public DbSet<OwnedCustomer> Customers { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OwnedCustomer>(e =>
            {
                e.ToTable("OwnedCustomer");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).UseIdentityColumn();
                e.Property(x => x.Name).HasMaxLength(100).IsRequired();
                e.OwnsOne(x => x.Address, address =>
                {
                    address.Property(a => a.Street).HasColumnName("Street").HasMaxLength(200);
                    address.Property(a => a.City).HasColumnName("City").HasMaxLength(100);
                });
                e.OwnsMany(x => x.Lines, lines =>
                {
                    lines.ToTable("OwnedOrderLine");
                    lines.WithOwner().HasForeignKey("CustomerId");
                    lines.Property<int>("Id");
                    lines.HasKey("Id");
                    lines.Property(l => l.Sku).HasMaxLength(50).IsRequired();
                });
            });
        }
    }
}
