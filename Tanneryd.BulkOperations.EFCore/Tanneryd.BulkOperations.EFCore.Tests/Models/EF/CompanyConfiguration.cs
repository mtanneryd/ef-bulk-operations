// <auto-generated>
// ReSharper disable All

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Tanneryd.BulkOperations.EFCore.Tests
{
    // Company
    public class CompanyConfiguration : IEntityTypeConfiguration<Company>
    {
        public void Configure(EntityTypeBuilder<Company> builder)
        {
            builder.ToTable("Company", "dbo");
            builder.HasKey(x => x.Id).HasName("PK_dbo.Company").IsClustered();

            builder.Property(x => x.Id).HasColumnName(@"Id").HasColumnType("bigint").IsRequired().ValueGeneratedOnAdd().UseIdentityColumn();
            builder.Property(x => x.Name).HasColumnName(@"Name").HasColumnType("nvarchar(max)").IsRequired();
            builder.Property(x => x.ParentCompanyId).HasColumnName(@"ParentCompanyId").HasColumnType("bigint").IsRequired();

            // Foreign keys
            builder.HasOne(a => a.ParentCompany).WithMany(b => b.Companies).HasForeignKey(c => c.ParentCompanyId).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("FK_dbo.Company_dbo.Company_ParentCompanyId");

            builder.HasIndex(x => x.ParentCompanyId).HasName("IX_ParentCompanyId");
        }
    }

}
// </auto-generated>