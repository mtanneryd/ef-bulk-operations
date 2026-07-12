using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Tanneryd.BulkOperations.EFCore.Tests
{
    public class LogItemConfiguration : IEntityTypeConfiguration<LogItem>
    {
        public void Configure(EntityTypeBuilder<LogItem> builder)
        {
            builder.ToTable("LogItem", "dbo");
            builder.HasKey(x => x.Id).HasName("PK_dbo.LogItem").IsClustered();

            builder.Property(x => x.Id).HasColumnName(@"Id").HasColumnType("int").IsRequired().ValueGeneratedOnAdd().UseIdentityColumn();
            builder.Property(x => x.Message).HasColumnName(@"Message").HasColumnType("nvarchar(max)").IsRequired(false);
            builder.Property(x => x.Timestamp).HasColumnName(@"Timestamp").HasColumnType("datetime").IsRequired();

            builder.HasDiscriminator<string>("LogType")
                .HasValue<LogWarning>("Warning")
                .HasValue<LogError>("Error");
        }
    }

    public class LogWarningConfiguration : IEntityTypeConfiguration<LogWarning>
    {
        public void Configure(EntityTypeBuilder<LogWarning> builder)
        {
            builder.Property(x => x.Recommendation).HasColumnName(@"Recommendation").HasColumnType("nvarchar(max)").IsRequired(false);
        }
    }

    public class LogErrorConfiguration : IEntityTypeConfiguration<LogError>
    {
        public void Configure(EntityTypeBuilder<LogError> builder)
        {
            builder.Property(x => x.Severity).HasColumnName(@"Severity").HasColumnType("int").IsRequired();
        }
    }
}
