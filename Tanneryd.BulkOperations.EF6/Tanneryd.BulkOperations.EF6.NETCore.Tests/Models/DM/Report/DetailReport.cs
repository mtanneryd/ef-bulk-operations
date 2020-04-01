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

namespace Tanneryd.BulkOperations.EF6.NETCore.Tests.Models.DM.Report
{
    public class DetailReportWithFROM
    {
        // Primary key 
        public int ReportID { get; set; }

        public string Title { get; set; }

        public string Entry { get; set; }

        public DateTime Date { get; set; }

        public string Level1 { get; set; }
        public string Level2 { get; set; }
        public string Level3 { get; set; }

        public string BadNamedColumn1 { get; set; }
        public string BadNamedColumn2 { get; set; }
        public string BadNamedColumn3 { get; set; }

        public decimal Volume { get; set; }
        public decimal Amount { get; set; }

        // Foreign key 
        public int PeriodID { get; set; }
        public virtual Period Period { get; set; }

        // Foreign key 
        public int SummaryReportID { get; set; }
        public virtual SummaryReportFROMTableASExtent SummaryReport { get; set; }
    }
}
