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

using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.EFCore.Tests.Models.EF.ComplexCollision;

namespace Tanneryd.BulkOperations.EFCore.Tests.UnitTests.Mappings
{
    /// <summary>
    /// Regression for 9edb0ca: complex-type leaves must be keyed by store column
    /// name so sibling complex properties that share a leaf CLR name
    /// (Home.Street / Work.Street) remain unique in ColumnMappingByPropertyName.
    /// Model-only — no database connection required.
    /// </summary>
    [TestClass]
    public class MappingsExtractorComplexLeafCollisionTests
    {
        private const string DummyConnectionString =
            @"data source=(localdb)\MSSQLLocalDB;initial catalog=MappingsExtractorComplexLeafCollisionTests;Integrated Security=SSPI;TrustServerCertificate=true";

        private static readonly DbContextOptions<ComplexCollisionContext> Options =
            new DbContextOptionsBuilder<ComplexCollisionContext>()
                .UseSqlServer(DummyConnectionString)
                .Options;

        /// <summary>
        /// Stand-in for a lazy-loading / change-tracking proxy whose runtime type
        /// is not itself in the EF model.
        /// </summary>
        [NotMapped]
        private sealed class ComplexCollisionContactProxy : ComplexCollisionContact
        {
        }

        [TestMethod]
        public void SiblingComplexLeaves_ShouldBeKeyedByStoreColumnName()
        {
            using var ctx = new ComplexCollisionContext(Options);
            var mappings = new MappingsExtractor(ctx).GetMappings(typeof(ComplexCollisionContact));

            Assert.IsTrue(mappings.ColumnMappingByPropertyName.ContainsKey("Home_Street"));
            Assert.IsTrue(mappings.ColumnMappingByPropertyName.ContainsKey("Work_Street"));
            Assert.IsFalse(
                mappings.ColumnMappingByPropertyName.ContainsKey("Street"),
                "Keying by leaf CLR name would collide Home.Street with Work.Street.");

            Assert.AreEqual(
                "Home_Street",
                mappings.ColumnMappingByPropertyName["Home_Street"].TableColumn.Column.Name);
            Assert.AreEqual(
                "Work_Street",
                mappings.ColumnMappingByPropertyName["Work_Street"].TableColumn.Column.Name);
        }

        [TestMethod]
        public void ComplexLeafColumnNameByPath_ShouldMapNavigationPathToStoreColumn()
        {
            using var ctx = new ComplexCollisionContext(Options);
            var mappings = new MappingsExtractor(ctx).GetMappings(typeof(ComplexCollisionContact));

            Assert.AreEqual("Home_Street", mappings.ComplexLeafColumnNameByPath["Home.Street"]);
            Assert.AreEqual("Home_City", mappings.ComplexLeafColumnNameByPath["Home.City"]);
            Assert.AreEqual("Work_Street", mappings.ComplexLeafColumnNameByPath["Work.Street"]);
            Assert.AreEqual("Work_City", mappings.ComplexLeafColumnNameByPath["Work.City"]);
            Assert.AreEqual(4, mappings.ComplexLeafColumnNameByPath.Count);
        }

        [TestMethod]
        public void ComplexPropertyNames_ShouldListSiblingComplexRoots()
        {
            using var ctx = new ComplexCollisionContext(Options);
            var mappings = new MappingsExtractor(ctx).GetMappings(typeof(ComplexCollisionContact));

            CollectionAssert.AreEquivalent(
                new[] { nameof(ComplexCollisionContact.Home), nameof(ComplexCollisionContact.Work) },
                mappings.ComplexPropertyNames.ToArray());
        }

        [TestMethod]
        public void ResolveMappedClrType_ShouldUnwrapUnmappedProxySubclass()
        {
            using var ctx = new ComplexCollisionContext(Options);
            var extractor = new MappingsExtractor(ctx);

            Assert.AreEqual(
                typeof(ComplexCollisionContact),
                extractor.ResolveMappedClrType(typeof(ComplexCollisionContactProxy)),
                "Proxy / unmapped subclass CLR types must resolve to the mapped entity type.");
            Assert.AreEqual(
                typeof(ComplexCollisionContact),
                extractor.ResolveMappedClrType(typeof(ComplexCollisionContact)));
            Assert.AreEqual(
                typeof(string),
                extractor.ResolveMappedClrType(typeof(string)),
                "Unknown types must pass through unchanged.");
            Assert.IsNotNull(extractor.GetMappings(typeof(ComplexCollisionContactProxy)));
        }
    }
}
