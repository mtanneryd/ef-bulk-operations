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
    }
}
