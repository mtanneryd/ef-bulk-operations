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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.EF6.Tests.DM.School;
using Tanneryd.BulkOperations.EF6.Tests.EF;
using Tanneryd.BulkOperations.EF6.Tests.Models.DM.Report;

namespace Tanneryd.BulkOperations.EF6.Tests
{
    [TestClass]
    public class DbContextExtensionsTests
    {
        [TestMethod]
        public void SchoolTableNames()
        {
            using (var ctx = new SchoolContext())
            {
                var tableName = ctx.GetTableName(typeof(Course));
                Assert.AreEqual("dbo", tableName.Schema);
                Assert.AreEqual("Course", tableName.Name);

                tableName = ctx.GetTableName(typeof(Department));
                Assert.AreEqual("dbo", tableName.Schema);
                Assert.AreEqual("Department", tableName.Name);

                tableName = ctx.GetTableName(typeof(Instructor));
                Assert.AreEqual("dbo", tableName.Schema);
                Assert.AreEqual("Instructor", tableName.Name);

                tableName = ctx.GetTableName(typeof(OfficeAssignment));
                Assert.AreEqual("dbo", tableName.Schema);
                Assert.AreEqual("OfficeAssignment", tableName.Name);
            }
        }
        
        [TestMethod]
        public void ReportTableNames()
        {
            using (var ctx = new ReportContext())
            {
                var tableName = ctx.GetTableName(typeof(Period));
                Assert.AreEqual("Some.Complex_Schema Name", tableName.Schema);
                Assert.AreEqual("Period", tableName.Name);

                tableName = ctx.GetTableName(typeof(SummaryReportFROMTableASExtent));
                Assert.AreEqual(nameof(SummaryReportFROMTableASExtent), tableName.Name);

                tableName = ctx.GetTableName(typeof(DetailReportWithFROM));
                Assert.AreEqual("In # Some.Complex_Schema @Name", tableName.Schema);
                Assert.AreEqual("SELECT WORSE FROM NAMES AS Extent1", tableName.Name);
            }
        }
    }
}
