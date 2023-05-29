// <auto-generated>
// ReSharper disable All

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Tanneryd.BulkOperations.EFCore.Tests
{
    // CoachWithUserGeneratedGuid
    public class CoachWithUserGeneratedGuid
    {
        public Guid Id { get; set; } // Id (Primary key)
        public string Firstname { get; set; } // Firstname
        public string Lastname { get; set; } // Lastname

        // Reverse navigation

        /// <summary>
        /// Child CoachTeamsWithUserGeneratedGuids where [CoachTeamsWithUserGeneratedGuid].[CoachId] point to this entity (FK_dbo.CoachTeamsWithUserGeneratedGuid_dbo.CoachWithUserGeneratedGuid_CoachId)
        /// </summary>
        public ICollection<CoachTeamsWithUserGeneratedGuid> CoachTeamsWithUserGeneratedGuids { get; set; } // CoachTeamsWithUserGeneratedGuid.FK_dbo.CoachTeamsWithUserGeneratedGuid_dbo.CoachWithUserGeneratedGuid_CoachId

        [NotMapped]
        public List<TeamWithUserGeneratedGuid> Teams
        {
            get
            {
                return CoachTeamsWithUserGeneratedGuids.Select(ct => ct.TeamWithUserGeneratedGuid).ToList();
            }
        }

        public CoachWithUserGeneratedGuid()
        {
            CoachTeamsWithUserGeneratedGuids = new List<CoachTeamsWithUserGeneratedGuid>();
        }
    }

}
// </auto-generated>