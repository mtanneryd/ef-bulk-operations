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
using Tanneryd.BulkOperations.EF6.NetStd;
using Tanneryd.BulkOperations.EF6.NetStd.Model;
using Tanneryd.BulkOperations.EF6.Tests.DM.Levels;
using Tanneryd.BulkOperations.EF6.Tests.DM.Miscellaneous;
using Tanneryd.BulkOperations.EF6.Tests.DM.Numbers;
using Tanneryd.BulkOperations.EF6.Tests.DM.People;
using Tanneryd.BulkOperations.EF6.Tests.EF;

namespace Tanneryd.BulkOperations.EF6.Tests.Tests.Insert
{
    [TestClass]
    public class BulkInsertMiscellaneousTests : BulkOperationTestBase
    {
        [TestInitialize]
        public void Initialize()
        {
            InitializeSchoolContext();
            InitializeNumberContext();
            InitializeLevelContext();
            InitializeBlogContext();
            InitializeMiscellaneousContext();
            InitializeTeamContext();
            InitializePeopleContext();
            CleanUp();
        }

        [TestCleanup]
        public void CleanUp()
        {
            CleanupSchoolContext();
            CleanupNumberContext();
            CleanupLevelContext();
            CleanupBlogContext();
            CleanupMiscellaneousContext();
            CleanupTeamContext();
            CleanupPeopleContext();
        }

        [TestMethod]
        public void FillingUpTableWithPrimaryKeyColumnOnlyShouldBePossible()
        {
            using (var db = new MiscellaneousContext())
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
                for (int i = 0; i < 1000; i++)
                {
                    Console.WriteLine(entities[i].Id);
                }
            }
        }

        [TestMethod]
        public void AlreadyExistingEntityWithIdentityKeyShouldNotBeInserted()
        {
            using (var db = new PeopleContext())
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
            using (var db = new NumberContext())
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
            using (var db = new NumberContext())
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
        public void ComplexTypesShouldBeInserted()
        {
            using (var db = new LevelContext())
            {
                // The level entities are used to test EF complex types.
                var expectedLevels = new[]
                {
                        new Level1
                        {
                            Level2 = new Level2
                            {
                                Level2Name = "L2",
                                Level3 = new Level3
                                {
                                    Level3Name = "L3",
                                    Updated = new DateTime(2018, 1, 1)
                                }
                            }
                        }
                    };
                db.BulkInsertAll(expectedLevels);

                var actualLevels = db.Levels.ToArray();
                Assert.AreEqual(expectedLevels.Length, actualLevels.Length);
                Assert.AreEqual(expectedLevels[0].Id, actualLevels[0].Id);
                Assert.AreEqual(expectedLevels[0].Level2.Level2Name, actualLevels[0].Level2.Level2Name);
                Assert.AreEqual(expectedLevels[0].Level2.Level3.Level3Name,
                    actualLevels[0].Level2.Level3.Level3Name);
                Assert.AreEqual(expectedLevels[0].Level2.Level3.Updated.Ticks,
                    actualLevels[0].Level2.Level3.Updated.Ticks);
            }
        }

        [TestMethod]
        public void RowWithReservedSqlKeywordAsColumnNameShouldBeInserted()
        {
            using (var db = new MiscellaneousContext())
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
            using (var db = new MiscellaneousContext())
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

