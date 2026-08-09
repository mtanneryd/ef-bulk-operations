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
using Tanneryd.BulkOperations.EF6;
using Tanneryd.BulkOperations.EF6.NET48.Tests.Models.EF.ComplexCollision;

namespace Tanneryd.BulkOperations.EF6.NET48.Tests.Tests.Mappings
{
    /// <summary>
    /// Regression for 9edb0ca: complex-type leaves must be keyed by store column
    /// name so sibling complex properties that share a leaf CLR name
    /// (Home.Street / Work.Street) remain unique in ColumnMappingByPropertyName.
    /// Requires LocalDB (EF6 MetadataWorkspace is built against the store model).
    /// </summary>
    [TestClass]
    public class MappingsExtractorComplexLeafCollisionTests
    {
        [TestInitialize]
        public void Initialize()
        {
            using (var ctx = new ComplexCollisionContext())
            {
                if (ctx.Database.Exists())
                    ctx.Database.Delete();
                ctx.Database.Create();
            }
        }

        [TestCleanup]
        public void Cleanup()
        {
            using (var ctx = new ComplexCollisionContext())
            {
                if (ctx.Database.Exists())
                    ctx.Database.Delete();
            }
        }

        [TestMethod]
        public void SiblingComplexLeaves_ShouldBeKeyedByStoreColumnName()
        {
            using (var ctx = new ComplexCollisionContext())
            {
                var mappings = new MappingsExtractor().GetMappings(ctx, typeof(ComplexCollisionContact));

                Assert.IsTrue(mappings.ColumnMappingByPropertyName.ContainsKey("Home_Street"));
                Assert.IsTrue(mappings.ColumnMappingByPropertyName.ContainsKey("Work_Street"));
                Assert.IsFalse(
                    mappings.ColumnMappingByPropertyName.ContainsKey("Street"),
                    "Keying by leaf CLR name would collide Home.Street with Work.Street.");

                Assert.AreEqual(
                    "Home_Street",
                    mappings.ColumnMappingByPropertyName["Home_Street"].TableColumn.Name);
                Assert.AreEqual(
                    "Work_Street",
                    mappings.ColumnMappingByPropertyName["Work_Street"].TableColumn.Name);
            }
        }

        [TestMethod]
        public void ComplexLeafColumnNameByPath_ShouldMapNavigationPathToStoreColumn()
        {
            using (var ctx = new ComplexCollisionContext())
            {
                var mappings = new MappingsExtractor().GetMappings(ctx, typeof(ComplexCollisionContact));

                Assert.AreEqual("Home_Street", mappings.ComplexLeafColumnNameByPath["Home.Street"]);
                Assert.AreEqual("Home_City", mappings.ComplexLeafColumnNameByPath["Home.City"]);
                Assert.AreEqual("Work_Street", mappings.ComplexLeafColumnNameByPath["Work.Street"]);
                Assert.AreEqual("Work_City", mappings.ComplexLeafColumnNameByPath["Work.City"]);
                Assert.AreEqual(4, mappings.ComplexLeafColumnNameByPath.Count);
            }
        }

        [TestMethod]
        public void ComplexPropertyNames_ShouldListSiblingComplexRoots()
        {
            using (var ctx = new ComplexCollisionContext())
            {
                var mappings = new MappingsExtractor().GetMappings(ctx, typeof(ComplexCollisionContact));

                CollectionAssert.AreEquivalent(
                    new[] { nameof(ComplexCollisionContact.Home), nameof(ComplexCollisionContact.Work) },
                    mappings.ComplexPropertyNames.ToArray());
            }
        }
    }
}