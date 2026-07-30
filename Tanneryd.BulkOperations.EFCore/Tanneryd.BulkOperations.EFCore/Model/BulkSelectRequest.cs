/*
 * Copyright ©  2017-2026 Tånneryd IT AB
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *   http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace Tanneryd.BulkOperations.EFCore.Model
{
    /// <summary>
    /// Parameters for bulk select / select-existing / select-not-existing operations.
    /// </summary>
    public class BulkSelectRequest<T>
    {
        /// <summary>
        /// Creates a request matching on the given key property names.
        /// </summary>
        public BulkSelectRequest(string[] keyPropertyNames, IList<T> items = null, SqlTransaction transaction = null)
        {
            KeyPropertyMappings = KeyPropertyMapping.IdentityMappings(keyPropertyNames);
            ColumnPropertyMappings = Array.Empty<KeyPropertyMapping>();
            Items = items;
            Transaction = transaction;
        }

        /// <summary>
        /// The local items to match against database rows.
        /// </summary>
        public IList<T> Items { get; set; }

        /// <summary>
        /// Mappings used to match local items to database rows.
        /// </summary>
        public KeyPropertyMapping[] KeyPropertyMappings { get; set; }

        /// <summary>
        /// Optional mappings used by BulkSelectExisting to copy matched database
        /// column values onto the local items (not used for BulkUpdate).
        /// </summary>
        public KeyPropertyMapping[] ColumnPropertyMappings { get; set; }

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
        /// Creates an empty request; set <see cref="KeyPropertyMappings"/> and <see cref="Items"/> before use.
        /// </summary>
        public BulkSelectRequest()
        {
            KeyPropertyMappings = Array.Empty<KeyPropertyMapping>();
            ColumnPropertyMappings = Array.Empty<KeyPropertyMapping>();
            Items = Array.Empty<T>();
        }
    }
}
