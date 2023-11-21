// <auto-generated>

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Tanneryd.BulkOperations.EFCore.SQLite.Tests
{
    public class EmptyTableConfiguration : IEntityTypeConfiguration<EmptyTable>
    {
        public void Configure(EntityTypeBuilder<EmptyTable> builder)
        {
            builder.ToTable("EmptyTable", "main");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasColumnName(@"Id").HasColumnType("integer").IsRequired().ValueGeneratedOnAdd();
        }
    }

}
// </auto-generated>
