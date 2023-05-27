using Microsoft.EntityFrameworkCore.Metadata;

namespace Tanneryd.BulkOperations.EFCore.Model
{
    public class Discriminator
    {
        public IProperty Column { get; set; }
        public object Value { get; set; }
    }
}