// <auto-generated>

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Tanneryd.BulkOperations.EFCore.SQLite.Tests
{
    public class CoachTeamsWithDbGeneratedGuidConfiguration : IEntityTypeConfiguration<CoachTeamsWithDbGeneratedGuid>
    {
        public void Configure(EntityTypeBuilder<CoachTeamsWithDbGeneratedGuid> builder)
        {
            builder.ToTable("CoachTeamsWithDbGeneratedGuid", "main");
            builder.HasKey(x => new { x.CoachId, x.TeamId });

            builder.Property(x => x.CoachId).HasColumnName(@"CoachId").HasColumnType("uniqueidentifier").IsRequired().ValueGeneratedOnAdd();
            builder.Property(x => x.TeamId).HasColumnName(@"TeamId").HasColumnType("uniqueidentifier").IsRequired().ValueGeneratedOnAdd();
        }
    }

}
// </auto-generated>
