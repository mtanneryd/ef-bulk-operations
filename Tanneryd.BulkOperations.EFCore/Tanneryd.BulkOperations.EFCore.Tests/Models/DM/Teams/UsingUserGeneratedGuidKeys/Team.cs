using System;
using System.Collections.Generic;

namespace Tanneryd.BulkOperations.EFCore.Tests.Models.DM.Teams.UsingUserGeneratedGuidKeys
{
    public class TeamWithUserGeneratedGuidKey
    {
        public Guid Id { get; set; }
        public string Name { get; set; }

        public ICollection<PlayerWithUserGeneratedGuidKey> Players { get; set; }

        public TeamWithUserGeneratedGuidKey()
        {
            Players = new List<PlayerWithUserGeneratedGuidKey>();
        }
    }
}
