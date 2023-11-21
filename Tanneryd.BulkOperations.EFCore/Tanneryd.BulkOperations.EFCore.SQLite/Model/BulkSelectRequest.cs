﻿/*
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
using Microsoft.Data.SqlClient;

namespace Tanneryd.BulkOperations.EFCore.Model
{
    public class BulkSelectRequest<T>
    {
        public BulkSelectRequest(string[] keyPropertyNames, IList<T> items = null, SqlTransaction transaction = null)
        {
            KeyPropertyMappings = KeyPropertyMapping.IdentityMappings(keyPropertyNames);
            ColumnPropertyMappings = new KeyPropertyMapping[0];
            Items = items;
            Transaction = transaction;
        }

        public IList<T> Items { get; set; }
        public KeyPropertyMapping[] KeyPropertyMappings { get; set; }
        
        /// <summary>
        /// Mappings for the columns we would like to update on our
        /// local entities if they match existing entities in the database.
        /// </summary>
        public KeyPropertyMapping[] ColumnPropertyMappings { get; set; }
        public SqlTransaction Transaction { get; set; }
        public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromMinutes(1);

        public BulkSelectRequest()
        {
            KeyPropertyMappings = Array.Empty<KeyPropertyMapping>();
            ColumnPropertyMappings = Array.Empty<KeyPropertyMapping>();
            Items = Array.Empty<T>();
        }
    }
}