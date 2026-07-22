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
using Tanneryd.BulkOperations.EF6.Model;
using Tanneryd.BulkOperations.EF6.NET48.Tests.Models.DM.Levels;
using Tanneryd.BulkOperations.EF6.NET48.Tests.Models.EF;

namespace Tanneryd.BulkOperations.EF6.NET48.Tests.Tests.Insert
{
    /// <summary>
    /// Flatten must not NRE when a mapped complex property is null; callers need
    /// a clear ArgumentException that names the property.
    /// </summary>
    [TestClass]
    public class BulkInsertNullComplexPropertyTests : BulkOperationTestBase
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
        public void BulkInsert_ShouldThrowArgumentException_WhenComplexPropertyIsNull()
        {
            using (var db = new UnitTestContext())
            {
                var levels = new[]
                {
                    new Level1
                    {
                        Level1Name = "orphan",
                        Level2 = null,
                    },
                };

                var ex = Assert.ThrowsExactly<ArgumentException>(() => db.BulkInsertAll(levels));
                StringAssert.Contains(
                    ex.Message,
                    nameof(Level1.Level2),
                    "Expected a clear error naming the null complex property, not a NullReferenceException from Flatten.");
            }
        }
    }
}
