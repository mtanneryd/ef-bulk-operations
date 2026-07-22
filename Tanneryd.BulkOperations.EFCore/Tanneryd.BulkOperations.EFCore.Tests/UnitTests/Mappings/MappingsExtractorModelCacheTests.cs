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
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.EFCore.Model;
using Tanneryd.BulkOperations.EFCore.Tests.Models.EF.VariantModel;

namespace Tanneryd.BulkOperations.EFCore.Tests.UnitTests.Mappings
{
    /// <summary>
    /// MappingsExtractor must not be cached solely by DbContext CLR type when the
    /// same class can have different IModel instances (table names, options, etc.).
    /// </summary>
    [TestClass]
    public class MappingsExtractorModelCacheTests
    {
        private const string ConnectionString =
            @"data source=(localdb)\MSSQLLocalDB;initial catalog=Tanneryd.BulkOperations.EFCore.Tests.VariantModel;persist security info=True;Integrated Security=SSPI;MultipleActiveResultSets=True;TrustServerCertificate=true";

        private const string TableA = "WidgetA";
        private const string TableB = "WidgetB";

        private static VariantTableContext CreateContext(string tableName)
        {
            var options = new DbContextOptionsBuilder<VariantTableContext>()
                .UseSqlServer(ConnectionString)
                .ReplaceService<IModelCacheKeyFactory, VariantTableModelCacheKeyFactory>()
                .Options;
            return new VariantTableContext(options, tableName);
        }

        [TestInitialize]
        public void Initialize()
        {
            using var a = CreateContext(TableA);
            a.Database.EnsureDeleted();
            a.Database.EnsureCreated();

            using var b = CreateContext(TableB);
            b.Database.ExecuteSqlRaw($@"
IF OBJECT_ID(N'[dbo].[{TableB}]', N'U') IS NULL
CREATE TABLE [dbo].[{TableB}](
    [Id] int NOT NULL IDENTITY(1,1),
    [Name] nvarchar(max) NULL,
    CONSTRAINT [PK_{TableB}] PRIMARY KEY ([Id]));");
        }

        [TestCleanup]
        public void Cleanup()
        {
            using var a = CreateContext(TableA);
            a.Database.EnsureDeleted();
        }

        [TestMethod]
        public void BulkInsert_ShouldUseCurrentContextModel_WhenSameContextTypeHasDifferentTables()
        {
            using (var a = CreateContext(TableA))
            {
                Assert.AreEqual(TableA, a.Model.FindEntityType(typeof(Widget))!.GetTableName());

                a.BulkInsertAll(new BulkInsertRequest<Widget>
                {
                    Entities = new[] { new Widget { Name = "in-A" } },
                    EnableRecursiveInsert = EnableRecursiveInsert.NoButRetrieveGeneratedPrimaryKeys,
                });
            }

            using (var b = CreateContext(TableB))
            {
                Assert.AreEqual(
                    TableB,
                    b.Model.FindEntityType(typeof(Widget))!.GetTableName(),
                    "Precondition failed: EF must build a distinct model for TableB.");

                b.BulkInsertAll(new BulkInsertRequest<Widget>
                {
                    Entities = new[] { new Widget { Name = "in-B" } },
                    EnableRecursiveInsert = EnableRecursiveInsert.NoButRetrieveGeneratedPrimaryKeys,
                });
            }

            using (var a = CreateContext(TableA))
            using (var b = CreateContext(TableB))
            {
                Assert.AreEqual(
                    1,
                    a.Widgets.Count(),
                    "WidgetA must keep only its own row; a CLR-type-keyed extractor cache would send the second insert here too.");
                Assert.AreEqual("in-A", a.Widgets.Single().Name);

                Assert.AreEqual(
                    1,
                    b.Widgets.Count(),
                    "WidgetB must receive the second insert; stale mappings from WidgetA leave this table empty.");
                Assert.AreEqual("in-B", b.Widgets.Single().Name);
            }
        }
    }
}
