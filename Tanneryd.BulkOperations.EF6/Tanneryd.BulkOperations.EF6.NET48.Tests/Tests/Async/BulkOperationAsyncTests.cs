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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.EF6;
using Tanneryd.BulkOperations.EF6.Model;
using Tanneryd.BulkOperations.EF6.NET48.Tests.Models.DM.People;
using Tanneryd.BulkOperations.EF6.NET48.Tests.Models.DM.Prices;
using Tanneryd.BulkOperations.EF6.NET48.Tests.Models.EF;

namespace Tanneryd.BulkOperations.EF6.NET48.Tests.Tests.Async
{
    [TestClass]
    public class BulkOperationAsyncTests : BulkOperationTestBase
    {
        [TestInitialize]
        public void Initialize()
        {
            InitializeUnitTestContext();
        }

        [TestCleanup]
        public void CleanUp()
        {
            CleanupUnitTestContext();
        }

        [TestMethod]
        public async Task BulkInsertAllAsyncShouldInsertAndRetrieveGeneratedKeys()
        {
            using (var db = new UnitTestContext())
            {
                var people = Enumerable.Range(0, 10)
                    .Select(i => new Person
                    {
                        FirstName = $"Async{i:D2}",
                        LastName = "Insert",
                        BirthDate = new DateTime(1980, 1, 1)
                    })
                    .ToList();

                var response = await db.BulkInsertAllAsync(new BulkInsertRequest<Person>
                {
                    Entities = people,
                    EnableRecursiveInsert = EnableRecursiveInsert.NoButRetrieveGeneratedPrimaryKeys,
                    AllowNotNullSelfReferences = AllowNotNullSelfReferences.No
                });

                Assert.IsTrue(response.AffectedRows.Sum(r => r.Item2) >= people.Count);
                Assert.IsTrue(people.All(p => p.Id > 0));
                Assert.AreEqual(people.Count, db.People.Count());
            }
        }

        [TestMethod]
        public async Task BulkSelectExistingAsyncShouldReturnMatchingItems()
        {
            using (var db = new UnitTestContext())
            {
                var people = new List<Person>
                {
                    new Person { FirstName = "Select", LastName = "Async", BirthDate = new DateTime(1975, 5, 5) }
                };

                await db.BulkInsertAllAsync(new BulkInsertRequest<Person>
                {
                    Entities = people,
                    EnableRecursiveInsert = EnableRecursiveInsert.NoButRetrieveGeneratedPrimaryKeys
                });

                var probe = new Person { Id = people[0].Id };
                var existing = await db.BulkSelectExistingAsync<Person, Person>(new BulkSelectRequest<Person>
                {
                    Items = new[] { probe },
                    KeyPropertyMappings = new[]
                    {
                        new KeyPropertyMapping
                        {
                            ItemPropertyName = "Id",
                            EntityPropertyName = "Id"
                        }
                    }
                });

                Assert.AreEqual(1, existing.Count);
                Assert.AreEqual(people[0].Id, existing[0].Id);
            }
        }

        [TestMethod]
        public async Task BulkUpdateAllAsyncShouldUpdateMatchingRows()
        {
            var seedDate = new DateTime(2024, 1, 1);
            using (var db = new UnitTestContext())
            {
                db.Prices.Add(new Price { Date = seedDate, Name = "AsyncPrice", Value = 10m });
                db.SaveChanges();

                await db.BulkUpdateAllAsync(new BulkUpdateRequest
                {
                    Entities = new[]
                    {
                        new Price { Date = seedDate, Name = "AsyncPrice", Value = 42m }
                    },
                    KeyPropertyNames = new[] { "Date", "Name" },
                    UpdatedPropertyNames = new[] { "Value" }
                });
            }

            using (var db = new UnitTestContext())
            {
                Assert.AreEqual(42m, db.Prices.Single(p => p.Name == "AsyncPrice").Value);
            }
        }
    }
}
