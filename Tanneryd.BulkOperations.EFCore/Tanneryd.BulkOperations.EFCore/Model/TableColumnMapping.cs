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

using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Tanneryd.BulkOperations.EFCore.Model
{
    /// <summary>
    /// This class is used to map an entity property
    /// to a table column.
    /// 
    /// In conceptual-space, EdmProperty represents a property on an Entity.
    /// In store-space, EdmProperty represents a column in a table.
    /// </summary>
    public class TableColumnMapping
    {
        public bool IsIncludedFromComplexType { get; set; }
        public bool IsForeignKey { get; set; }
        public bool IsPrimaryKey { get; set; }
        public bool IsIdentity { get; set; }
        public IProperty EntityProperty { get; set; }
        public IColumnMapping TableColumn { get; set; }
    }
}