using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Tanneryd.BulkOperations.EFCore.Tests.Models.DM.Teams.UsingDbGeneratedGuidKeys;
using Tanneryd.BulkOperations.EFCore.Tests.Models.DM.Teams.UsingUserGeneratedGuidKeys;

namespace Tanneryd.BulkOperations.EFCore.Tests
{
    public class TeamWithDbGeneratedGuidKeyConfiguration : IEntityTypeConfiguration<TeamWithDbGeneratedGuidKey>
    {
        public void Configure(EntityTypeBuilder<TeamWithDbGeneratedGuidKey> builder)
        {
            builder.ToTable("TeamWithDbGeneratedGuid", "dbo");
            builder.HasKey(x => x.Id).HasName("PK_dbo.TeamWithDbGeneratedGuid").IsClustered();

            builder.Property(x => x.Id).HasColumnName(@"Id").HasColumnType("uniqueidentifier").IsRequired().ValueGeneratedOnAdd();
            builder.Property(x => x.Name).HasColumnName(@"Name").HasColumnType("nvarchar(max)").IsRequired();
        }
    }
}
