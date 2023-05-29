// <auto-generated>
// ReSharper disable All

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Tanneryd.BulkOperations.EFCore.Tests
{
    // Visitor
    public class VisitorConfiguration : IEntityTypeConfiguration<Visitor>
    {
        public void Configure(EntityTypeBuilder<Visitor> builder)
        {
            builder.ToTable("Visitor", "dbo");
            builder.HasKey(x => x.Id).HasName("PK_dbo.Visitor").IsClustered();

            builder.Property(x => x.Id).HasColumnName(@"Id").HasColumnType("uniqueidentifier").IsRequired().ValueGeneratedOnAdd();
            builder.Property(x => x.Name).HasColumnName(@"Name").HasColumnType("nvarchar(max)").IsRequired();
            
            // builder
            //     .HasMany(p => p.Posts)
            //     .WithMany(p => p.Visitors)
            //     .UsingEntity(j => j.ToTable("VisitorPosts"));
        }
    }

}
// </auto-generated>