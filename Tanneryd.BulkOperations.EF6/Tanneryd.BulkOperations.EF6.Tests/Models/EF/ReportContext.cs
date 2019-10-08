using System.Data.Entity;
using System.Data.Entity.ModelConfiguration.Conventions;
using Tanneryd.BulkOperations.EF6.Tests.DM.School;

namespace Tanneryd.BulkOperations.EF6.Tests.EF
{
    // https://msdn.microsoft.com/en-us/library/jj591620%28v=vs.113%29.aspx?f=255&MSPPError=-2147217396
    public class ReportContext : DbContext
    {
        public DbSet<Period> Periods { get; set; }
        public DbSet<SummaryReportFROMTableASExtent> SummaryReports { get; set; }
        public DbSet<DetailReportWithFROM> DetailReports { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            Database.SetInitializer(new DropCreateDatabaseIfModelChanges<ReportContext>());

            modelBuilder.Conventions.Remove<PluralizingTableNameConvention>();

            #region Period

            modelBuilder.Entity<Period>()
                .ToTable("Period", "Some.Complex_Schema Name")
                .HasKey(p => p.PeriodID);
            modelBuilder.Entity<Period>()
                .Property(e => e.Name);
            modelBuilder.Entity<Period>()
                .HasMany(e => e.SummaryReports)
                .WithRequired(e => e.Period)
                .HasForeignKey(e => e.PeriodID);

            #endregion

            #region SummaryReport

            modelBuilder.Entity<SummaryReportFROMTableASExtent>()
                .HasKey(p => p.ReportID);
            modelBuilder.Entity<SummaryReportFROMTableASExtent>()
                .Property(e => e.Title);
            modelBuilder.Entity<SummaryReportFROMTableASExtent>()
                .Property(e => e.Entry);
            modelBuilder.Entity<SummaryReportFROMTableASExtent>()
                .HasRequired(t => t.Period)
                .WithMany(t => t.SummaryReports)
                .HasForeignKey(e => e.PeriodID);

            #endregion

            #region DetailReport

            modelBuilder.Entity<DetailReportWithFROM>()
                .ToTable("SELECT WORSE FROM NAMES AS Extent1", "In # Some.Complex_Schema @Name")
                .HasKey(p => p.ReportID);
            modelBuilder.Entity<DetailReportWithFROM>()
                .Property(e => e.Title);
            modelBuilder.Entity<DetailReportWithFROM>()
                .Property(e => e.Entry);
            modelBuilder.Entity<DetailReportWithFROM>()
                .Property(e => e.BadNamedColumn1).HasColumnName("I am Bad Column Name 1 ");
            modelBuilder.Entity<DetailReportWithFROM>()
                .Property(e => e.BadNamedColumn2).HasColumnName("A #_@ Abc 123 Абв$");
            modelBuilder.Entity<DetailReportWithFROM>()
                .Property(e => e.BadNamedColumn3).HasColumnName("SELECT WORSE FROM NAMES AS Extent ");
            modelBuilder.Entity<DetailReportWithFROM>()
                .HasRequired(t => t.Period)
                .WithMany(t => t.DetailReports)
                .HasForeignKey(e => e.PeriodID);
            modelBuilder.Entity<DetailReportWithFROM>()
                .HasRequired(t => t.SummaryReport)
                .WithMany()
                .HasForeignKey(e => e.SummaryReportID)
                .WillCascadeOnDelete(false);
            
            #endregion
        }
    }
}