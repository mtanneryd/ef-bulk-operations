/*
 * Copyright ©  2017-2019 Tånneryd IT AB
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

using System.Data.Entity.Core.Metadata.Edm;

namespace Tanneryd.BulkOperations.EF6.NetStd.Model
{
    /// <summary>
    /// 
    /// We support two kinds of foreign key mappings.
    /// (1) One-To-One, One-To-Many
    /// (2) Many-To-Many
    /// 
    /// The property ForeignKeyRelations holds mapping data used for (1)
    /// and the property AssociationMapping holds data used for (2).
    /// 
    /// We do not support Many-To-Many relationships with compound keys.
    /// 
    /// </summary>
    internal class ForeignKeyMapping
    {
        public ForeignKeyMapping()
        {
            ForeignKeyRelations = new ForeignKeyRelation[0];
        }

        public BuiltInTypeKind BuiltInTypeKind { get; set; }
        public string NavigationPropertyName { get; set; }
        public string FromType { get; set; }
        public string ToType { get; set; }

        public ForeignKeyRelation[] ForeignKeyRelations { get; set; }
        public AssociationMapping AssociationMapping { get; set; }
    }
}