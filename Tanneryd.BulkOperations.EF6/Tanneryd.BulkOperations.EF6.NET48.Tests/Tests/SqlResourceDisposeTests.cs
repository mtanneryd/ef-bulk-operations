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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.Common.Sql;
using Tanneryd.BulkOperations.EF6.Model;
using Tanneryd.BulkOperations.EF6.NET48.Tests.Models.DM.Prices;
using Tanneryd.BulkOperations.EF6.NET48.Tests.Models.EF;

namespace Tanneryd.BulkOperations.EF6.NET48.Tests.Tests
{
    /// <summary>
    /// Regression: SqlCommand / SqlBulkCopy wrappers created during bulk ops must
    /// be disposed. Uses <see cref="SqlResourceTracker"/> so the assertion is
    /// deterministic (LocalDB alone cannot catch this).
    /// </summary>
    [TestClass]
    public class SqlResourceDisposeTests : BulkOperationTestBase
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
        public void BulkInsert_ShouldDisposeSqlCommandsAndBulkCopies()
        {
            using (var scope = SqlResourceTracker.BeginScope())
            using (var db = new UnitTestContext())
            {
                db.BulkInsertAll(new BulkInsertRequest<Price>
                {
                    Entities = new[]
                    {
                        new Price { Date = new DateTime(2019, 1, 1), Name = "DisposeCheck", Value = 1 },
                    },
                    EnableRecursiveInsert = EnableRecursiveInsert.NoButRetrieveGeneratedPrimaryKeys,
                });

                Assert.IsTrue(scope.Created > 0, "Expected at least one tracked SQL resource to be created.");
                Assert.AreEqual(
                    scope.Created,
                    scope.Disposed,
                    $"Expected every tracked SqlCommand/SqlBulkCopy to be disposed. Created={scope.Created}, Disposed={scope.Disposed}.");
            }
        }
    }
}
