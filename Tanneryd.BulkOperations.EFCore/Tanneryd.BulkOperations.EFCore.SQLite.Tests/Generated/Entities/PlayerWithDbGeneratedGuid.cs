// <auto-generated>

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Tanneryd.BulkOperations.EFCore.SQLite.Tests
{
    public class PlayerWithDbGeneratedGuid
    {
        public string Id { get; set; }
        public string Firstname { get; set; }
        public string Lastname { get; set; }
        public string TeamId { get; set; }

        public TeamWithDbGeneratedGuid TeamWithDbGeneratedGuid { get; set; }
    }

}
// </auto-generated>
