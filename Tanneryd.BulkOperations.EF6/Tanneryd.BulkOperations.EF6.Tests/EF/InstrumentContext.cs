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