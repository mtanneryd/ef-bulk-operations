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
using Tanneryd.BulkOperations.EF6.Tests.DM.Companies;
using Tanneryd.BulkOperations.EF6.Tests.DM.Instruments;

namespace Tanneryd.BulkOperations.EF6.Tests.EF
{
    public class InstrumentContext : DbContext
    {
        public DbSet<Instrument> Instruments { get; set; }
        public DbSet<Currency> Currencies { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Instrument>()
                .ToTable("Instrument")
                .HasKey(p => p.Key);
            modelBuilder.Entity<Instrument>()
                .Property(p => p.Key)
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            modelBuilder.Entity<Instrument>()
                .Property(p => p.Name)
                .IsRequired();

            modelBuilder.Entity<Currency>()
                .ToTable("Currency")
                .HasKey(p => p.Key);
            modelBuilder.Entity<Currency>()
                .Property(p => p.Key)
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            modelBuilder.Entity<Currency>()
                .Property(p => p.Id)
                .IsRequired();
            modelBuilder.Entity<Currency>()
                .HasMany(e => e.Instruments)
                .WithRequired(e => e.Currency)
                .HasForeignKey(e => e.CurrencyKey);
        }
    }
}