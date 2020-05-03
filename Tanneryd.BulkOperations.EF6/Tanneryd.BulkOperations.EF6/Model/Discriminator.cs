using System;
using System.Data.Entity.Core.Metadata.Edm;

namespace Tanneryd.BulkOperations.EF6.Model
{
    public class Discriminator
    {
        public EdmProperty Column { get; set; }
        public object Value { get; set; }
    }
}