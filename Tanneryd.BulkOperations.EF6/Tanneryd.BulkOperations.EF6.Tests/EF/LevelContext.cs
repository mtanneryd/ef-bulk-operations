using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity;
using Tanneryd.BulkOperations.EF6.Tests.DM.Levels;

namespace Tanneryd.BulkOperations.EF6.Tests.EF
{
    public class LevelContext : DbContext
    {
        public DbSet<Level1> Levels { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Level1>()
                .ToTable("Level1")
                .HasKey(u => u.Id);
            modelBuilder.Entity<Level1>()
                .Property(u => u.Id)
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            modelBuilder.Entity<Level1>()
                .Property(p => p.Level1Name)
                .HasColumnName("Level1Name");

            modelBuilder.ComplexType<Level2>()
                .Property(p => p.Level2Name)
                .HasColumnName("Level2Name");

            modelBuilder.ComplexType<Level3>()
                .Property(p => p.Level3Name)
                .HasColumnName("Level3Name");
        }
    }
}