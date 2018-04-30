using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Linq.Expressions;

namespace Tanneryd.BulkOperations.EF6.Model
{
    public class KeyPropertyMapping
    {
        public string ItemPropertyName { get; set; }
        public string EntityPropertyName { get; set; }

        public static KeyPropertyMapping[] IdentityMappings(string[] names)
        {
            return names.Select(n => new KeyPropertyMapping
            {
                ItemPropertyName = n,
                EntityPropertyName = n
            }).ToArray();
        }
    }

    public class BulkSelectRequest<T>
    {
        public BulkSelectRequest(string[] keyPropertyNames)
        {
            KeyPropertyMappings = keyPropertyNames.Select(n => new KeyPropertyMapping
                {
                    ItemPropertyName = n,
                    EntityPropertyName = n
                })
                .ToArray();
        }
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