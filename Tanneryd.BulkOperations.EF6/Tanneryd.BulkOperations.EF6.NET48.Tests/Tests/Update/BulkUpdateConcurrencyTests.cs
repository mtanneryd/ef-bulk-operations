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
using Tanneryd.BulkOperations.EF6.Model;
using Tanneryd.BulkOperations.EF6.NET48.Tests.Models.EF;
using Tanneryd.BulkOperations.TestModels;

namespace Tanneryd.BulkOperations.EF6.NET48.Tests.Tests.Update
{
    /// <summary>
    /// Regression for GitHub issue #34: BulkUpdate must honour rowversion /
    /// concurrency tokens the same way SaveChanges does.
    /// Also covers: concurrency updates must not partially commit when the
    /// provider cannot own a Microsoft.Data.SqlClient transaction (legacy SqlClient).
    /// </summary>
    [TestClass]
    public class BulkUpdateConcurrencyTests : BulkOperationTestBase
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

        [TestMethod]
        public void SaveChanges_ShouldThrow_WhenRowVersionIsStale()
        {
            using (var db1 = new UnitTestContext())
            using (var db2 = new UnitTestContext())
            {
                var item = new ConcurrencyItem { Name = "Original" };
                db1.ConcurrencyItems.Add(item);
                db1.SaveChanges();

                Assert.IsNotNull(item.RowVersion, "Expected SQL Server to return a RowVersion after insert.");

                var stale = db1.ConcurrencyItems.Single(x => x.Id == item.Id);

                var other = db2.ConcurrencyItems.Single(x => x.Id == item.Id);
                other.Name = "Changed by other writer";
                db2.SaveChanges();

                stale.Name = "SaveChanges attempt";
                Assert.ThrowsExactly<DbUpdateConcurrencyException>(() => db1.SaveChanges());
            }
        }

        [TestMethod]
        public void BulkUpdate_ShouldNotOverwrite_WhenRowVersionIsStale()
        {
            using (var db1 = new UnitTestContext())
            using (var db2 = new UnitTestContext())
            {
                var item = new ConcurrencyItem { Name = "Original" };
                db1.ConcurrencyItems.Add(item);
                db1.SaveChanges();

                Assert.IsNotNull(item.RowVersion, "Expected SQL Server to return a RowVersion after insert.");

                var stale = db1.ConcurrencyItems.Single(x => x.Id == item.Id);

                var other = db2.ConcurrencyItems.Single(x => x.Id == item.Id);
                other.Name = "Changed by other writer";
                db2.SaveChanges();

                stale.Name = "Bulk overwrite";
                Assert.ThrowsExactly<DbUpdateConcurrencyException>(() =>
                    db1.BulkUpdateAll(new BulkUpdateRequest
                    {
                        Entities = new[] { stale },
                        KeyPropertyNames = new[] { nameof(ConcurrencyItem.Id) },
                        UpdatedPropertyNames = new[] { nameof(ConcurrencyItem.Name) },
                    }));

                using (var verify = new UnitTestContext())
                {
                    var fromDb = verify.ConcurrencyItems.Single(x => x.Id == item.Id);
                    Assert.AreEqual(
                        "Changed by other writer",
                        fromDb.Name,
                        "Stale BulkUpdate must not overwrite a row whose RowVersion has changed (issue #34).");
                }
            }
        }

        [TestMethod]
        public void BulkUpdate_ShouldSucceed_WhenRowVersionIsCurrent()
        {
            using (var db = new UnitTestContext())
            {
                var item = new ConcurrencyItem { Name = "Original" };
                db.ConcurrencyItems.Add(item);
                db.SaveChanges();

                var current = db.ConcurrencyItems.Single(x => x.Id == item.Id);
                current.Name = "Updated via bulk";
                db.BulkUpdateAll(new BulkUpdateRequest
                {
                    Entities = new[] { current },
                    KeyPropertyNames = new[] { nameof(ConcurrencyItem.Id) },
                    UpdatedPropertyNames = new[] { nameof(ConcurrencyItem.Name) },
                });

                using (var verify = new UnitTestContext())
                {
                    Assert.AreEqual(
                        "Updated via bulk",
                        verify.ConcurrencyItems.Single(x => x.Id == item.Id).Name);
                }
            }
        }

