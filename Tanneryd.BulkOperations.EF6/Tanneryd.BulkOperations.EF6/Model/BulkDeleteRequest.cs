using System;
using System.Collections.Generic;
using System.Data.SqlClient;

namespace Tanneryd.BulkOperations.EF6.Model
{
    public class BulkDeleteRequest<T>
    {
        public SqlCondition[] SqlConditions { get; set; }

        public KeyPropertyMapping[] KeyPropertyMappings { get; set; }
        public IList<T> Items { get; set; }
        public SqlTransaction Transaction { get; set; }
        public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromMinutes(1);

        public BulkDeleteRequest(
            SqlCondition[] sqlConditions,
            string[] keyPropertyNames,
            IList<T> items = null,
            SqlTransaction transaction = null)
        {
            SqlConditions = sqlConditions;
            KeyPropertyMappings = KeyPropertyMapping.IdentityMappings(keyPropertyNames);
            Items = items;
            Transaction = transaction;
        }

        public BulkDeleteRequest()
        {
            KeyPropertyMappings = new KeyPropertyMapping[0];
            Items = new T[0];
        }
    }
}