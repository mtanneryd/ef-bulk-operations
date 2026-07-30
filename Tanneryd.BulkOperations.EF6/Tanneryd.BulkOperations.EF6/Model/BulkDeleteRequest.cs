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
        /// <summary>
        /// Conditions selecting the delete window (rows eligible for deletion).
        /// </summary>
        public SqlCondition[] SqlConditions { get; set; }

        /// <summary>
        /// Mappings used to match local items against database rows.
        /// </summary>
        public KeyPropertyMapping[] KeyPropertyMappings { get; set; }

        /// <summary>
        /// The local items whose matching rows should be kept.
        /// </summary>
        public IList<T> Items { get; set; }

        /// <summary>
        /// Optional existing transaction to enlist in.
        /// </summary>
        public SqlTransaction Transaction { get; set; }

        /// <summary>
        /// Timeout for SQL commands and SqlBulkCopy. Default is 1 minute.
        /// </summary>
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

        /// <summary>
        /// Creates a request with the given conditions and key property names.
        /// </summary>
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

        /// <summary>
        /// Creates an empty request; set <see cref="SqlConditions"/>, <see cref="KeyPropertyMappings"/>
        /// and <see cref="Items"/> before use.
        /// </summary>
        public BulkDeleteRequest()
        {
            KeyPropertyMappings = Array.Empty<KeyPropertyMapping>();
            Items = Array.Empty<T>();
        }
    }
}
