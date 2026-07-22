using System;
using System.Collections.Generic;

namespace Tanneryd.BulkOperations.EFCore.Tests.Models.DM.Teams.UsingDbGeneratedGuidKeys
{
    public class CoachWithDbGeneratedGuidKey
    {
        public Guid Id { get; set; }
        public string Firstname { get; set; }
        public string Lastname { get; set; }

        public ICollection<TeamWithDbGeneratedGuidKey> Teams { get; set; }

        public CoachWithDbGeneratedGuidKey()
        {
            Id = Guid.NewGuid();
            Teams = new List<TeamWithDbGeneratedGuidKey>();
        }
    }
}
