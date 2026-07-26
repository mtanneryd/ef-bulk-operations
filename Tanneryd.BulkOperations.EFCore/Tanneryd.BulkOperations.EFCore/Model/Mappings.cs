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

namespace Tanneryd.BulkOperations.EFCore.Model
{
    /// <summary>
    /// EF model mappings for a CLR entity type: table name, columns, and FK relations.
    /// </summary>
    public class Mappings
    {
        public Mappings()
        {
            ComplexPropertyNames = new String [0];
            ToForeignKeyMappings = new ForeignKeyMapping[0];
            FromForeignKeyMappings = new ForeignKeyMapping[0];
        }

        public TableName TableName { get; set; }
        public Discriminator Discriminator { get; set; }
        public string[] ComplexPropertyNames { get; set; }
        /// <summary>
        /// Complex leaf path (e.g. <c>Home.Street</c> or <c>Level2.Level3.Updated</c>)
        /// → store column name. Flatten uses these as Expando keys so sibling complex
        /// properties with the same leaf CLR name do not collide.
        /// </summary>
        public Dictionary<string, string> ComplexLeafColumnNameByPath { get; set; } =
            new Dictionary<string, string>(StringComparer.Ordinal);
        public Dictionary<string, TableColumnMapping> ColumnMappingByPropertyName { get; set; }
        public Dictionary<string, TableColumnMapping> ColumnMappingByColumnName { get; set; }
        /// <summary>
        /// Optimistic concurrency tokens (e.g. rowversion). Not included in
        /// <see cref="ColumnMappingByPropertyName"/> so inserts never write them.
        /// </summary>
        public TableColumnMapping[] ConcurrencyTokenMappings { get; set; } = Array.Empty<TableColumnMapping>();
        public ForeignKeyMapping[] ToForeignKeyMappings { get; set; }
        public ForeignKeyMapping[] FromForeignKeyMappings { get; set; }
    }
}