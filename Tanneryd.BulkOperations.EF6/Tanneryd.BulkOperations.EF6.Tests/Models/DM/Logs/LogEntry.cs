using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tanneryd.BulkOperations.EF6.Tests.DM.Logs
{
    public class LogEntry
    {
        public DateTime Date { get; set; }
        public string Message { get; set; }
    }

    public class LogEntryWithComplexType
    {
        public DateTime Date { get; set; }
        public string Message { get; set; }
        public ExtraInfo ExtraInfo { get; set; }
    }

    public class ExtraInfo
    {
        public string Extra1 { get; set; }
        public string Extra2 { get; set; }
        public string Extra3 { get; set; }
    }
}
