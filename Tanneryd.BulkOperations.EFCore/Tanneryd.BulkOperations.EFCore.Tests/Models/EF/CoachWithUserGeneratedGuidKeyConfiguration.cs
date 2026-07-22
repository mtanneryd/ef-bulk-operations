using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Tanneryd.BulkOperations.EFCore.Tests.Models.DM.Teams.UsingDbGeneratedGuidKeys;
using Tanneryd.BulkOperations.EFCore.Tests.Models.DM.Teams.UsingUserGeneratedGuidKeys;

namespace Tanneryd.BulkOperations.EFCore.Tests
{
    public class CoachWithUserGeneratedGuidKeyConfiguration : IEntityTypeConfiguration<CoachWithUserGeneratedGuidKey>
    {
        public void Configure(EntityTypeBuilder<CoachWithUserGeneratedGuidKey> builder)
        {
            builder.ToTable("CoachWithUserGeneratedGuid", "dbo");
            builder.HasKey(x => x.Id).HasName("PK_dbo.CoachWithUserGeneratedGuid").IsClustered();

            builder.Property(x => x.Id).HasColumnName(@"Id").HasColumnType("uniqueidentifier").IsRequired().ValueGeneratedNever();
            builder.Property(x => x.Firstname).HasColumnName(@"Firstname").HasColumnType("nvarchar(max)").IsRequired();
            builder.Property(x => x.Lastname).HasColumnName(@"Lastname").HasColumnType("nvarchar(max)").IsRequired();

            builder.HasMany(c => c.Teams)
                .WithMany()
                .UsingEntity<Dictionary<string, object>>(
                    "CoachTeamsWithUserGeneratedGuid",
                    r => r.HasOne<TeamWithUserGeneratedGuidKey>().WithMany().HasForeignKey("TeamId")
                        .HasConstraintName("FK_dbo.CoachTeamsWithUserGeneratedGuid_dbo.TeamWithUserGeneratedGuid_TeamId"),
                    l => l.HasOne<CoachWithUserGeneratedGuidKey>().WithMany().HasForeignKey("CoachId")
                        .HasConstraintName("FK_dbo.CoachTeamsWithUserGeneratedGuid_dbo.CoachWithUserGeneratedGuid_CoachId"),
                    j =>
                    {
                        j.HasKey("CoachId", "TeamId").HasName("PK_dbo.CoachTeamsWithUserGeneratedGuid");
                        j.ToTable("CoachTeamsWithUserGeneratedGuid", "dbo");
                        j.HasIndex("CoachId").HasDatabaseName("IX_CoachId");
                        j.HasIndex("TeamId").HasDatabaseName("IX_TeamId");
                    });
        }
    }
}
