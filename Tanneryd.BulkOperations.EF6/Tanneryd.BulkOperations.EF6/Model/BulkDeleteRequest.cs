using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace Tanneryd.BulkOperations.EF6.Model
{
    /// <summary>
    /// Parameters for BulkDeleteNotExisting. Deletes rows matching SqlConditions
    /// that do not appear in Items according to KeyPropertyMappings.
    /// Empty Items is rejected unless <see cref="AllowDeleteAllMatchingConditions"/> is true.
    /// </summary>
    public class BulkDeleteRequest<T>
    {
        public SqlCondition[] SqlConditions { get; set; }

        public KeyPropertyMapping[] KeyPropertyMappings { get; set; }
        public IList<T> Items { get; set; }
        public SqlTransaction Transaction { get; set; }
        public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// When true, SqlBulkCopy into the staging temp table uses TABLOCK.
        /// </summary>
        public bool UseTableLock { get; set; } = false;

        /// <summary>
        /// When true, an empty <see cref="Items"/> list is allowed and deletes every
        /// row in the SqlConditions window. Default is false (safe).
        /// </summary>
        public bool AllowDeleteAllMatchingConditions { get; set; }

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
