// <auto-generated>
// ReSharper disable All

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Tanneryd.BulkOperations.EFCore.Tests
{
    // Parity
    public class Parity
    {
        public int Id { get; set; } // Id (Primary key)
        public string Name { get; set; } // Name
        public DateTime UpdatedAt { get; set; } // UpdatedAt
        public string UpdatedBy { get; set; } // UpdatedBy

        // Reverse navigation

        /// <summary>
        /// Child Numbers where [Number].[ParityId] point to this entity (FK_dbo.Number_dbo.Parity_ParityId)
        /// </summary>
        public ICollection<Number> Numbers { get; set; } // Number.FK_dbo.Number_dbo.Parity_ParityId

        public Parity()
        {
            Numbers = new List<Number>();
        }
    }

}
// </auto-generated>