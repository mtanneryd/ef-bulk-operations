using System;
using System.Collections.Generic;

namespace Tanneryd.BulkOperations.EF6.Tests.DM.Teams.UsingDbGeneratedGuidKeys
{
    public class Coach
    {
        public Guid Id { get; set; }
        public string Firstname { get; set; }
        public string Lastname { get; set; }

        public ICollection<Team> Teams { get; set; }
    }
}