using System;
using System.Collections.Generic;

namespace Tanneryd.BulkOperations.EFCore.Tests.Models.DM.Teams.UsingDbGeneratedGuidKeys
{
    public class TeamWithDbGeneratedGuidKey
    {
        public Guid Id { get; set; }
        public string Name { get; set; }

        public ICollection<PlayerWithDbGeneratedGuidKey> Players { get; set; }

        public TeamWithDbGeneratedGuidKey()
        {
            Id = Guid.NewGuid();
            Players = new List<PlayerWithDbGeneratedGuidKey>();
        }
    }
}
