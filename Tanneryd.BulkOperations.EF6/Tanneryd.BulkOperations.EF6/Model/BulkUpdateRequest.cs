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
using System.Collections;
using Microsoft.Data.SqlClient;

namespace Tanneryd.BulkOperations.EF6.Model
{
    /// <summary>
    /// Parameters for BulkUpdateAll. Stages rows in a temp table, UPDATEs the target
    /// on key match (plus concurrency tokens when mapped), and optionally INSERTs
    /// unmatched rows when InsertIfNew is set. Stale concurrency tokens throw
    /// DbUpdateConcurrencyException.
    /// </summary>
    public class BulkUpdateRequest
    {
        public BulkUpdateRequest()
        {
            UpdatedPropertyNames = new string[0];
            KeyPropertyNames = new string[0];
            InsertIfNew = false;
        }

        public IList Entities { get; set; }

        /// <summary>
        /// CLR property names to update. Empty or null means all mapped non-key columns.
        /// </summary>
        public string[] UpdatedPropertyNames { get; set; }

        /// <summary>
        /// CLR property names used as the join/match key. Empty or null means the table primary key.
        /// When the entity has concurrency tokens, this must be empty/null or exactly the
        /// primary key — non-unique keys can update multiple rows and false-fire
        /// <see cref="System.Data.Entity.Infrastructure.DbUpdateConcurrencyException"/>.
        /// </summary>
        public string[] KeyPropertyNames { get; set; }

        public SqlTransaction Transaction { get; set; }

        /// <summary>
        /// When true, entities that do not match existing rows are inserted.
        /// Staging then includes all insertable columns (not only
        /// <see cref="UpdatedPropertyNames"/>); the UPDATE SET list is unchanged.
        /// </summary>
        public bool InsertIfNew { get; set; }

        /// <summary>
        /// When true, SqlBulkCopy into the staging temp table uses TABLOCK.
        /// </summary>
        public bool UseTableLock { get; set; } = false;

        public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromMinutes(30);
    }
}
