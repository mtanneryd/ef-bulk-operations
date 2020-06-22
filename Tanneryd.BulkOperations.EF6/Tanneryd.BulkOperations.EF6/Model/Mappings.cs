/*
 * Copyright ©  2017-2020 Tånneryd IT AB
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
using System.Linq;

namespace Tanneryd.BulkOperations.EF6.Model
{
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
        public Dictionary<string, TableColumnMapping> ColumnMappingByPropertyName { get; set; }
        public Dictionary<string, TableColumnMapping> ColumnMappingByColumnName { get; set; }
        public ForeignKeyMapping[] ToForeignKeyMappings { get; set; }
        public ForeignKeyMapping[] FromForeignKeyMappings { get; set; }

        public string[] GetPrimaryKeyMembers()
        {
            dynamic declaringType = ColumnMappingByPropertyName
                .Values
                .First()
                .TableColumn
                .DeclaringType;

            var primaryKeyMembers = new List<string>();
            foreach (var keyMember in declaringType.KeyMembers)
                primaryKeyMembers.Add(keyMember.ToString());

            return primaryKeyMembers.ToArray();
        }

        public TableColumnMapping[] GetPrimaryKeyColumnMappings()
        {
            var keyMembers = GetPrimaryKeyMembers();
            var pkColumnMappings = ColumnMappingByPropertyName.Values
                .Where(m => keyMembers.Contains(m.TableColumn.Name))
                .ToArray();
            return pkColumnMappings;
        }

        public bool IsPrimaryKeyStoreGenerated(TableColumnMapping[] pkColumnMappings)
        {
            return pkColumnMappings.Length == 1 &&
                   (pkColumnMappings[0].TableColumn.IsStoreGeneratedIdentity ||
                    pkColumnMappings[0].TableColumn.IsStoreGeneratedComputed);
        }
    }
}