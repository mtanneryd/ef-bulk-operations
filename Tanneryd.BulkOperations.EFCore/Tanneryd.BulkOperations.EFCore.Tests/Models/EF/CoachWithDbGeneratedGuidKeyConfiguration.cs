using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Tanneryd.BulkOperations.EFCore.Tests.Models.DM.Teams.UsingDbGeneratedGuidKeys;
using Tanneryd.BulkOperations.EFCore.Tests.Models.DM.Teams.UsingUserGeneratedGuidKeys;

namespace Tanneryd.BulkOperations.EFCore.Tests
{
    public class CoachWithDbGeneratedGuidKeyConfiguration : IEntityTypeConfiguration<CoachWithDbGeneratedGuidKey>
    {
        public void Configure(EntityTypeBuilder<CoachWithDbGeneratedGuidKey> builder)
        {
            builder.ToTable("CoachWithDbGeneratedGuid", "dbo");
            builder.HasKey(x => x.Id).HasName("PK_dbo.CoachWithDbGeneratedGuid").IsClustered();

            builder.Property(x => x.Id).HasColumnName(@"Id").HasColumnType("uniqueidentifier").IsRequired().ValueGeneratedOnAdd();
            builder.Property(x => x.Firstname).HasColumnName(@"Firstname").HasColumnType("nvarchar(max)").IsRequired();
            builder.Property(x => x.Lastname).HasColumnName(@"Lastname").HasColumnType("nvarchar(max)").IsRequired();

            builder.HasMany(c => c.Teams)
                .WithMany()
                .UsingEntity<Dictionary<string, object>>(
                    "CoachTeamsWithDbGeneratedGuid",
                    r => r.HasOne<TeamWithDbGeneratedGuidKey>().WithMany().HasForeignKey("TeamId")
                        .HasConstraintName("FK_dbo.CoachTeamsWithDbGeneratedGuid_dbo.TeamWithDbGeneratedGuid_TeamId"),
                    l => l.HasOne<CoachWithDbGeneratedGuidKey>().WithMany().HasForeignKey("CoachId")
                        .HasConstraintName("FK_dbo.CoachTeamsWithDbGeneratedGuid_dbo.CoachWithDbGeneratedGuid_CoachId"),
                    j =>
                    {
                        j.HasKey("CoachId", "TeamId").HasName("PK_dbo.CoachTeamsWithDbGeneratedGuid");
                        j.ToTable("CoachTeamsWithDbGeneratedGuid", "dbo");
                        j.HasIndex("CoachId").HasDatabaseName("IX_CoachId");
                        j.HasIndex("TeamId").HasDatabaseName("IX_TeamId");
                    });
        }
    }
}
