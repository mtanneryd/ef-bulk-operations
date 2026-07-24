/*
* Copyright ©  2017-2026 Tånneryd IT AB
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

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.EFCore;

namespace Tanneryd.BulkOperations.EFCore.Tests.UnitTests.Mappings
{
    /// <summary>
    /// Regression for M8: EF Core MappingsExtractor must populate
    /// <c>AssociationMapping</c> for skip-navigation many-to-many
    /// (same role as EF6 AssociationSetMappings), so recursive bulk insert
    /// can write join-table rows via the Expando path.
    /// </summary>
    [TestClass]
    public class MappingsExtractorManyToManyTests : BulkOperationTestBase
    {
        [TestInitialize]
        public void Initialize()
        {
            InitializeUnitTestContext();
        }

        [TestCleanup]
        public void CleanUp()
        {
            CleanupUnitTestContext();
        }

        [TestMethod]
        public void SkipNavigation_ManyToMany_ShouldPopulateAssociationMapping()
        {
            using var db = Factory.CreateDbContext();
            var extractor = new MappingsExtractor(db);

            var postMappings = extractor.GetMappings(typeof(Post));
            var visitorMappings = extractor.GetMappings(typeof(Visitor));

            var postVisitors = postMappings.FromForeignKeyMappings
                .Concat(postMappings.ToForeignKeyMappings)
                .SingleOrDefault(m => m.NavigationPropertyName == nameof(Post.Visitors));

            Assert.IsNotNull(
                postVisitors,
                "Post.Visitors skip navigation must appear in foreign-key mappings.");
            Assert.IsTrue(postVisitors.IsCollection);
            Assert.IsNotNull(
                postVisitors.AssociationMapping,
                "Post.Visitors must have AssociationMapping so join rows can be bulk-copied.");
            Assert.AreEqual("VisitorPosts", postVisitors.AssociationMapping.TableName.Name);
            Assert.AreEqual("dbo", postVisitors.AssociationMapping.TableName.Schema);
            Assert.IsNotNull(postVisitors.AssociationMapping.Sources);
            Assert.IsNotNull(postVisitors.AssociationMapping.Targets);
            Assert.AreEqual(1, postVisitors.AssociationMapping.Sources.Length);
            Assert.AreEqual(1, postVisitors.AssociationMapping.Targets.Length);

            var visitorPosts = visitorMappings.FromForeignKeyMappings
                .Concat(visitorMappings.ToForeignKeyMappings)
                .SingleOrDefault(m => m.NavigationPropertyName == nameof(Visitor.Posts));

            Assert.IsNotNull(
                visitorPosts,
                "Visitor.Posts skip navigation must appear in foreign-key mappings.");
            Assert.IsNotNull(
                visitorPosts.AssociationMapping,
                "Visitor.Posts must have AssociationMapping so join rows can be bulk-copied.");
            Assert.AreEqual("VisitorPosts", visitorPosts.AssociationMapping.TableName.Name);
        }
    }
}
