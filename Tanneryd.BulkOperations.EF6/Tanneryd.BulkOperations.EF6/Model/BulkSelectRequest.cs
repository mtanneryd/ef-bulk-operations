/*
 * Copyright ©  2017-2018 Tånneryd IT AB
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
using System.Data.SqlClient;
using System.Linq;
using System.Linq.Expressions;

namespace Tanneryd.BulkOperations.EF6.Model
{
    public class KeyPropertyMapping
    {
        public string ItemPropertyName { get; set; }
        public string EntityPropertyName { get; set; }

        public static KeyPropertyMapping[] IdentityMappings(string[] names)
        {
            return names.Select(n => new KeyPropertyMapping
            {
                ItemPropertyName = n,
                EntityPropertyName = n
            }).ToArray();
        }
    }

    public class BulkDeleteRequest<T> : BulkSelectRequest<T>
    {
        public BulkDeleteRequest(string[] keyPropertyNames, IList<T> items = null, SqlTransaction transaction = null)
            :base(keyPropertyNames, items, transaction)
        {
            
        }
    }

    public class BulkSelectRequest<T>
    {
        public BulkSelectRequest(string[] keyPropertyNames, IList<T> items = null, SqlTransaction transaction = null)
        {
            KeyPropertyMappings = keyPropertyNames.Select(n => new KeyPropertyMapping
                {
                    ItemPropertyName = n,
                    EntityPropertyName = n
                })
                .ToArray();
            Items = items;
            Transaction = transaction;
        }
        public IList<T> Items { get; set; }
        public KeyPropertyMapping[] KeyPropertyMappings { get; set; }
        public SqlTransaction Transaction { get; set; }
        public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromMinutes(1);

        public BulkSelectRequest()
        {
            KeyPropertyMappings = new KeyPropertyMapping[0];
            Items = new T[0];
        }
    }
}