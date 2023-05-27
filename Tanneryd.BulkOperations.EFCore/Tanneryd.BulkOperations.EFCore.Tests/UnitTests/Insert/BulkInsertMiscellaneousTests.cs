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
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.EFCore.Model;

namespace Tanneryd.BulkOperations.EFCore.Tests.UnitTests.Insert
{
    [TestClass]
    public class BulkInsertMiscellaneousTests : BulkOperationTestBase
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
        public void FillingUpTableWithPrimaryKeyColumnOnlyShouldBePossible()
        {
            using (var db = new UnitTestContext())
            {
                var entities = new List<EmptyTable>();
                for (int i = 0; i < 1000; i++)
                {
                    entities.Add(new EmptyTable());
                }

                var request = new BulkInsertRequest<EmptyTable>
                {
                    AllowNotNullSelfReferences = AllowNotNullSelfReferences.No,
                    EnableRecursiveInsert = EnableRecursiveInsert.NoButRetrieveGeneratedPrimaryKeys,
                    Entities = entities
                };

                db.BulkInsertAll(request);
                //for (int i = 0; i < 1000; i++)
                //{
                //    Console.WriteLine(entities[i].Id);
                //}
            }
        }

        [TestMethod]
        public void AlreadyExistingEntityWithIdentityKeyShouldNotBeInserted()
        {
            using (var db = new UnitTestContext())
            {
                var p = new Person
                {
                    FirstName = "Måns",
                    LastName = "Tånneryd",
                    BirthDate = new DateTime(1968,10,04)
                };

                db.BulkInsertAll(new BulkInsertRequest<Person>
                {
                    Entities = new[] {p}.ToList()
                });


                Assert.AreEqual(1, db.People.Count());

                db.BulkInsertAll(new BulkInsertRequest<Person>
                {
                    Entities = new[] {p}.ToList()
                });

                Assert.AreEqual(1, db.People.Count());

            }
        }

        [TestMethod]
        public void PrimaryKeyColumnMappedToPropertyWithDifferentNameShouldBeAllowed()
        {
            using (var db = new UnitTestContext())
            {
                var now = DateTime.Now;

                // The Parity table is defined with a pk column named Key but
                // it is mapped to the property Id. There was a user reporting that
                // this did not work properly so we want to test it.
                var parities = new[]
                {
                        new Parity {Name = "Even", UpdatedAt = now, UpdatedBy = "Måns"},
                        new Parity {Name = "Odd", UpdatedAt = now, UpdatedBy = "Måns"},
                    };
                db.BulkInsertAll(parities);

                Assert.IsTrue(parities[0].Id > 0);
                Assert.IsTrue(parities[1].Id > 0);
            }
        }

        [TestMethod]
        public void EntityHierarchyShouldBeInserted()
        {
            using (var db = new UnitTestContext())
            {
                var now = DateTime.Now;

                var numbers = GenerateNumbers(1, 10, now).ToArray(); // 1-10
                var primes = GeneratePrimeNumbers(10, numbers, now); // 1,2,3,5,7

                var request = new BulkInsertRequest<Prime>
                {
                    Entities = primes,
                    EnableRecursiveInsert = EnableRecursiveInsert.Yes,
                };
                db.BulkInsertAll(request);

                var actualNumbers = db.Numbers.ToArray();
                var actualPrimes = db.Primes.ToArray();

                Assert.AreEqual(5, actualNumbers.Length);
                Assert.AreEqual(5, actualPrimes.Length);
                Assert.AreEqual(1, actualPrimes[0].Number.Value);
                Assert.AreEqual(2, actualPrimes[1].Number.Value);
                Assert.AreEqual(3, actualPrimes[2].Number.Value);
                Assert.AreEqual(5, actualPrimes[3].Number.Value);
                Assert.AreEqual(7, actualPrimes[4].Number.Value);
            }
        }

        [TestMethod]
        public void RowWithReservedSqlKeywordAsColumnNameShouldBeInserted()
        {
            using (var db = new UnitTestContext())
            {
                var e = new ReservedSqlKeyword
                {
                    Identity = 10
                };
                db.BulkInsertAll(new[] { e });

                Assert.AreEqual(10, e.Identity);
            }
        }

        [TestMethod]
        public void RowWithCompositePrimaryKeyShouldBeInserted()
        {
            using (var db = new UnitTestContext())
            {
                var x = new Coordinate
                {
                    Value = 1
                };
                var y = new Coordinate
                {
                    Value = 2
                };
                var p = new Point
                {
                    XCoordinate = x,
                    YCoordinate = y,
                    Value = 100
                };
                db.BulkInsertAll(new[] { p }, null, true);

                Assert.AreEqual(100, p.Value);
            }
        }
    }
}

