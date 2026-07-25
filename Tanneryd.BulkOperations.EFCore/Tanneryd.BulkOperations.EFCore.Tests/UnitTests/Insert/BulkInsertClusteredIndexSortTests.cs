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
using Tanneryd.BulkOperations.EFCore.Model;
using Tanneryd.BulkOperations.EFCore.Tests.Models.EF.ClusteredIndexSort;

namespace Tanneryd.BulkOperations.EFCore.Tests.UnitTests.Insert
{
    /// <summary>
    /// SortUsingClusteredIndex must not KeyNotFound when the clustered index
    /// includes concurrency/computed columns absent from ColumnMappingByColumnName.
    /// </summary>
    [TestClass]
    public class BulkInsertClusteredIndexSortTests
    {
        private static ClusteredIndexSortContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<ClusteredIndexSortContext>()
                .UseSqlServer(ClusteredIndexSortContext.ConnectionString)
                .Options;
            return new ClusteredIndexSortContext(options);
        }

        [TestInitialize]
        public void Initialize()
        {
            using var ctx = CreateContext();
            ctx.Database.EnsureDeleted();
            ctx.Database.EnsureCreated();
            // Default PK is clustered on Id only. Rebuild so the CI also includes
            // RowVersion (concurrency token, not in ColumnMappingByColumnName).
            ctx.Database.ExecuteSqlRaw(@"
DECLARE @pk sysname =
    (SELECT kc.name
     FROM sys.key_constraints kc
     WHERE kc.parent_object_id = OBJECT_ID(N'dbo.SortProbe') AND kc.type = N'PK');
EXEC(N'ALTER TABLE [dbo].[SortProbe] DROP CONSTRAINT [' + @pk + N']');
ALTER TABLE [dbo].[SortProbe] ADD CONSTRAINT [PK_SortProbe] PRIMARY KEY NONCLUSTERED ([Id]);
CREATE CLUSTERED INDEX [CX_SortProbe] ON [dbo].[SortProbe] ([Id], [RowVersion]);

DECLARE @pk2 sysname =
    (SELECT kc.name
     FROM sys.key_constraints kc
     WHERE kc.parent_object_id = OBJECT_ID(N'dbo.ComplexSortProbe') AND kc.type = N'PK');
EXEC(N'ALTER TABLE [dbo].[ComplexSortProbe] DROP CONSTRAINT [' + @pk2 + N']');
ALTER TABLE [dbo].[ComplexSortProbe] ADD CONSTRAINT [PK_ComplexSortProbe] PRIMARY KEY NONCLUSTERED ([Id]);
CREATE CLUSTERED INDEX [CX_ComplexSortProbe] ON [dbo].[ComplexSortProbe] ([City]);");
        }

        [TestCleanup]
        public void Cleanup()
        {
            using var ctx = CreateContext();
            ctx.Database.EnsureDeleted();
        }

        [TestMethod]
        public void BulkInsert_WithSortUsingClusteredIndex_ShouldSucceed_WhenClusteredIndexIncludesRowVersion()
        {
            using var db = CreateContext();

            db.BulkInsertAll(new BulkInsertRequest<SortProbe>
            {
                Entities = new[]
                {
                    new SortProbe { Name = "bravo" },
                    new SortProbe { Name = "alpha" },
                },
                SortUsingClusteredIndex = true,
            });

            Assert.AreEqual(2, db.SortProbes.Count());
            CollectionAssert.AreEquivalent(
                new[] { "alpha", "bravo" },
                db.SortProbes.Select(p => p.Name).ToArray());
        }

        /// <summary>
        /// Clustered-index columns mapped from complex types expose leaf property
        /// names (e.g. City) that are not on the root CLR type. Sorting must skip
        /// them instead of NullReferenceException from GetProperty(...).GetValue.
        /// </summary>
        [TestMethod]
        public void BulkInsert_WithSortUsingClusteredIndex_ShouldSucceed_WhenClusteredIndexIncludesComplexTypeColumn()
        {
            using var db = CreateContext();

            db.BulkInsertAll(new BulkInsertRequest<ComplexSortProbe>
            {
                Entities = new[]
                {
                    new ComplexSortProbe
                    {
                        Name = "bravo",
                        Location = new SortLocation { City = "Zurich" },
                    },
                    new ComplexSortProbe
                    {
                        Name = "alpha",
                        Location = new SortLocation { City = "Amsterdam" },
                    },
                },
                SortUsingClusteredIndex = true,
            });

            Assert.AreEqual(2, db.ComplexSortProbes.Count());
            CollectionAssert.AreEquivalent(
                new[] { "alpha", "bravo" },
                db.ComplexSortProbes.Select(p => p.Name).ToArray());
            CollectionAssert.AreEquivalent(
                new[] { "Amsterdam", "Zurich" },
                db.ComplexSortProbes.Select(p => p.Location.City).ToArray());
        }
    }
}
