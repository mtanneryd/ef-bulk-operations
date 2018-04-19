using System.Collections.Generic;
using System.Data.SqlClient;

namespace Tanneryd.BulkOperations.EF6.Model
{
    public class BulkInsertRequest<T>
    {
        public IList<T> Entities { get; set; }
        public SqlTransaction Transaction { get; set; }
        public bool Recursive { get; set; }
        public bool AllowNotNullSelfReferences { get; set; }
    }
}