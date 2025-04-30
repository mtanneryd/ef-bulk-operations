using System;

namespace Tanneryd.BulkOperations.EF6.NET48.Tests.Models.DM.Logs
{
    public class LogWarning : LogItem
    {
        public string Recommendation { get; set; }

    }

    public class LogError : LogItem
    {
        public int Severity { get; set; }

    }

    public class LogItem
    {
        public int Id { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
