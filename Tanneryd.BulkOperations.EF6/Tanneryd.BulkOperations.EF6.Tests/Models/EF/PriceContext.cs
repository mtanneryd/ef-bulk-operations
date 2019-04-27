using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity;
using Tanneryd.BulkOperations.EF6.Tests.Models.DM.Prices;

namespace Tanneryd.BulkOperations.EF6.Tests.EF
{
    public class PriceContext : DbContext
    {
        public DbSet<Price> Prices { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Price>()
                .ToTable("Price")
                .HasKey(u => u.Id);
            modelBuilder.Entity<Price>()
                .Property(u => u.Id)
                .HasColumnName("Id")
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            modelBuilder.Entity<Price>()
                .Property(u => u.Date)
                .IsRequired();
            modelBuilder.Entity<Price>()
                .Property(u => u.Name)
                .IsRequired();
            modelBuilder.Entity<Price>()
                .Property(u => u.Value)
                .IsOptional();
        }
    }
}