using System;
using System.Collections.Generic;

namespace Tanneryd.BulkOperations.EF6.Model
{
    public class BulkOperationResponse
    {
        public TimeSpan Elapsed { get; set; } = TimeSpan.Zero;

        public List<Tuple<Type, long>> AffectedRows { get; set; } = new List<Tuple<Type, long>>();
    }

    public class BulkInsertResponse : BulkOperationResponse
    {
        public List<Tuple<Type, BulkInsertStatistics>> BulkInsertStatistics { get; set; } = new List<Tuple<Type, BulkInsertStatistics>>();
        public List<string> TablesWithNoCheckConstraints { get; set; } = new List<string>();
    }

    public struct BulkInsertStatistics {
        public TimeSpan TimeElapsedDuringBulkCopy { get; set; }
        public TimeSpan TimeElapsedDuringInsertInto { get; set; }
    }
}