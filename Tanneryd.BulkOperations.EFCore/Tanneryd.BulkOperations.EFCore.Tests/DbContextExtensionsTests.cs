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

using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.EFCore.Tests.UnitTests;


namespace Tanneryd.BulkOperations.EFCore.Tests
{
    [TestClass]
    public class DbContextExtensionsTests : BulkOperationTestBase
    {
        [TestInitialize]
        public void Initialize()
        {
            InitializeUnitTestContext();
        }

        [TestMethod]
        public void SchoolTableNames()
        {
            using (var ctx = Factory.CreateDbContext())
            {
                var timeout = ctx.Database.GetCommandTimeout();
                var extractor = new MappingsExtractor(ctx);
                extractor.GetMappings(typeof(Course));
                var tableName = extractor.GetTableName(ctx, typeof(Course));
                Assert.AreEqual("dbo", tableName.Schema);
                Assert.AreEqual("Course", tableName.Name);

                tableName = extractor.GetTableName(ctx, typeof(Department));
                Assert.AreEqual("dbo", tableName.Schema);
                Assert.AreEqual("Department", tableName.Name);

                tableName = extractor.GetTableName(ctx, typeof(Instructor));
                Assert.AreEqual("dbo", tableName.Schema);
                Assert.AreEqual("Instructor", tableName.Name);

                tableName = extractor.GetTableName(ctx, typeof(OfficeAssignment));
                Assert.AreEqual("dbo", tableName.Schema);
                Assert.AreEqual("OfficeAssignment", tableName.Name);
            }
        }

        [TestMethod]
        public void SkipViewsInMappingExtractor()
        {
            using (var ctx = Factory.CreateDbContext())
            {
                var extractor = new MappingsExtractor(ctx);
                var hasMappings = extractor.HasMappings(typeof(Contact));
                Assert.IsFalse(hasMappings);
            }
        }
    }
}
