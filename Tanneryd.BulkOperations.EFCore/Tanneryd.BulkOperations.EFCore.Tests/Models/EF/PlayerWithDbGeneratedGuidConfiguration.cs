// <auto-generated>
// ReSharper disable All

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Tanneryd.BulkOperations.EFCore.Tests
{
    // PlayerWithDbGeneratedGuid
    public class PlayerWithDbGeneratedGuidConfiguration : IEntityTypeConfiguration<PlayerWithDbGeneratedGuid>
    {
        public void Configure(EntityTypeBuilder<PlayerWithDbGeneratedGuid> builder)
        {
            builder.ToTable("PlayerWithDbGeneratedGuid", "dbo");
            builder.HasKey(x => x.Id).HasName("PK_dbo.PlayerWithDbGeneratedGuid").IsClustered();

            builder.Property(x => x.Id).HasColumnName(@"Id").HasColumnType("uniqueidentifier").IsRequired().ValueGeneratedOnAdd();
            builder.Property(x => x.Firstname).HasColumnName(@"Firstname").HasColumnType("nvarchar(max)").IsRequired();
            builder.Property(x => x.Lastname).HasColumnName(@"Lastname").HasColumnType("nvarchar(max)").IsRequired();
            builder.Property(x => x.TeamId).HasColumnName(@"TeamId").HasColumnType("uniqueidentifier").IsRequired();

            // Foreign keys
            builder.HasOne(a => a.TeamWithDbGeneratedGuid).WithMany(b => b.PlayerWithDbGeneratedGuids).HasForeignKey(c => c.TeamId).HasConstraintName("FK_dbo.PlayerWithDbGeneratedGuid_dbo.TeamWithDbGeneratedGuid_TeamId");

            builder.HasIndex(x => x.TeamId).HasName("IX_TeamId");
        }
    }

}
// </auto-generated>
