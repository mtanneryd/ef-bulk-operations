using System.Collections.Generic;
using System.Data.SqlClient;

namespace Tanneryd.BulkOperations.EF6.Model
{
    public class BulkSelectRequest<T>
    {
        public IList<T> Entities { get; set; }
        public string[] KeyPropertyNames { get; set; }
        public SqlTransaction Transaction { get; set; }


        public BulkSelectRequest()
        {
            KeyPropertyNames = new string[0];
            Entities = new T[0];
        }
    }
}