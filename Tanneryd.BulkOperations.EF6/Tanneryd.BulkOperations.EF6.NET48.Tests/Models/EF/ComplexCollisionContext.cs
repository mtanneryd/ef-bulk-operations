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

namespace Tanneryd.BulkOperations.EF6.NET48.Tests.Models.EF.ComplexCollision
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
            @"data source=(localdb)\MSSQLLocalDB;initial catalog=Tanneryd.BulkOperations.EF6.NET48.Tests.ComplexCollision;persist security info=True;Integrated Security=SSPI;MultipleActiveResultSets=True;TrustServerCertificate=true";

        public ComplexCollisionContext()
            : base(ConnectionString)
        {
        }

        public DbSet<ComplexCollisionContact> Contacts { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Configurations.Add(new ComplexCollisionContactConfiguration());
            modelBuilder.ComplexType<CollisionAddress>();
            base.OnModelCreating(modelBuilder);
        }
    }

    public class ComplexCollisionContactConfiguration : EntityTypeConfiguration<ComplexCollisionContact>
    {
        public ComplexCollisionContactConfiguration()
        {
            ToTable("ComplexCollisionContact");
            HasKey(e => e.Id);
            Property(e => e.Id).HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            Property(e => e.Name).HasMaxLength(100).IsRequired();
            Property(e => e.Home.Street).HasColumnName("Home_Street").HasMaxLength(200).IsRequired();
            Property(e => e.Home.City).HasColumnName("Home_City").HasMaxLength(100).IsRequired();
            Property(e => e.Work.Street).HasColumnName("Work_Street").HasMaxLength(200).IsRequired();
            Property(e => e.Work.City).HasColumnName("Work_City").HasMaxLength(100).IsRequired();
        }
    }
}
