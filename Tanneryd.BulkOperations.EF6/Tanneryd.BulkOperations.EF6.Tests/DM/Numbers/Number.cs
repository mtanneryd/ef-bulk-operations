/*
* Copyright ©  2017 Tånneryd IT AB
* 
* This file is part of the tutorial application BulkInsert.App.
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

namespace Tanneryd.BulkOperations.EF6.Tests.DM.Numbers
{
    public class Number
    {
        public long Id { get; set; }
        public long Value { get; set; }

        public int ParityId { get; set; }
        public Parity Parity { get; set; }

        public int? PrimeId { get; set; }
        public Prime Prime { get; set; }

        public int? CompositeId { get; set; }
        public Composite Composite { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string UpdatedBy { get; set; }

    }
}