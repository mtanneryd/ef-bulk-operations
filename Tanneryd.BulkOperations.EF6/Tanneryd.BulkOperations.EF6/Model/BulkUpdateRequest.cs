using System.Collections;
using System.Data.SqlClient;

namespace Tanneryd.BulkOperations.EF6.Model
{
    public class BulkUpdateRequest
    {
        public BulkUpdateRequest()
        {
            UpdatedColumnNames = new string[0];
            KeyPropertyNames = new string[0];
            InsertIfNew = false;
        }

        public IList Entities { get; set; }
        public string[] UpdatedColumnNames { get; set; }
        public string[] KeyPropertyNames { get; set; }
        public SqlTransaction Transaction { get; set; }
        public bool InsertIfNew { get; set; }
    }
}