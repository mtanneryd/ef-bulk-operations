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

namespace Tanneryd.BulkOperations.EF6.Model
{
    public class BulkOperationResponse
    {
        public TimeSpan Elapsed { get; set; } = TimeSpan.Zero;

        public List<Tuple<Type, long>> AffectedRows { get; set; } = new List<Tuple<Type, long>>();
    }

    public class BulkInsertResponse : BulkOperationResponse
    {
        public List<Tuple<Type, BulkInsertStatistics>> BulkInsertStatistics { get; set; } = new List<Tuple<Type, BulkInsertStatistics>>();
        public List<string> TablesWithNoCheckConstraints { get; set; } = new List<string>();
    }

    public struct BulkInsertStatistics {
        public TimeSpan TimeElapsedDuringBulkCopy { get; set; }
        public TimeSpan TimeElapsedDuringInsertInto { get; set; }
    }
}