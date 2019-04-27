using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tanneryd.BulkOperations.EF6.Tests.Models.DM.Prices
{
    public class Price
    {
        public int Id { get; set; }

        public DateTime Date { get; set; }
        public string Name { get; set; }
        public decimal? Value { get; set; }
    }
}
