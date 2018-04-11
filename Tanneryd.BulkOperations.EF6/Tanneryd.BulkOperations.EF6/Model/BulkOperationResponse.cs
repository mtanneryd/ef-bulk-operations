using System;
using System.Collections.Generic;

namespace Tanneryd.BulkOperations.EF6.Model
{
    public class BulkOperationResponse
    {
        public TimeSpan Elapsed { get; set; } = TimeSpan.Zero;

        public List<Tuple<Type, long>> AffectedRows { get; set; } = new List<Tuple<Type, long>>();
    }
}