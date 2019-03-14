using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tanneryd.BulkOperations.EF6.Tests.DM.Miscellaneous
{
    // Use this class to test that we can properly
    // handle tables with sql server reserved keywords.
    public class ReservedSqlKeyword
    {
        public int Id { get; set; }
        public int Key { get; set; }
        public int Identity { get; set; }
        public string Select { get; set; }
    }
}
