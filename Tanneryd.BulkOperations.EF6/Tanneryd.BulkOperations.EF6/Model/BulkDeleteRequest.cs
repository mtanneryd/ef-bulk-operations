using System.Collections.Generic;
using System.Data.SqlClient;

namespace Tanneryd.BulkOperations.EF6.Model
{
    public class BulkDeleteRequest<T> : BulkSelectRequest<T>
    {
        public BulkDeleteRequest(string[] keyPropertyNames, IList<T> items = null, SqlTransaction transaction = null)
            :base(keyPropertyNames, items, transaction)
        {
            
        }
    }
}