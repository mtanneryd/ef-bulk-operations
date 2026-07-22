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
using Tanneryd.BulkOperations.Common.Sql;
using Tanneryd.BulkOperations.EFCore.Model;
using Tanneryd.BulkOperations.TestModels;

namespace Tanneryd.BulkOperations.EFCore.Tests.UnitTests.Update
{
    /// <summary>
    /// Owned concurrency BulkUpdate transactions must be Disposed after
    /// Commit/Rollback. Nulling the local before finally skips Dispose and
    /// leaks the SqlTransaction.
    /// </summary>
    [TestClass]
    public class OwnedConcurrencyTransactionDisposeTests : BulkOperationTestBase
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
        public void BulkUpdate_WithConcurrencyTokens_ShouldDisposeOwnedTransaction_OnSuccess()
        {
            using var db = Factory.CreateDbContext();
            var item = new ConcurrencyItem { Name = "Original" };
            db.ConcurrencyItems.Add(item);
            db.SaveChanges();

            var current = db.ConcurrencyItems.Single(x => x.Id == item.Id);
            current.Name = "Updated via bulk";

            using var scope = SqlTransactionTracker.BeginScope();
            db.BulkUpdateAll(new BulkUpdateRequest
            {
                Entities = new[] { current },
                KeyPropertyNames = new[] { nameof(ConcurrencyItem.Id) },
                UpdatedPropertyNames = new[] { nameof(ConcurrencyItem.Name) },
            });

            Assert.IsTrue(scope.Created > 0, "Expected an owned concurrency transaction to be created.");
            Assert.AreEqual(
                scope.Created,
                scope.Disposed,
                $"Owned concurrency transaction must be disposed after Commit. Created={scope.Created}, Disposed={scope.Disposed}.");
        }

        [TestMethod]
        public void BulkUpdate_WithConcurrencyTokens_ShouldDisposeOwnedTransaction_OnConcurrencyFailure()
        {
            using var db1 = Factory.CreateDbContext();
            using var db2 = Factory.CreateDbContext();

            var item = new ConcurrencyItem { Name = "Original" };
            db1.ConcurrencyItems.Add(item);
            db1.SaveChanges();

            var stale = db1.ConcurrencyItems.Single(x => x.Id == item.Id);

            var other = db2.ConcurrencyItems.Single(x => x.Id == item.Id);
            other.Name = "Changed by other writer";
            db2.Entry(other).Property(x => x.Name).IsModified = true;
            db2.SaveChanges();

            stale.Name = "Bulk overwrite";

            using var scope = SqlTransactionTracker.BeginScope();
            Assert.ThrowsExactly<DbUpdateConcurrencyException>(() =>
                db1.BulkUpdateAll(new BulkUpdateRequest
                {
                    Entities = new[] { stale },
                    KeyPropertyNames = new[] { nameof(ConcurrencyItem.Id) },
                    UpdatedPropertyNames = new[] { nameof(ConcurrencyItem.Name) },
                }));

            Assert.IsTrue(scope.Created > 0, "Expected an owned concurrency transaction to be created.");
            Assert.AreEqual(
                scope.Created,
                scope.Disposed,
                $"Owned concurrency transaction must be disposed after Rollback. Created={scope.Created}, Disposed={scope.Disposed}.");
        }
    }
}
