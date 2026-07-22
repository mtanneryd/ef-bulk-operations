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
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.Common.Sql;
using Tanneryd.BulkOperations.EFCore.Model;
using Tanneryd.BulkOperations.TestModels;

namespace Tanneryd.BulkOperations.EFCore.Tests.UnitTests
{
    /// <summary>
    /// Ensures SET IDENTITY_INSERT is always balanced (OFF after every ON).
    /// BulkUpdate stages rows in a temp table that inherits identity metadata from
    /// the target; FillTempTable turns IDENTITY_INSERT ON to KeepIdentity-copy the
    /// key values, and must turn it OFF again on both success and failure paths.
    /// SQL Server allows IDENTITY_INSERT ON for only one table per session; leaving
    /// it on for the staging #temp blocks any later SET IDENTITY_INSERT ON in that
    /// session. With connection pooling, that session state survives when the
    /// connection returns to the pool, so a later unrelated caller can fail on a
    /// seemingly clean connection.
    /// </summary>
    [TestClass]
    public class IdentityInsertCleanupTests : BulkOperationTestBase
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
        /// Happy path: BulkUpdate of an identity-keyed entity enables IDENTITY_INSERT
        /// while filling the staging temp table, then disables it before returning.
        /// </summary>
        [TestMethod]
        public void BulkUpdate_ShouldDisableIdentityInsert_AfterFillTempTable()
        {
            using var db = Factory.CreateDbContext();
            var price = new Price
            {
                Date = new DateTime(2019, 1, 1),
                Name = "IdentityInsertCleanup",
                Value = 10,
            };
            db.Prices.Add(price);
            db.SaveChanges();

            price.Value = 20;

            using var scope = IdentityInsertTracker.BeginScope();
            db.BulkUpdateAll(new BulkUpdateRequest
            {
                Entities = new[] { price },
                KeyPropertyNames = new[] { nameof(Price.Id) },
            });

            Assert.IsTrue(scope.Enabled > 0, "Expected IDENTITY_INSERT ON for identity-key temp fill.");
            Assert.AreEqual(
                scope.Enabled,
                scope.Disabled,
                $"Expected IDENTITY_INSERT OFF after enable. Enabled={scope.Enabled}, Disabled={scope.Disabled}.");
        }

        /// <summary>
        /// Failure path: IDENTITY_INSERT must still be turned OFF when BulkUpdate
        /// fails after FillTempTable. A stale rowversion forces a concurrency
        /// exception after the identity-key temp fill (empty UpdatedPropertyNames
        /// is rejected before FillTempTable, so it cannot exercise this path).
        /// </summary>
        [TestMethod]
        public void BulkUpdate_ShouldDisableIdentityInsert_WhenUpdateFails()
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

            using var scope = IdentityInsertTracker.BeginScope();

            Assert.ThrowsExactly<DbUpdateConcurrencyException>(() =>
                db1.BulkUpdateAll(new BulkUpdateRequest
                {
                    Entities = new[] { stale },
                    KeyPropertyNames = new[] { nameof(ConcurrencyItem.Id) },
                    UpdatedPropertyNames = new[] { nameof(ConcurrencyItem.Name) },
                }));

            Assert.IsTrue(scope.Enabled > 0, "Expected IDENTITY_INSERT ON before the UPDATE failed.");
            Assert.AreEqual(
                scope.Enabled,
                scope.Disabled,
                $"Expected IDENTITY_INSERT OFF after failure. Enabled={scope.Enabled}, Disabled={scope.Disabled}.");
        }
    }
}
