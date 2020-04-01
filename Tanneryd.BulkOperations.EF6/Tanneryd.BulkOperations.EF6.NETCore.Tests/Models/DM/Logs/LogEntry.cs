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

namespace Tanneryd.BulkOperations.EF6.NETCore.Tests.Models.DM.Logs
{
    public class LogEntry
    {
        public DateTime Date { get; set; }
        public string Message { get; set; }
    }

    public class LogEntryWithComplexType
    {
        public DateTime Date { get; set; }
        public string Message { get; set; }
        public ExtraInfo ExtraInfo { get; set; }
    }

    public class ExtraInfo
    {
        public string Extra1 { get; set; }
        public string Extra2 { get; set; }
        public string Extra3 { get; set; }
    }
}
