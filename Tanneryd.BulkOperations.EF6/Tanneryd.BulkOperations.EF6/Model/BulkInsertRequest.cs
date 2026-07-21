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

namespace Tanneryd.BulkOperations.EF6.Model
{
    /// <summary>
    /// Parameters for BulkInsertAll.
    /// </summary>
    public class BulkInsertRequest<T>
    {
        public IList<T> Entities { get; set; }
        public SqlTransaction Transaction { get; set; }

        /// <summary>
        /// When true, runs UPDATE STATISTICS on the target table after insert.
        /// </summary>
        public bool UpdateStatistics { get; set; } = false;

        /// <summary>
        /// Controls whether related entities are inserted and whether store-generated
        /// primary keys are retrieved back onto the local entities.
        /// </summary>
        public EnableRecursiveInsert EnableRecursiveInsert { get; set; } = EnableRecursiveInsert.NoButRetrieveGeneratedPrimaryKeys;

        /// <summary>
        /// When Yes, temporarily disables table CHECK/FK constraints so not-null
        /// self-referencing graphs can be inserted (requires ALTER TABLE privileges).
        /// </summary>
        public AllowNotNullSelfReferences AllowNotNullSelfReferences { get; set; } = AllowNotNullSelfReferences.No;

        /// <summary>
        /// When true, sorts entities by the target table clustered index before bulk copy.
        /// </summary>
        public bool SortUsingClusteredIndex { get; set; } = true;

        /// <summary>
        /// When true, SqlBulkCopy uses TABLOCK on the destination. Opt-in: improves throughput
        /// for large exclusive loads but reduces concurrency with other writers.
        /// </summary>
        public bool UseTableLock { get; set; } = false;

        public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromMinutes(30);
    }

    public enum AllowNotNullSelfReferences
    {
        No,
        Yes
    }

    /// <summary>
    /// Recursive insert behavior for bulk insert.
    /// </summary>
    public enum EnableRecursiveInsert
    {
        /// <summary>Insert only the supplied entities; retrieve store-generated PKs.</summary>
        NoButRetrieveGeneratedPrimaryKeys,

        /// <summary>Insert only the supplied entities; skip retrieving store-generated PKs.</summary>
        NoAndIgnoreGeneratedPrimaryKeys,

        /// <summary>Walk navigation properties and insert the full entity graph.</summary>
        Yes
    }
}
