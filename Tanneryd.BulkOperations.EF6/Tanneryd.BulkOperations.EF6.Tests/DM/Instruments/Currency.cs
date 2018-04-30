using System.Collections;
using System.Collections.Generic;

namespace Tanneryd.BulkOperations.EF6.Tests.DM.Instruments
{
    public class Currency
    {
        public Currency()
        {
            Instruments = new HashSet<Instrument>();
        }
        public int Key { get; set; }
        public string Id { get; set; }

        public ICollection<Instrument> Instruments { get; set; }
    }
}