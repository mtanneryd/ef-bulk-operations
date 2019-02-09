using System;

namespace Tanneryd.BulkOperations.EF6.Tests.DM.Teams.UsingDbGeneratedGuidKeys
{
    public class Player
    {
        public Guid Id { get; set; }
        public string Firstname { get; set; }
        public string Lastname { get; set; }

        public Guid TeamId { get; set; }
        public Team Team { get; set; }
    }
}