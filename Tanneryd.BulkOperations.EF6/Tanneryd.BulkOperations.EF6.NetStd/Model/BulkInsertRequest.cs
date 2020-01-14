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

using System;
using System.Collections.Generic;
using System.Data.SqlClient;

namespace Tanneryd.BulkOperations.EF6.NetStd.Model
{
    public class BulkInsertRequest<T>
    {
        public IList<T> Entities { get; set; }
        public SqlTransaction Transaction { get; set; }
        public bool UpdateStatistics { get; set; } = false;
        public EnableRecursiveInsert EnableRecursiveInsert { get; set; } = EnableRecursiveInsert.NoButRetrieveGeneratedPrimaryKeys;
        public AllowNotNullSelfReferences AllowNotNullSelfReferences { get; set; } = AllowNotNullSelfReferences.No;
        public bool SortUsingClusteredIndex { get; set; } = true;
        public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromMinutes(30);
    }

    public enum AllowNotNullSelfReferences
    {
        No,
        Yes
    }

    public enum EnableRecursiveInsert
    {
        NoButRetrieveGeneratedPrimaryKeys,
        NoAndIgnoreGeneratedPrimaryKeys,
        Yes
    }
}