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

using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Tanneryd.BulkOperations.EFCore.Model
{
    /// <summary>
    /// Maps an entity property to a table column.
    /// </summary>
    public class TableColumnMapping
    {
        public bool IsIncludedFromComplexType { get; set; }
        public bool IsForeignKey { get; set; }
        public bool IsPrimaryKey { get; set; }

        /// <summary>
        /// True when the column is a SQL Server IDENTITY column
        /// (<c>SqlServerValueGenerationStrategy.IdentityColumn</c>).
        /// Used for IDENTITY_INSERT on staging temps.
        /// </summary>
        public bool IsIdentity { get; set; }

        /// <summary>
        /// True when omitting the column from INSERT lets SQL Server supply the value
        /// (IDENTITY, DEFAULT such as NEWSEQUENTIALID, computed). Client-side
        /// generators (SequenceHiLo, bare Guid ValueGeneratedOnAdd) are excluded.
        /// Used to choose the MERGE … OUTPUT insert path for single primary keys.
        /// </summary>
        public bool IsStoreGenerated { get; set; }

        public IProperty EntityProperty { get; set; }
        public IColumnMapping TableColumn { get; set; }
    }
}