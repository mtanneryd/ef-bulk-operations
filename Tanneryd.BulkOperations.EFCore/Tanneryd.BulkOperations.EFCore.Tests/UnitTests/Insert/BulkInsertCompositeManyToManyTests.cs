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
using Tanneryd.BulkOperations.EFCore.Tests.Models.EF.CompositeManyToMany;

namespace Tanneryd.BulkOperations.EFCore.Tests.UnitTests.Insert
{
    /// <summary>
    /// Recursive M2M insert must write every join-table column when one end
    /// uses a composite principal key.
    /// </summary>
    [TestClass]
    public class BulkInsertCompositeManyToManyTests
    {
        private static CompositeManyToManyContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<CompositeManyToManyContext>()
                .UseSqlServer(CompositeManyToManyContext.ConnectionString)
                .Options;
            return new CompositeManyToManyContext(options);
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
        public void BulkInsert_RecursiveManyToMany_ShouldWriteAllCompositeJoinColumns()
        {
            using var db = CreateContext();

            var item = new TenantItem
            {
                TenantId = 7,
                ItemId = 42,
                Name = "widget",
            };
            item.Labels.Add(new ItemLabel { Text = "hot" });

            db.BulkInsertAll(new BulkInsertRequest<TenantItem>
            {
                Entities = new[] { item },
                EnableRecursiveInsert = EnableRecursiveInsert.Yes,
                AllowNotNullSelfReferences = AllowNotNullSelfReferences.No,
            });

            Assert.AreEqual(1, db.TenantItems.Count());
            Assert.AreEqual(1, db.ItemLabels.Count());

            var joinRows = db.Database
                .SqlQueryRaw<CompositeJoinRow>(
                    "SELECT TenantId, ItemId, LabelId FROM [dbo].[TenantItemLabels]")
                .ToArray();
            Assert.AreEqual(1, joinRows.Length, "Join-table row for TenantItem↔ItemLabel must be inserted.");
            Assert.AreEqual(7, joinRows[0].TenantId);
            Assert.AreEqual(
                42,
                joinRows[0].ItemId,
                "Composite left-end join key must include ItemId, not only TenantId.");
            Assert.AreEqual(db.ItemLabels.Single().Id, joinRows[0].LabelId);
        }

        private class CompositeJoinRow
        {
            public int TenantId { get; set; }
            public int ItemId { get; set; }
            public int LabelId { get; set; }
        }
    }
}
