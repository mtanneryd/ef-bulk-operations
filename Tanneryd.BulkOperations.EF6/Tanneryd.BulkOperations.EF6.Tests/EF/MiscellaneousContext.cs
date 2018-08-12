using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity;
using Tanneryd.BulkOperations.EF6.Tests.DM.Miscellaneous;

namespace Tanneryd.BulkOperations.EF6.Tests.EF
{
    public class MiscellaneousContext : DbContext
    {
        public DbSet<ReservedSqlKeyword> ReservedSqlKeywords { get; set; }
        public DbSet<Coordinate> Coordinates { get; set; }
        public DbSet<Point> Points { get; set; }


        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ReservedSqlKeyword>()
                .ToTable("ReservedSqlKeyword")
                .HasKey(p => p.Id);
            modelBuilder.Entity<ReservedSqlKeyword>()
                .Property(p => p.Id)
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            modelBuilder.Entity<ReservedSqlKeyword>()
                .Property(p => p.Key)
                .IsOptional();
            modelBuilder.Entity<ReservedSqlKeyword>()
                .Property(p => p.Select)
                .IsOptional();
            modelBuilder.Entity<ReservedSqlKeyword>()
                .Property(p => p.Identity)
                .IsOptional();

            modelBuilder.Entity<Coordinate>()
                .ToTable("Coordinate")
                .HasKey(p => p.Id);
            modelBuilder.Entity<Coordinate>()
                .Property(p => p.Id)
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            modelBuilder.Entity<Coordinate>()
                .Property(p => p.Value)
                .IsRequired();
            modelBuilder.Entity<Coordinate>()
                .HasMany(e => e.XCoordinatePoints)
                .WithRequired(e => e.XCoordinate)
                .HasForeignKey(e => e.XCoordinateId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<Coordinate>()
                .HasMany(e => e.YCoordinatePoints)
                .WithRequired(e => e.YCoordinate)
                .HasForeignKey(e => e.YCoordinateId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Point>()
                .ToTable("Point")
                .HasKey(p => new { p.XCoordinateId, p.YCoordinateId});
            modelBuilder.Entity<Point>()
                .Property(p => p.XCoordinateId)
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.None)
                .HasColumnOrder(0);
            modelBuilder.Entity<Point>()
                .Property(p => p.YCoordinateId)
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.None)
                .HasColumnOrder(1);
            modelBuilder.Entity<Point>()
                .Property(e => e.Value)
                .IsRequired();
        }
    }
}