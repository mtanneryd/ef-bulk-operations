using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Tanneryd.BulkOperations.EFCore.Tests
{
    public class Level1Configuration : IEntityTypeConfiguration<Level1>
    {
        public void Configure(EntityTypeBuilder<Level1> builder)
        {
            builder.ToTable("Level1", "dbo");
            builder.HasKey(x => x.Id).HasName("PK_dbo.Level1").IsClustered();
            builder.Property(x => x.Id).HasColumnName(@"Id").HasColumnType("int").IsRequired().ValueGeneratedOnAdd().UseIdentityColumn();
            builder.Property(x => x.Level1Name).HasColumnName(@"Level1Name").HasColumnType("nvarchar(max)").IsRequired(false);

            builder.ComplexProperty(x => x.Level2, level2 =>
            {
                level2.IsRequired();
                level2.Property(l => l.Level2Name).HasColumnName("Level2Name").IsRequired();
                level2.ComplexProperty(l => l.Level3, level3 =>
                {
                    level3.IsRequired();
                    level3.Property(l => l.Level3Name).HasColumnName("Level3Name").IsRequired();
                    level3.Property(l => l.Updated).HasColumnName("Level2_Level3_Updated").IsRequired();
                });
            });
        }
    }
}
