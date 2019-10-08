namespace Tanneryd.BulkOperations.EF6.Tests.DM.School
{
    public class SummaryReportFROMTableASExtent
    {        
        // Primary key 
        public int ReportID { get; set; }

        public string Title { get; set; }

        public string Entry { get; set; }

        public decimal Volume { get; set; }
        public decimal Amount { get; set; }

        // Foreign key 
        public int PeriodID { get; set; }
        public virtual Period Period { get; set; }
    }
}