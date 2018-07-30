/*
* Copyright ©  2017-2018 Tånneryd IT AB
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
using Tanneryd.BulkOperations.EF6.Tests.DM;
using Tanneryd.BulkOperations.EF6.Tests.DM.Numbers;

namespace Tanneryd.BulkOperations.EF6.Tests.EF
{
    public class NumberContext : DbContext
    {
        public DbSet<Parity> Parities { get; set; }
        public DbSet<Number> Numbers { get; set; }
        public DbSet<Prime> Primes { get; set; }
        public DbSet<Composite> Composites { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Parity>()
                .ToTable("Parity")
                .HasKey(u => u.Id);
            modelBuilder.Entity<Parity>()
                .Property(u => u.Id)
                .HasColumnName("Key")
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            modelBuilder.Entity<Parity>()
                .Property(u => u.Name)
                .IsRequired();
            modelBuilder.Entity<Parity>()
                .Property(p => p.UpdatedAt)
                .HasColumnName("UpdatedAt");
            modelBuilder.Entity<Parity>()
                .Property(p => p.UpdatedBy)
                .HasColumnName("UpdatedBy");
            modelBuilder.Entity<Parity>()
                .HasMany(p => p.Numbers)
                .WithRequired(n => n.Parity)
                .HasForeignKey(n => n.ParityId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Number>()
                .ToTable("Number")
                .HasKey(u => u.Id);
            modelBuilder.Entity<Number>()
                .Property(u => u.Id)
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            modelBuilder.Entity<Number>()
                .Property(u => u.Value);
            modelBuilder.Entity<Number>()
                .Property(p => p.UpdatedAt)
                .HasColumnName("UpdatedAt");
            modelBuilder.Entity<Number>()
                .Property(p => p.UpdatedBy)
                .HasColumnName("UpdatedBy");

            modelBuilder.Entity<Prime>()
                .ToTable("Prime")
                .HasKey(u => u.NumberId);
            modelBuilder.Entity<Prime>()
                .HasRequired(t => t.Number)
                .WithOptional(t => t.Prime);
            modelBuilder.Entity<Prime>()
                .Property(p => p.UpdatedAt)
                .HasColumnName("UpdatedAt");
            modelBuilder.Entity<Prime>()
                .Property(p => p.UpdatedBy)
                .HasColumnName("UpdatedBy");

            modelBuilder.Entity<Composite>()
                .ToTable("Composite")
                .HasKey(u => u.NumberId);
            modelBuilder.Entity<Composite>()
                .Property(p => p.UpdatedAt)
                .HasColumnName("UpdatedAt");
            modelBuilder.Entity<Composite>()
                .Property(p => p.UpdatedBy)
                .HasColumnName("UpdatedBy");
            modelBuilder.Entity<Composite>()
                .HasRequired(t => t.Number)
                .WithOptional(t => t.Composite);
            modelBuilder.Entity<Composite>()
                .HasMany(e => e.Primes)
                .WithMany(e => e.Composites)
                .Map(m => m.ToTable("CompositePrime").MapLeftKey("CompositeId").MapRightKey("PrimeId"));
        }
    }
}