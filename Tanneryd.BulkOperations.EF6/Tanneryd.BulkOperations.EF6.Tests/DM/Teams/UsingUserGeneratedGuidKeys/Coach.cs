using System;
using System.Collections.Generic;

namespace Tanneryd.BulkOperations.EF6.Tests.DM.Teams.UsingUserGeneratedGuidKeys
{
    public class Coach
    {
        public Coach()
        {
            Teams = new HashSet<Team>();
        }
        public Guid Id { get; set; }
        public string Firstname { get; set; }
        public string Lastname { get; set; }

        public ICollection<Team> Teams { get; set; }
    }
}