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

namespace Tanneryd.BulkOperations.EFCore.Tests.Models.EF.ComplexCollision
{
    public class CollisionAddress
    {
        public string Street { get; set; }
        public string City { get; set; }
    }

    /// <summary>
    /// Two complex properties share leaf CLR names (Street/City) but map to
    /// distinct store columns — exercises flatten/mapping key uniqueness.
    /// </summary>
    public class ComplexCollisionContact
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public CollisionAddress Home { get; set; }
        public CollisionAddress Work { get; set; }
    }

    public class ComplexCollisionContext : DbContext
    {
        public const string ConnectionString =
            @"data source=(localdb)\MSSQLLocalDB;initial catalog=Tanneryd.BulkOperations.EFCore.Tests.ComplexCollision;persist security info=True;Integrated Security=SSPI;MultipleActiveResultSets=True;TrustServerCertificate=true";

        public ComplexCollisionContext(DbContextOptions<ComplexCollisionContext> options)
            : base(options)
        {
        }

        public DbSet<ComplexCollisionContact> Contacts { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ComplexCollisionContact>(e =>
            {
                e.ToTable("ComplexCollisionContact");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).UseIdentityColumn();
                e.Property(x => x.Name).HasMaxLength(100).IsRequired();
                e.ComplexProperty(x => x.Home, home =>
                {
                    home.IsRequired();
                    home.Property(a => a.Street).HasColumnName("Home_Street").HasMaxLength(200).IsRequired();
                    home.Property(a => a.City).HasColumnName("Home_City").HasMaxLength(100).IsRequired();
                });
                e.ComplexProperty(x => x.Work, work =>
                {
                    work.IsRequired();
                    work.Property(a => a.Street).HasColumnName("Work_Street").HasMaxLength(200).IsRequired();
                    work.Property(a => a.City).HasColumnName("Work_City").HasMaxLength(100).IsRequired();
                });
            });
        }
    }
}
