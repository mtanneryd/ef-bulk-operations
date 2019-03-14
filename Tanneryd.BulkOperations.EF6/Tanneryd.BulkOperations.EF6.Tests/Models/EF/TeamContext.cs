using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity;
using Tanneryd.BulkOperations.EF6.Tests.DM.Blog;
using Tanneryd.BulkOperations.EF6.Tests.DM.Teams.UsingUserGeneratedGuidKeys;

namespace Tanneryd.BulkOperations.EF6.Tests.EF
{
        public class DbGeneratedTeamContext : DbContext
    {
        public DbSet<DM.Teams.UsingDbGeneratedGuidKeys.Team> Teams { get; set; }
        public DbSet<DM.Teams.UsingDbGeneratedGuidKeys.Player> Players { get; set; }
        public DbSet<DM.Teams.UsingDbGeneratedGuidKeys.Coach> Coaches { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DM.Teams.UsingDbGeneratedGuidKeys.Team>()
                .ToTable("Team")
                .HasKey(p => p.Id);
            modelBuilder.Entity<DM.Teams.UsingDbGeneratedGuidKeys.Team>()
                .Property(p => p.Id)
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            modelBuilder.Entity<DM.Teams.UsingDbGeneratedGuidKeys.Team>()
                .Property(p => p.Name)
                .IsRequired();
            modelBuilder.Entity<DM.Teams.UsingDbGeneratedGuidKeys.Team>()
                .HasMany(b => b.Players)
                .WithRequired(p => p.Team)
                .HasForeignKey(p => p.TeamId);

            modelBuilder.Entity<DM.Teams.UsingDbGeneratedGuidKeys.Player>()
                .ToTable("Player")
                .HasKey(p => p.Id);
            modelBuilder.Entity<DM.Teams.UsingDbGeneratedGuidKeys.Player>()
                .Property(p => p.Id)
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            modelBuilder.Entity<DM.Teams.UsingDbGeneratedGuidKeys.Player>()
                .Property(p => p.Firstname)
                .IsRequired();
            modelBuilder.Entity<DM.Teams.UsingDbGeneratedGuidKeys.Player>()
                .Property(p => p.Lastname)
                .IsRequired();
           

            modelBuilder.Entity<DM.Teams.UsingDbGeneratedGuidKeys.Coach>()
                .ToTable("Coach")
                .HasKey(p => p.Id);
            modelBuilder.Entity<DM.Teams.UsingDbGeneratedGuidKeys.Coach>()
                .Property(p => p.Id)
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            modelBuilder.Entity<DM.Teams.UsingDbGeneratedGuidKeys.Coach>()
                .Property(p => p.Firstname)
                .IsRequired();            
            modelBuilder.Entity<DM.Teams.UsingDbGeneratedGuidKeys.Coach>()
                .Property(p => p.Lastname)
                .IsRequired();    
 
            modelBuilder.Entity<DM.Teams.UsingDbGeneratedGuidKeys.Coach>()
                .HasMany(a => a.Teams)
                .WithMany()
                .Map(x =>
                {
                    x.MapLeftKey("CoachId");
                    x.MapRightKey("TeamId");
                    x.ToTable("CoachTeams");
                });
        }
    }
    public class UserGeneratedTeamContext : DbContext
    {
        public DbSet<Team> Teams { get; set; }
        public DbSet<Player> Players { get; set; }
        public DbSet<Coach> Coaches { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Team>()
                .ToTable("Team")
                .HasKey(p => p.Id);
            modelBuilder.Entity<Team>()
                .Property(p => p.Id)
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.None);
            modelBuilder.Entity<Team>()
                .Property(p => p.Name)
                .IsRequired();
            modelBuilder.Entity<Team>()
                .HasMany(b => b.Players)
                .WithRequired(p => p.Team)
                .HasForeignKey(p => p.TeamId);

            modelBuilder.Entity<Player>()
                .ToTable("Player")
                .HasKey(p => p.Id);
            modelBuilder.Entity<Player>()
                .Property(p => p.Id)
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.None);
            modelBuilder.Entity<Player>()
                .Property(p => p.Firstname)
                .IsRequired();
            modelBuilder.Entity<Player>()
                .Property(p => p.Lastname)
                .IsRequired();
           

            modelBuilder.Entity<Coach>()
                .ToTable("Coach")
                .HasKey(p => p.Id);
            modelBuilder.Entity<Coach>()
                .Property(p => p.Id)
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.None);
            modelBuilder.Entity<Coach>()
                .Property(p => p.Firstname)
                .IsRequired();            
            modelBuilder.Entity<Coach>()
                .Property(p => p.Lastname)
                .IsRequired();    
 
            modelBuilder.Entity<Coach>()
                .HasMany(a => a.Teams)
                .WithMany()
                .Map(x =>
                {
                    x.MapLeftKey("CoachId");
                    x.MapRightKey("TeamId");
                    x.ToTable("CoachTeams");
                });
        }
    }
}
