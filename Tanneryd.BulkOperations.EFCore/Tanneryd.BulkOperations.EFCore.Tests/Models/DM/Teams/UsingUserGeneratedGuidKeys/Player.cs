using System;

namespace Tanneryd.BulkOperations.EFCore.Tests.Models.DM.Teams.UsingUserGeneratedGuidKeys
{
    public class PlayerWithUserGeneratedGuidKey
    {
        public Guid Id { get; set; }
        public string Firstname { get; set; }
        public string Lastname { get; set; }
        public Guid TeamId { get; set; }

        public TeamWithUserGeneratedGuidKey Team { get; set; }
    }
}
