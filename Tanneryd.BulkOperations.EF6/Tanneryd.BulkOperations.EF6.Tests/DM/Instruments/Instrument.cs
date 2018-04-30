using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tanneryd.BulkOperations.EF6.Tests.DM.Instruments
{
    public class Instrument
    {
        public int Key { get; set; }
        public string Name { get; set; }
        public int CurrencyKey { get; set; }
        public Currency Currency { get; set; }
    }
}
