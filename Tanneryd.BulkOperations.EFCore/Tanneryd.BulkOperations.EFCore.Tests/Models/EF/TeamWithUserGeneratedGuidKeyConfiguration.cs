using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Tanneryd.BulkOperations.EFCore.Tests.Models.DM.Teams.UsingDbGeneratedGuidKeys;
using Tanneryd.BulkOperations.EFCore.Tests.Models.DM.Teams.UsingUserGeneratedGuidKeys;

namespace Tanneryd.BulkOperations.EFCore.Tests
{
    public class TeamWithUserGeneratedGuidKeyConfiguration : IEntityTypeConfiguration<TeamWithUserGeneratedGuidKey>
    {
        public void Configure(EntityTypeBuilder<TeamWithUserGeneratedGuidKey> builder)
        {
            builder.ToTable("TeamWithUserGeneratedGuid", "dbo");
            builder.HasKey(x => x.Id).HasName("PK_dbo.TeamWithUserGeneratedGuid").IsClustered();

            builder.Property(x => x.Id).HasColumnName(@"Id").HasColumnType("uniqueidentifier").IsRequired().ValueGeneratedNever();
            builder.Property(x => x.Name).HasColumnName(@"Name").HasColumnType("nvarchar(max)").IsRequired();
        }
    }
}
