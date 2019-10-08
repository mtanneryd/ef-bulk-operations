using System.Collections.Generic;

namespace Tanneryd.BulkOperations.EF6.Tests.DM.School
{
    public class Period
    {
        // Primary key
        public int PeriodID { get; set; }
        public string Name { get; set; }

        public virtual ICollection<SummaryReportFROMTableASExtent> SummaryReports { get; set; }
        public virtual ICollection<DetailReportWithFROM> DetailReports { get; set; }
    }
}
