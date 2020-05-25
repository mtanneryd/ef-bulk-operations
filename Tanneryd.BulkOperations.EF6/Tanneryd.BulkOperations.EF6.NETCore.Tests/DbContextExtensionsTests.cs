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
using Tanneryd.BulkOperations.EF6.NETCore.Tests.Models.DM.Report;
using Tanneryd.BulkOperations.EF6.NETCore.Tests.Models.DM.School;
using Tanneryd.BulkOperations.EF6.NETCore.Tests.Models.EF;

namespace Tanneryd.BulkOperations.EF6.NETCore.Tests
{
    [TestClass]
    public class DbContextExtensionsTests
    {
        [TestMethod]
        public void TableNameForGitHubIssue17ShouldPass()
        {
            var sql = @"SELECT [Extent1].[Id] AS [Id], [Extent1].[Identifier] AS [Identifier], [Extent1].[Name] AS [Name], [Extent1].[Latitude] AS [Latitude], [Extent1].[Longitude] AS [Longitude], [Extent1].[FIR] AS [FIR], [Extent1].[UIR] AS [UIR], [Extent1].[ICAO] AS [ICAO], [Extent1].[MagneticVariation] AS [MagneticVariation], [Extent1].[Frequency] AS [Frequency], [Extent1].[CountryName] AS [CountryName], [Extent1].[CountryId] AS [CountryId], [Extent1].[StateId] AS [StateId], [Extent1].[CityId] AS [CityId], [Extent1].[IsActive] AS [IsActive] FROM [dbo].[Points] AS [Extent1] WHERE ([Extent1].[IsActive] = @DynamicFilterParam_000001) OR (@DynamicFilterParam_000002 IS NOT NULL)";
            var tableName = new MappingsExtractor().ParseTableName(sql);
            Assert.AreEqual("Points", tableName.Name);
            Assert.AreEqual("dbo", tableName.Schema);
        }


        [TestMethod]
        public void SchoolTableNames()
        {
            using (var ctx = new UnitTestContext())
            {
                var extractor = new MappingsExtractor();
                var tableName = extractor.GetTableName(ctx,typeof(Course));
                Assert.AreEqual("dbo", tableName.Schema);
                Assert.AreEqual("Course", tableName.Name);

                tableName = extractor.GetTableName(ctx,typeof(Department));
                Assert.AreEqual("dbo", tableName.Schema);
                Assert.AreEqual("Department", tableName.Name);

                tableName = extractor.GetTableName(ctx,typeof(Instructor));
                Assert.AreEqual("dbo", tableName.Schema);
                Assert.AreEqual("Instructor", tableName.Name);

                tableName = extractor.GetTableName(ctx,typeof(OfficeAssignment));
                Assert.AreEqual("dbo", tableName.Schema);
                Assert.AreEqual("OfficeAssignment", tableName.Name);
            }
        }
        
        [TestMethod]
        public void ReportTableNames()
        {
            using (var ctx = new UnitTestContext())
            {
                var extractor = new MappingsExtractor();

                var tableName = extractor.GetTableName(ctx,typeof(Period));
                Assert.AreEqual("Some.Complex_Schema Name", tableName.Schema);
                Assert.AreEqual("Period", tableName.Name);

                tableName = extractor.GetTableName(ctx,typeof(SummaryReportFROMTableASExtent));
                Assert.AreEqual(nameof(SummaryReportFROMTableASExtent), tableName.Name);

                tableName = extractor.GetTableName(ctx,typeof(DetailReportWithFROM));
                Assert.AreEqual("In # Some.Complex_Schema @Name", tableName.Schema);
                Assert.AreEqual("SELECT WORSE FROM NAMES AS Extent1", tableName.Name);
            }
        }
    }
}
