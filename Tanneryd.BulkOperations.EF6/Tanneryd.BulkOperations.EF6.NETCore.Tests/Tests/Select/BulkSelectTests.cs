/*
* Copyright ©  2017-2019 Tånneryd IT AB
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
using System.Globalization;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.EF6.Model;
using Tanneryd.BulkOperations.EF6.NETCore.Tests.Models.DM.Numbers;
using Tanneryd.BulkOperations.EF6.NETCore.Tests.Models.EF;

namespace Tanneryd.BulkOperations.EF6.NETCore.Tests.Tests.Select
{
    [TestClass]
    public class BulkSelectTests : BulkOperationTestBase
    {
        [TestInitialize]
        public void Initialize()
        {
            InitializeUnitTestContext();
            CleanUp();
        }

        [TestCleanup]
        public void CleanUp()
        {
            CleanupUnitTestContext();
        }

        [TestMethod]
        public void ExistingEntitiesShouldBeSelectedOnSingleKeyUsingSimpleType()
        {
            using (var db = new UnitTestContext())
            {
                var now = DateTime.Now;

                // Save 200 numbers (1 to 200) to the database.
                var numbers = GenerateNumbers(1, 200, now).ToArray();
                db.BulkInsertAll(new BulkInsertRequest<Number>
                {
                    Entities = numbers,
                    EnableRecursiveInsert = EnableRecursiveInsert.Yes
                });

                // Create a list of 100 numbers with values 151 to 250
                var nums = GenerateNumbers(151, 100, now).ToList();

                // Numbers 151 to 200 from the database should be selected.
                var existingNumbers = db.BulkSelect<Number, Number>(new BulkSelectRequest<Number>
                {
                    Items = nums.ToArray(),
                    KeyPropertyMappings = new[]
                    {
                        new KeyPropertyMapping
                        {
                            ItemPropertyName = "Value",
                            EntityPropertyName = "Value"
                        },
                    }
                }).ToArray();

                Assert.AreEqual(existingNumbers.Length, 50);
                var expectedNumbers = numbers.Skip(150).Take(50).ToArray();

                for (int i = 0; i < 50; i++)
                {
                    Assert.AreEqual(expectedNumbers[i].Id, existingNumbers[i].Id);
                    Assert.AreEqual(expectedNumbers[i].ParityId, existingNumbers[i].ParityId);
                    Assert.AreEqual(expectedNumbers[i].UpdatedAt.ToString(CultureInfo.InvariantCulture), existingNumbers[i].UpdatedAt.ToString(CultureInfo.InvariantCulture));
                    Assert.AreEqual(expectedNumbers[i].UpdatedBy, existingNumbers[i].UpdatedBy);
                    Assert.AreEqual(expectedNumbers[i].Value, existingNumbers[i].Value);
                }
            }
        }

        [TestMethod]
        public void ExistingEntitiesShouldBeSelectedOnSingleKey()
        {
            using (var db = new UnitTestContext())
            {
                var now = DateTime.Now;

                // Save 200 numbers (1 to 200) to the database.
                var numbers = GenerateNumbers(1, 200, now).ToArray();
                db.BulkInsertAll(new BulkInsertRequest<Number>
                {
                    Entities = numbers,
                    EnableRecursiveInsert = EnableRecursiveInsert.Yes
                });

                // Create a list of 100 numbers with values 151 to 250
                var nums = GenerateNumbers(151, 100, now)
                    .Select(n=> new Num { Val = n.Value})
                    .ToList();

                // Numbers 151 to 200 from the database should be selected.
                var existingNumbers = db.BulkSelect<Num, Number>(new BulkSelectRequest<Num>
                {
                    Items = nums.ToArray(),
                    KeyPropertyMappings = new[]
                    {
                        new KeyPropertyMapping
                        {
                            ItemPropertyName = "Val",
                            EntityPropertyName = "Value"
                        },
                    }
                }).ToArray();

                Assert.AreEqual(existingNumbers.Length, 50);
                var expectedNumbers = numbers.Skip(150).Take(50).ToArray();

                for (int i = 0; i < 50; i++)
                {
                    Assert.AreEqual(expectedNumbers[i].Id, existingNumbers[i].Id);
                    Assert.AreEqual(expectedNumbers[i].ParityId, existingNumbers[i].ParityId);
                    Assert.AreEqual(expectedNumbers[i].UpdatedAt.ToString(CultureInfo.InvariantCulture), existingNumbers[i].UpdatedAt.ToString(CultureInfo.InvariantCulture));
                    Assert.AreEqual(expectedNumbers[i].UpdatedBy, existingNumbers[i].UpdatedBy);
                    Assert.AreEqual(expectedNumbers[i].Value, existingNumbers[i].Value);
                }
            }
        }

    }
}
