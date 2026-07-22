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

using System;
using System.Data.Entity.Infrastructure;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.Common.Sql;
using Tanneryd.BulkOperations.EF6.Model;
using Tanneryd.BulkOperations.EF6.NET48.Tests.Models.DM.Prices;
using Tanneryd.BulkOperations.EF6.NET48.Tests.Models.EF;
using Tanneryd.BulkOperations.TestModels;

namespace Tanneryd.BulkOperations.EF6.NET48.Tests.Tests
{
    /// <summary>
    /// Ensures session-scoped staging temp tables are always dropped.
    /// Bulk insert/update create #temp tables for identity-key retrieval or
    /// UPDATE staging; leaks leave orphaned objects on the connection and can
    /// exhaust tempdb under load. Covers happy path and post-fill failure.
    /// </summary>
    [TestClass]
    public class TempTableCleanupTests : BulkOperationTestBase
    {
        [TestInitialize]
        public void Initialize()
        {
            InitializeUnitTestContext();
            CleanupUnitTestContext();
        }

        [TestCleanup]
        public void CleanUp()
        {
            CleanupUnitTestContext();
        }

        /// <summary>
        /// Happy path: BulkInsert with identity-key retrieval creates a staging
        /// temp table (MERGE OUTPUT / key round-trip) and must drop it before return.
        /// </summary>
        [TestMethod]
        public void BulkInsert_ShouldDropTempTable_AfterIdentityKeyRetrieval()
        {
            using (var scope = TempTableTracker.BeginScope())
            using (var db = new UnitTestContext())
            {
                db.BulkInsertAll(new BulkInsertRequest<Price>
                {
                    Entities = new[]
                    {
                        new Price { Date = new DateTime(2019, 1, 1), Name = "TempCleanup", Value = 1 },
                    },
                    EnableRecursiveInsert = EnableRecursiveInsert.NoButRetrieveGeneratedPrimaryKeys,
                });

                Assert.IsTrue(scope.Created > 0, "Expected a temp table to be created for identity retrieval.");
                Assert.AreEqual(
                    scope.Created,
                    scope.Dropped,
                    $"Expected every temp table to be dropped. Created={scope.Created}, Dropped={scope.Dropped}.");
            }
        }

        /// <summary>
        /// Failure path: the BulkUpdate staging temp table must still be dropped
        /// when the operation fails after FillTempTable. A stale rowversion forces
        /// a concurrency exception after the temp table exists (empty
        /// UpdatedPropertyNames is rejected before FillTempTable, so it cannot
        /// exercise this path).
        /// Owned concurrency transaction: dispose first, then drop with the
        /// caller transaction only (null here)—never the disposed owned txn.
        /// </summary>
        [TestMethod]
        public void BulkUpdate_WithOwnedConcurrencyTransaction_ShouldDropTempTable_OnConcurrencyFailure()
        {
            using (var db1 = new UnitTestContext())
            using (var db2 = new UnitTestContext())
            {
                var item = new ConcurrencyItem { Name = "Original" };
                db1.ConcurrencyItems.Add(item);
                db1.SaveChanges();

                var stale = db1.ConcurrencyItems.Single(x => x.Id == item.Id);

                var other = db2.ConcurrencyItems.Single(x => x.Id == item.Id);
                other.Name = "Changed by other writer";
                db2.Entry(other).Property(x => x.Name).IsModified = true;
                db2.SaveChanges();

                stale.Name = "Bulk overwrite";

                using (var txScope = SqlTransactionTracker.BeginScope())
                using (var tempScope = TempTableTracker.BeginScope())
                {
                    Assert.ThrowsExactly<DbUpdateConcurrencyException>(() =>
                        db1.BulkUpdateAll(new BulkUpdateRequest
                        {
                            Entities = new[] { stale },
                            KeyPropertyNames = new[] { nameof(ConcurrencyItem.Id) },
                            UpdatedPropertyNames = new[] { nameof(ConcurrencyItem.Name) },
                        }));

                    Assert.IsTrue(txScope.Created > 0, "Expected an owned concurrency transaction.");
                    Assert.AreEqual(
                        txScope.Created,
                        txScope.Disposed,
                        $"Owned transaction must be disposed before/with cleanup. Created={txScope.Created}, Disposed={txScope.Disposed}.");
                    Assert.IsTrue(tempScope.Created > 0, "Expected a temp table to be created before the UPDATE failed.");
                    Assert.AreEqual(
                        tempScope.Created,
                        tempScope.Dropped,
                        $"Temp must be dropped after owned transaction dispose (caller txn only). Created={tempScope.Created}, Dropped={tempScope.Dropped}.");
                }
            }
        }

        /// <summary>
        /// Success path with an owned concurrency transaction: staging temp is
        /// created inside that txn; after Commit+Dispose, drop must still succeed
        /// using only request.Transaction (null)—not the disposed owned txn.
        /// </summary>
        [TestMethod]
        public void BulkUpdate_WithOwnedConcurrencyTransaction_ShouldDropTempTable_OnSuccess()
        {
            using (var db = new UnitTestContext())
            {
                var item = new ConcurrencyItem { Name = "Original" };
                db.ConcurrencyItems.Add(item);
                db.SaveChanges();

                var current = db.ConcurrencyItems.Single(x => x.Id == item.Id);
                current.Name = "Updated via bulk";

                using (var txScope = SqlTransactionTracker.BeginScope())
                using (var tempScope = TempTableTracker.BeginScope())
                {
                    db.BulkUpdateAll(new BulkUpdateRequest
                    {
                        Entities = new object[] { current },
                        KeyPropertyNames = new[] { nameof(ConcurrencyItem.Id) },
                        UpdatedPropertyNames = new[] { nameof(ConcurrencyItem.Name) },
                    });

                    Assert.IsTrue(txScope.Created > 0, "Expected an owned concurrency transaction.");
                    Assert.AreEqual(
                        txScope.Created,
                        txScope.Disposed,
                        $"Owned transaction must be disposed before/with cleanup. Created={txScope.Created}, Disposed={txScope.Disposed}.");
                    Assert.IsTrue(tempScope.Created > 0, "Expected a staging temp table for BulkUpdate.");
                    Assert.AreEqual(
                        tempScope.Created,
                        tempScope.Dropped,
                        $"Temp must be dropped after owned transaction dispose (caller txn only). Created={tempScope.Created}, Dropped={tempScope.Dropped}.");
                }
            }
        }
    }
}
