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
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.EFCore;
using Tanneryd.BulkOperations.EFCore.Tests.Models.EF.ShortNamedFk;

namespace Tanneryd.BulkOperations.EFCore.Tests.UnitTests.Mappings
{
    /// <summary>
    /// Regression for review finding H1: MappingsExtractor must store
    /// FromType/ToType as entity type Name (same as the filter), not ClrType.ToString().
    /// </summary>
    [TestClass]
    public class MappingsExtractorShortNameTests
    {
        private const string ConnectionString =
            @"data source=(localdb)\MSSQLLocalDB;initial catalog=Tanneryd.BulkOperations.EFCore.Tests.ShortNamedFk;persist security info=True;Integrated Security=SSPI;MultipleActiveResultSets=True;TrustServerCertificate=true";

        private static ShortNamedFkContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<ShortNamedFkContext>()
                .UseSqlServer(ConnectionString)
                .Options;
            return new ShortNamedFkContext(options);
        }

        [TestInitialize]
        public void Initialize()
        {
            using var ctx = CreateContext();
            ctx.Database.EnsureDeleted();
            ctx.Database.EnsureCreated();
        }

        [TestCleanup]
        public void Cleanup()
        {
            using var ctx = CreateContext();
            ctx.Database.EnsureDeleted();
        }

        [TestMethod]
        public void ForeignKeyMappings_ShouldBePopulated_WhenEntityTypeNameDiffersFromClrType()
        {
            using var ctx = CreateContext();

            var authorEntityType = ctx.Model.FindEntityType(ShortNamedFkContext.AuthorEntityName);
            Assert.IsNotNull(authorEntityType);
            Assert.AreEqual(typeof(Author), authorEntityType.ClrType);

            // Precondition: shared-type short Name vs namespace-qualified ClrType.ToString().
            // (EF Core 10 normal entity types use FullName for both, so UnitTestContext hides H1.)
            Assert.AreNotEqual(
                authorEntityType.ClrType.ToString(),
                authorEntityType.Name,
                "Precondition failed: entity type Name must differ from ClrType.ToString() to exercise H1.");
            Assert.AreEqual(ShortNamedFkContext.AuthorEntityName, authorEntityType.Name);

            var extractor = new MappingsExtractor(ctx);
            var authorMappings = extractor.GetMappings(typeof(Author));
            var bookMappings = extractor.GetMappings(typeof(Book));

            Assert.IsTrue(
                authorMappings.FromForeignKeyMappings.Any(m => m.NavigationPropertyName == nameof(Author.Books)),
                "Author (principal) should expose Books via FromForeignKeyMappings when Name != ClrType.ToString().");

            Assert.IsTrue(
                bookMappings.ToForeignKeyMappings.Any(m => m.NavigationPropertyName == nameof(Book.Author)),
                "Book (dependent) should expose Author via ToForeignKeyMappings when Name != ClrType.ToString().");
        }
    }
}
