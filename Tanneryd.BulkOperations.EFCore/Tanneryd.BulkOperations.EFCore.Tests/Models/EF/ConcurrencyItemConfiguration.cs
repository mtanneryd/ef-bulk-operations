using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tanneryd.BulkOperations.TestModels;

namespace Tanneryd.BulkOperations.EFCore.Tests
{
    public class ConcurrencyItemConfiguration : IEntityTypeConfiguration<ConcurrencyItem>
    {
        public void Configure(EntityTypeBuilder<ConcurrencyItem> builder)
        {
            builder.ToTable("ConcurrencyItem", "dbo");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasColumnName("Id").ValueGeneratedOnAdd().UseIdentityColumn();
            builder.Property(x => x.Name).HasColumnName("Name").HasMaxLength(100).IsRequired();
            builder.Property(x => x.RowVersion).HasColumnName("RowVersion").IsRowVersion();
        }
    }
}
