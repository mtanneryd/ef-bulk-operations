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
using System.Collections;
using System.Data.SqlClient;

namespace Tanneryd.BulkOperations.EF6.Model
{
    public class BulkUpdateRequest
    {
        public BulkUpdateRequest()
        {
            UpdatedColumnNames = new string[0];
            KeyPropertyNames = new string[0];
            InsertIfNew = false;
        }

        public IList Entities { get; set; }
        public string[] UpdatedColumnNames { get; set; }
        public string[] KeyPropertyNames { get; set; }
        public SqlTransaction Transaction { get; set; }
        public bool InsertIfNew { get; set; }
        public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromMinutes(30);
    }
}