using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Tanneryd.BulkOperations.EFCore.Tests.Models.DM.Teams.UsingDbGeneratedGuidKeys;
using Tanneryd.BulkOperations.EFCore.Tests.Models.DM.Teams.UsingUserGeneratedGuidKeys;

namespace Tanneryd.BulkOperations.EFCore.Tests
{
    public class PlayerWithDbGeneratedGuidKeyConfiguration : IEntityTypeConfiguration<PlayerWithDbGeneratedGuidKey>
    {
        public void Configure(EntityTypeBuilder<PlayerWithDbGeneratedGuidKey> builder)
        {
            builder.ToTable("PlayerWithDbGeneratedGuid", "dbo");
            builder.HasKey(x => x.Id).HasName("PK_dbo.PlayerWithDbGeneratedGuid").IsClustered();

            builder.Property(x => x.Id).HasColumnName(@"Id").HasColumnType("uniqueidentifier").IsRequired().ValueGeneratedOnAdd();
            builder.Property(x => x.Firstname).HasColumnName(@"Firstname").HasColumnType("nvarchar(max)").IsRequired();
            builder.Property(x => x.Lastname).HasColumnName(@"Lastname").HasColumnType("nvarchar(max)").IsRequired();
            builder.Property(x => x.TeamId).HasColumnName(@"TeamId").HasColumnType("uniqueidentifier").IsRequired();

            builder.HasOne(a => a.Team).WithMany(b => b.Players).HasForeignKey(c => c.TeamId)
                .HasConstraintName("FK_dbo.PlayerWithDbGeneratedGuid_dbo.TeamWithDbGeneratedGuid_TeamId");

            builder.HasIndex(x => x.TeamId).HasDatabaseName("IX_TeamId");
        }
    }
}
