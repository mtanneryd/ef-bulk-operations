using System.Collections.Generic;
using System.Data.SqlClient;

namespace Tanneryd.BulkOperations.EF6.Model
{
    public class BulkSelectExistingRequest<T>
    {
        public IList<T> Entities { get; set; }
        public string[] KeyMemberNames { get; set; }
        public SqlTransaction Transaction { get; set; }


        public BulkSelectExistingRequest()
        {
            KeyMemberNames = new string[0];
            Entities = new T[0];
        }
    }
}