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
using Microsoft.Data.SqlClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.Common.Sql;
using Tanneryd.BulkOperations.EFCore.Model;

namespace Tanneryd.BulkOperations.EFCore.Tests.UnitTests
{
    /// <summary>
    /// Regression: session temp tables must be dropped even when bulk ops fail
    /// (and on identity-insert paths that previously only dropped on some happy paths).
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

        [TestMethod]
        public void BulkInsert_ShouldDropTempTable_AfterIdentityKeyRetrieval()
        {
            using var scope = TempTableTracker.BeginScope();
            using var db = Factory.CreateDbContext();

            db.BulkInsertAll(new BulkInsertRequest<Price>
            {
                Entities =
                [
                    new Price { Date = new DateTime(2019, 1, 1), Name = "TempCleanup", Value = 1 },
                ],
                EnableRecursiveInsert = EnableRecursiveInsert.NoButRetrieveGeneratedPrimaryKeys,
            });

            Assert.IsTrue(scope.Created > 0, "Expected a temp table to be created for identity retrieval.");
            Assert.AreEqual(
                scope.Created,
                scope.Dropped,
                $"Expected every temp table to be dropped. Created={scope.Created}, Dropped={scope.Dropped}.");
        }

        [TestMethod]
        public void BulkUpdate_ShouldDropTempTable_WhenUpdateSqlFails()
        {
            using var db = Factory.CreateDbContext();
            var price = new Price
            {
                Date = new DateTime(2019, 1, 1),
                Name = "TempCleanupUpdate",
                Value = 10,
            };
            db.Prices.Add(price);
            db.SaveChanges();

            using var scope = TempTableTracker.BeginScope();

            // UpdatedPropertyNames is only the key → zero SET columns → invalid UPDATE SQL
            // after the temp table has already been created/filled.
            try
            {
                db.BulkUpdateAll(new BulkUpdateRequest
                {
                    Entities = new[] { price },
                    KeyPropertyNames = new[] { nameof(Price.Id) },
                    UpdatedPropertyNames = new[] { nameof(Price.Id) },
                });
                Assert.Fail("Expected BulkUpdateAll to throw for an empty SET list.");
            }
            catch (SqlException)
            {
                // Expected: UPDATE … SET  FROM … is invalid.
            }
            catch (ArgumentException)
            {
                // Acceptable if validation is added later; temp must still be balanced.
            }

            Assert.IsTrue(scope.Created > 0, "Expected a temp table to be created before the UPDATE failed.");
            Assert.AreEqual(
                scope.Created,
                scope.Dropped,
                $"Expected temp table drop in finally after failure. Created={scope.Created}, Dropped={scope.Dropped}.");
        }
    }
}
