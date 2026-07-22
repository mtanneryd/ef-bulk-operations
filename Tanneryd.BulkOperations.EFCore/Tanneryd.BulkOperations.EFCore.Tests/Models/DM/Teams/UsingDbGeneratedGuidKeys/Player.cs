using System;

namespace Tanneryd.BulkOperations.EFCore.Tests.Models.DM.Teams.UsingDbGeneratedGuidKeys
{
    public class PlayerWithDbGeneratedGuidKey
    {
        public Guid Id { get; set; }
        public string Firstname { get; set; }
        public string Lastname { get; set; }
        public Guid TeamId { get; set; }

        public TeamWithDbGeneratedGuidKey Team { get; set; }

        public PlayerWithDbGeneratedGuidKey()
        {
            Id = Guid.NewGuid();
        }
    }
}