        /// <summary>
        /// Non-unique KeyPropertyNames with concurrency tokens can update many
        /// target rows per entity and false-fire DbUpdateConcurrencyException.
        /// Require the primary key (or empty KeyPropertyNames) instead.
        /// </summary>
        [TestMethod]
        public void BulkUpdate_WithConcurrencyToken_ShouldRejectNonUniqueKeyPropertyNames()
        {
            using (var db = new UnitTestContext())
            {
                var item = new ConcurrencyItem { Name = "Original" };
                db.ConcurrencyItems.Add(item);
                db.SaveChanges();

                var current = db.ConcurrencyItems.Single(x => x.Id == item.Id);
                current.Name = "Updated";

                var ex = Assert.ThrowsExactly<ArgumentException>(() =>
                    db.BulkUpdateAll(new BulkUpdateRequest
                    {
                        Entities = new[] { current },
                        KeyPropertyNames = new[] { nameof(ConcurrencyItem.Name) },
                        UpdatedPropertyNames = new[] { nameof(ConcurrencyItem.Name) },
                    }));

                StringAssert.Contains(ex.Message, "primary key", StringComparison.OrdinalIgnoreCase);
                StringAssert.Contains(ex.Message, "concurrency", StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// InsertIfNew row counts must not be folded into the concurrency check
        /// incorrectly: one successful update plus one insert is not a conflict.
        /// </summary>
        [TestMethod]
        public void BulkUpdate_InsertIfNew_WithConcurrencyToken_ShouldSucceed_WhenUpdatingOneAndInsertingOne()
        {
            using (var db = new UnitTestContext())
            {
                var item = new ConcurrencyItem { Name = "Existing" };
                db.ConcurrencyItems.Add(item);
                db.SaveChanges();

                var current = db.ConcurrencyItems.Single(x => x.Id == item.Id);
                current.Name = "Existing-updated";

                db.BulkUpdateAll(new BulkUpdateRequest
                {
                    Entities = new object[]
                    {
                        current,
                        new ConcurrencyItem { Name = "Brand-new" },
                    },
                    KeyPropertyNames = new[] { nameof(ConcurrencyItem.Id) },
                    UpdatedPropertyNames = new[] { nameof(ConcurrencyItem.Name) },
                    InsertIfNew = true,
                });
            }

            using (var verify = new UnitTestContext())
            {
                Assert.AreEqual(2, verify.ConcurrencyItems.Count());
                Assert.AreEqual(
                    "Existing-updated",
                    verify.ConcurrencyItems.Single(x => x.Name == "Existing-updated").Name);
                Assert.IsTrue(verify.ConcurrencyItems.Any(x => x.Name == "Brand-new"));
            }
        }

        /// <summary>
        /// Microsoft.Data.SqlClient path: BulkUpdate auto-begins a transaction when
        /// concurrency tokens are present. A mixed stale/current batch must roll back
        /// entirely — the current row must not stay updated after the concurrency throw.
        /// </summary>
        [TestMethod]
        public void BulkUpdate_ShouldNotPartiallyCommit_WhenMixedStaleAndCurrentRowVersions()
        {
            int currentId;
            int staleId;

            using (var seed = new UnitTestContext())
            {
                var current = new ConcurrencyItem { Name = "Current-original" };
                var stale = new ConcurrencyItem { Name = "Stale-original" };
                seed.ConcurrencyItems.Add(current);
                seed.ConcurrencyItems.Add(stale);
                seed.SaveChanges();
                currentId = current.Id;
                staleId = stale.Id;
            }

            using (var db1 = new UnitTestContext())
            using (var db2 = new UnitTestContext())
            {
                var current = db1.ConcurrencyItems.Single(x => x.Id == currentId);
                var stale = db1.ConcurrencyItems.Single(x => x.Id == staleId);

                // Advance only the stale row's rowversion from another context.
                var other = db2.ConcurrencyItems.Single(x => x.Id == staleId);
                other.Name = "Stale-changed-by-other";
                db2.SaveChanges();

                current.Name = "Current-bulk";
                stale.Name = "Stale-bulk";

                Assert.ThrowsExactly<DbUpdateConcurrencyException>(() =>
                    db1.BulkUpdateAll(new BulkUpdateRequest
                    {
                        Entities = new[] { current, stale },
                        KeyPropertyNames = new[] { nameof(ConcurrencyItem.Id) },
                        UpdatedPropertyNames = new[] { nameof(ConcurrencyItem.Name) },
                    }));
            }

            using (var verify = new UnitTestContext())
            {
                Assert.AreEqual(
                    "Current-original",
                    verify.ConcurrencyItems.Single(x => x.Id == currentId).Name,
                    "Current row must roll back with the failed concurrency batch (owned transaction).");
                Assert.AreEqual(
                    "Stale-changed-by-other",
                    verify.ConcurrencyItems.Single(x => x.Id == staleId).Name,
                    "Stale row must keep the other writer's value.");
            }
        }

        /// <summary>
        /// On System.Data.SqlClient, BulkUpdate cannot own a Microsoft.Data.SqlClient
        /// transaction, and request.Transaction cannot be used either. Without a guard,
        /// a mixed stale/current batch auto-commits the matching UPDATE rows before the
        /// concurrency exception. Refuse the operation instead of partially committing.
        /// </summary>
        [TestMethod]
        public void BulkUpdate_WithConcurrencyTokens_OnLegacySqlClient_ShouldRefuseRatherThanPartiallyCommit()
        {
            using (var probe = new LegacyUnitTestContext())
            {
                Assert.IsInstanceOfType(
                    probe.Database.Connection,
                    typeof(System.Data.SqlClient.SqlConnection),
                    "LegacyUnitTestContext must use System.Data.SqlClient for this regression.");
            }

            int currentId;
            int staleId;

            using (var seed = new UnitTestContext())
            {
                var current = new ConcurrencyItem { Name = "Legacy-current-original" };
                var stale = new ConcurrencyItem { Name = "Legacy-stale-original" };
                seed.ConcurrencyItems.Add(current);
                seed.ConcurrencyItems.Add(stale);
                seed.SaveChanges();
                currentId = current.Id;
                staleId = stale.Id;
            }

            using (var db1 = new LegacyUnitTestContext())
            using (var db2 = new UnitTestContext())
            {
                var current = db1.ConcurrencyItems.Single(x => x.Id == currentId);
                var stale = db1.ConcurrencyItems.Single(x => x.Id == staleId);

                var other = db2.ConcurrencyItems.Single(x => x.Id == staleId);
                other.Name = "Legacy-stale-changed-by-other";
                db2.SaveChanges();

                current.Name = "Legacy-current-bulk";
                stale.Name = "Legacy-stale-bulk";

                var ex = Assert.ThrowsExactly<NotSupportedException>(() =>
                    db1.BulkUpdateAll(new BulkUpdateRequest
                    {
                        Entities = new[] { current, stale },
                        KeyPropertyNames = new[] { nameof(ConcurrencyItem.Id) },
                        UpdatedPropertyNames = new[] { nameof(ConcurrencyItem.Name) },
                    }));

                StringAssert.Contains(ex.Message, "System.Data.SqlClient");
                StringAssert.Contains(ex.Message, "concurrency");
            }

            using (var verify = new UnitTestContext())
            {
                Assert.AreEqual(
                    "Legacy-current-original",
                    verify.ConcurrencyItems.Single(x => x.Id == currentId).Name,
                    "Legacy path must not partially commit the current row (H1).");
                Assert.AreEqual(
                    "Legacy-stale-changed-by-other",
                    verify.ConcurrencyItems.Single(x => x.Id == staleId).Name);
            }
        }
    }
}
