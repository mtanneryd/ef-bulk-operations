using System;

namespace Tanneryd.BulkOperations.EF6.Tests.DM.School
{
    public class DetailReportWithFROM
    {
        // Primary key 
        public int ReportID { get; set; }

        public string Title { get; set; }

        public string Entry { get; set; }

        public DateTime Date { get; set; }

        public string Level1 { get; set; }
        public string Level2 { get; set; }
        public string Level3 { get; set; }

        public string BadNamedColumn1 { get; set; }
        public string BadNamedColumn2 { get; set; }
        public string BadNamedColumn3 { get; set; }

        public decimal Volume { get; set; }
        public decimal Amount { get; set; }

        // Foreign key 
        public int PeriodID { get; set; }
        public virtual Period Period { get; set; }

        // Foreign key 
        public int SummaryReportID { get; set; }
        public virtual SummaryReportFROMTableASExtent SummaryReport { get; set; }
    }
}