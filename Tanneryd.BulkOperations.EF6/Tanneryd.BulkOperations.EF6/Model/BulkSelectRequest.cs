using System.Collections.Generic;
using System.Data.SqlClient;

namespace Tanneryd.BulkOperations.EF6.Model
{
    public class KeyPropertyMapping
    {
        public string ItemPropertyName { get; set; }
        public string EntityPropertyName { get; set; }
    }

    public class BulkSelectRequest<T>
    {
        public IList<T> Items { get; set; }
        public KeyPropertyMapping[] KeyPropertyMappings { get; set; }
        public SqlTransaction Transaction { get; set; }


        public BulkSelectRequest()
        {
            KeyPropertyMappings = new KeyPropertyMapping[0];
            Items = new T[0];
        }
    }
}