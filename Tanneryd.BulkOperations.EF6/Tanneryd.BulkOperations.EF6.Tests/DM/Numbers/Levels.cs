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

namespace Tanneryd.BulkOperations.EF6.Tests.DM.Numbers
{
    public class Level1
    {
        public int Id { get; set; }
        public Level2 Level2 { get; set; }
    }

    public class Level2
    {
        public string Level2Name { get; set; }
        public Level3 Level3 { get; set; }
    }

    public class Level3
    {
        public string Level3Name { get; set; }
        public DateTime Updated { get; set; }
    }
}