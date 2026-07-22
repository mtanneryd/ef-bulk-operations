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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.EF6.Model;
using Tanneryd.BulkOperations.EF6.NET48.Tests.Models.DM.Levels;
using Tanneryd.BulkOperations.EF6.NET48.Tests.Models.EF;

namespace Tanneryd.BulkOperations.EF6.NET48.Tests.Tests.Insert
{
    /// <summary>
    /// Complex types are flattened to ExpandoObject; GetProperties(entities[0])
    /// currently skips null keys on the first row, so later rows lose those columns.
    /// </summary>
    [TestClass]
    public class BulkInsertExpandoNullPropertyTests : BulkOperationTestBase
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
        public void BulkInsert_ComplexType_ShouldPersistNonNullProperty_WhenFirstRowHadNull()
        {
            using (var db = new UnitTestContext())
            {
                var levels = new[]
                {
                    new Level1
                    {
                        Level1Name = null,
                        Level2 = new Level2
                        {
                            Level2Name = "L2-first",
                            Level3 = new Level3
                            {
                                Level3Name = "L3-first",
                                Updated = new DateTime(2018, 1, 1),
                            },
                        },
                    },
                    new Level1
                    {
                        Level1Name = "must-not-be-lost",
                        Level2 = new Level2
                        {
                            Level2Name = "L2-second",
                            Level3 = new Level3
                            {
                                Level3Name = "L3-second",
                                Updated = new DateTime(2018, 2, 2),
                            },
                        },
                    },
                };

                db.BulkInsertAll(levels);

                var fromDb = db.Levels.OrderBy(l => l.Id).ToArray();
                Assert.AreEqual(2, fromDb.Length);
                Assert.IsNull(fromDb[0].Level1Name);
                Assert.AreEqual(
                    "must-not-be-lost",
                    fromDb[1].Level1Name,
                    "Skipping null Expando keys on entities[0] drops that column for later rows.");
                Assert.AreEqual("L2-second", fromDb[1].Level2.Level2Name);
                Assert.AreEqual("L3-second", fromDb[1].Level2.Level3.Level3Name);
            }
        }
    }
}
