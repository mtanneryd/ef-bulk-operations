using System;
using System.Collections.Generic;

namespace Tanneryd.BulkOperations.EFCore.Tests.Models.DM.Teams.UsingUserGeneratedGuidKeys
{
    public class CoachWithUserGeneratedGuidKey
    {
        public Guid Id { get; set; }
        public string Firstname { get; set; }
        public string Lastname { get; set; }

        public ICollection<TeamWithUserGeneratedGuidKey> Teams { get; set; }

        public CoachWithUserGeneratedGuidKey()
        {
            Teams = new List<TeamWithUserGeneratedGuidKey>();
        }
    }
}
