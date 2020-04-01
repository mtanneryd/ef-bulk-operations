/*
* Copyright ©  2017-2019 Tånneryd IT AB
* 
* Licensed under the Apache License, Version 2.0 (the "License");
* you may not use this file except in compliance with the License.
* You may obtain a copy of the License at
* 
*   http://www.apache.org/licenses/LICENSE-2.0
* 
* Unless required by applicable law or agreed to in writing, software
* distributed under the License is distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and
* limitations under the License.
*/

using System.Data.Entity;
using System.Data.Entity.ModelConfiguration.Conventions;
using Tanneryd.BulkOperations.EF6.NET47.Tests.Models.DM.Report;

namespace Tanneryd.BulkOperations.EF6.NET47.Tests.Models.EF
{
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
