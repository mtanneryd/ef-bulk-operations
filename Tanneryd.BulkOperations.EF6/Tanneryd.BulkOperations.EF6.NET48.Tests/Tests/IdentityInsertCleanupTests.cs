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
using Tanneryd.BulkOperations.EF6.Model;
using Tanneryd.BulkOperations.EF6.NET48.Tests.Models.DM.Prices;
using Tanneryd.BulkOperations.EF6.NET48.Tests.Models.EF;

namespace Tanneryd.BulkOperations.EF6.NET48.Tests.Tests
{
    /// <summary>
    /// Regression: SET IDENTITY_INSERT must be turned OFF after every ON
    /// (FillTempTable enables it for identity keys but historically never disabled).
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

        [TestMethod]
        public void BulkUpdate_ShouldDisableIdentityInsert_AfterFillTempTable()
        {
            using (var db = new UnitTestContext())
            {
                var price = new Price
                {
                    Date = new DateTime(2019, 1, 1),
                    Name = "IdentityInsertCleanup",
                    Value = 10,
                };
                db.Prices.Add(price);
                db.SaveChanges();

                price.Value = 20;

                using (var scope = IdentityInsertTracker.BeginScope())
                {
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
            }
        }

        [TestMethod]
        public void BulkUpdate_ShouldDisableIdentityInsert_WhenUpdateSqlFails()
        {
            using (var db = new UnitTestContext())
            {
                var price = new Price
                {
                    Date = new DateTime(2019, 1, 1),
                    Name = "IdentityInsertFailCleanup",
                    Value = 10,
                };
                db.Prices.Add(price);
                db.SaveChanges();

                using (var scope = IdentityInsertTracker.BeginScope())
                {
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
                        // Acceptable if validation is added later; IDENTITY_INSERT must still be balanced.
                    }

                    Assert.IsTrue(scope.Enabled > 0, "Expected IDENTITY_INSERT ON before the UPDATE failed.");
                    Assert.AreEqual(
                        scope.Enabled,
                        scope.Disabled,
                        $"Expected IDENTITY_INSERT OFF in finally after failure. Enabled={scope.Enabled}, Disabled={scope.Disabled}.");
                }
            }
        }
    }
}
