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

using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tanneryd.BulkOperations.EFCore.SQLite.Tests.UnitTests.Select
{
    [TestClass]
    public class BulkSelectExistingTests : BulkOperationTestBase
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
        public void ZeroShouldNotMatchNullWhenSelectExisting()
        {
            using (var db = Factory.CreateDbContext())
            {
                db.Prices.Add(new Price() { Date = new DateTime(2019, 1, 1), Name = "ERICB", Value = 80 });
                db.Prices.Add(new Price() { Date = new DateTime(2019, 1, 2), Name = "ERICB", Value = 81 });
                db.Prices.Add(new Price() { Date = new DateTime(2019, 1, 3), Name = "ERICB", Value = 82 });
                db.Prices.Add(new Price() { Date = new DateTime(2019, 1, 4), Name = "ERICB", Value = 0 });
                db.Prices.Add(new Price() { Date = new DateTime(2019, 1, 5), Name = "ERICB", Value = 86 });
                db.SaveChanges();

                var prices = new[]
                {
                    new Price() {Date = new DateTime(2019, 1, 1), Name = "ERICB", Value = 80},
                    new Price() {Date = new DateTime(2019, 1, 2), Name = "ERICB", Value = 81},
                    new Price() {Date = new DateTime(2019, 1, 3), Name = "ERICB", Value = 82},
                    new Price() {Date = new DateTime(2019, 1, 4), Name = "ERICB", Value = null},
                    new Price() {Date = new DateTime(2019, 1, 5), Name = "ERICB", Value = 86}
                };
                
                var existing = db.BulkSelectExisting<Price, Price>(
                    new BulkSelectRequest<Price>(new[] { "Date", "Name", "Value" }, prices));
                Assert.AreEqual(4, existing.Count);
                Assert.AreSame(prices[0], existing[0]);
                Assert.AreSame(prices[1], existing[1]);
                Assert.AreSame(prices[2], existing[2]);
                Assert.AreSame(prices[4], existing[3]);
            }
        }

        [TestMethod]
        public void ZeroShouldNotMatchNullWhenSelectNotExisting()
        {
            using (var db = Factory.CreateDbContext())
            {
                db.Prices.Add(new Price() { Date = new DateTime(2019, 1, 1), Name = "ERICB", Value = 80 });
                db.Prices.Add(new Price() { Date = new DateTime(2019, 1, 2), Name = "ERICB", Value = 81 });
                db.Prices.Add(new Price() { Date = new DateTime(2019, 1, 3), Name = "ERICB", Value = 82 });
                db.Prices.Add(new Price() { Date = new DateTime(2019, 1, 4), Name = "ERICB", Value = 0 });
                db.Prices.Add(new Price() { Date = new DateTime(2019, 1, 5), Name = "ERICB", Value = 86 });
                db.SaveChanges();

                var prices = new[]
                {
                    new Price() {Date = new DateTime(2019, 1, 1), Name = "ERICB", Value = 80},
                    new Price() {Date = new DateTime(2019, 1, 2), Name = "ERICB", Value = 81},
                    new Price() {Date = new DateTime(2019, 1, 3), Name = "ERICB", Value = 82},
                    new Price() {Date = new DateTime(2019, 1, 4), Name = "ERICB", Value = null},
                    new Price() {Date = new DateTime(2019, 1, 5), Name = "ERICB", Value = 86}
                };
                var existing = db.BulkSelectNotExisting<Price, Price>(
                    new BulkSelectRequest<Price>(new[] { "Date", "Name", "Value" }, prices));
                Assert.AreEqual(1, existing.Count);
                Assert.AreSame(prices[3], existing[0]);
            }
        }

        [TestMethod]
        public void SelectExistingFromTableWithUserGeneratedGuidAsPrimaryKey()
        {
            using (var db = Factory.CreateDbContext())
            {
                var teams = new List<TeamWithUserGeneratedGuid>();

                // Add ten teams to the database (Team 0 - Team 9)
                for (int i = 0; i < 10; i++)
                {
                    teams.Add(new TeamWithUserGeneratedGuid() { Id = Guid.NewGuid(), Name = $"Team #{i}" });
                }

                // Save the ten first teams to the database.
                db.BulkInsertAll(new BulkInsertRequest<TeamWithUserGeneratedGuid>
                {
                    Entities = teams,
                });

                // Add another ten teams (Team 10 - Team 19) to
                // the list but not to the database.
                for (int i = 10; i < 20; i++)
                {
                    teams.Add(new TeamWithUserGeneratedGuid() { Id = Guid.NewGuid(), Name = $"Team #{i}" });
                }

                // The only teams we should get back out of the 20 teams (Team 0 - Team 19)
                // are the first ten that we saved to the database.
                var existingTeams = db.BulkSelectExisting<TeamWithUserGeneratedGuid, TeamWithUserGeneratedGuid>(new BulkSelectRequest<TeamWithUserGeneratedGuid>(new[] { "Id" }, teams));
                existingTeams = existingTeams.OrderBy(t => t.Name).ToList();
                Assert.AreEqual(10, existingTeams.Count);
                for (int i = 0; i < 10; i++)
                {
                    Assert.AreEqual(teams[i].Id, existingTeams[i].Id);
                    Assert.AreEqual(teams[i].Name, existingTeams[i].Name);
                }
            }
        }

        [TestMethod]
        public void SelectNotExistingFromTableWithUserGeneratedGuidAsPrimaryKeyShouldWork()
        {
            using (var db = Factory.CreateDbContext())
            {
                var teams = new List<TeamWithUserGeneratedGuid>();

                // Add ten teams to the database (Team 0 - Team 9)
                for (int i = 0; i < 10; i++)
                {
                    teams.Add(new TeamWithUserGeneratedGuid() { Id = Guid.NewGuid(), Name = $"Team #{i}" });
                }

                // Save the ten first teams to the database.
                db.BulkInsertAll(new BulkInsertRequest<TeamWithUserGeneratedGuid>
                {
                    Entities = teams
                });

                // Add another ten teams (Team 10 - Team 19) to
                // the list but not to the database.
                for (int i = 10; i < 20; i++)
                {
                    teams.Add(new TeamWithUserGeneratedGuid() { Id = Guid.NewGuid(), Name = $"Team #{i}" });
                }

                // The only teams we should get back out of the 20 teams (Team 0 - Team 19)
                // are the last ten that we did not save to the database.
                var existingTeams = db.BulkSelectNotExisting<TeamWithUserGeneratedGuid, TeamWithUserGeneratedGuid>(new BulkSelectRequest<TeamWithUserGeneratedGuid>(new[] { "Id" }, teams));
                Assert.AreEqual(10, existingTeams.Count);
                for (int i = 0; i < 10; i++)
                {
                    Assert.AreEqual(teams[i + 10].Id, existingTeams[i].Id);
                    Assert.AreEqual(teams[i + 10].Name, existingTeams[i].Name);
                }
            }
        }

        [TestMethod]
        public void SelectExistingFromTableWithDbGeneratedGuidAsPrimaryKeyShouldWork()
        {
            using (var db = Factory.CreateDbContext())
            {
                var teams = new List<TeamWithDbGeneratedGuid>();

                // Add ten teams to the database (Team 0 - Team 9)
                for (int i = 0; i < 10; i++)
                {
                    teams.Add(new TeamWithDbGeneratedGuid() { Name = $"Team #{i}" });
                }

                // Save the ten first teams to the database.
                db.BulkInsertAll(new BulkInsertRequest<TeamWithDbGeneratedGuid>
                {
                    Entities = teams
                });

                // Add another ten teams (Team 10 - Team 19) to
                // the list but not to the database.
                for (int i = 10; i < 20; i++)
                {
                    teams.Add(new TeamWithDbGeneratedGuid { Name = $"Team #{i}" });
                }

                // The only teams we should get back out of the 20 teams (Team 0 - Team 19)
                // are the first ten that we saved to the database.
                var existingTeams = db.BulkSelectExisting<TeamWithDbGeneratedGuid, TeamWithDbGeneratedGuid>(new BulkSelectRequest<TeamWithDbGeneratedGuid>(new[] { "Id" }, teams));
                Assert.AreEqual(10, existingTeams.Count);
                for (int i = 0; i < 10; i++)
                {
                    Assert.AreEqual(teams[i].Id, existingTeams[i].Id);
                    Assert.AreEqual(teams[i].Name, existingTeams[i].Name);
                }
            }
        }

        [TestMethod]
        public void SelectNotExistingFromTableWithDbGeneratedGuidAsPrimaryKeyShouldWork()
        {
            using (var db = Factory.CreateDbContext())
            {
                var teams = new List<TeamWithDbGeneratedGuid>();

                // Add ten teams to the database (Team 0 - Team 9)
                for (int i = 0; i < 10; i++)
                {
                    teams.Add(new TeamWithDbGeneratedGuid { Name = $"Team #{i}" });
                }

                // Save the ten first teams to the database.
                db.BulkInsertAll(new BulkInsertRequest<TeamWithDbGeneratedGuid>
                {
                    Entities = teams
                });

                // Add another ten teams (Team 10 - Team 19) to
                // the list but not to the database.
                for (int i = 10; i < 20; i++)
                {
                    teams.Add(new TeamWithDbGeneratedGuid { Name = $"Team #{i}" });
                }

                // The only teams we should get back out of the 20 teams (Team 0 - Team 19)
                // are the last ten that we did not save to the database.
                var existingTeams = db.BulkSelectNotExisting<TeamWithDbGeneratedGuid, TeamWithDbGeneratedGuid>(new BulkSelectRequest<TeamWithDbGeneratedGuid>(new[] { "Id" }, teams));
                Assert.AreEqual(10, existingTeams.Count);
                for (int i = 0; i < 10; i++)
                {
                    Assert.AreEqual(teams[i + 10].Id, existingTeams[i].Id);
                    Assert.AreEqual(teams[i + 10].Name, existingTeams[i].Name);
                }
            }
        }

        [TestMethod]
        public void PrimitiveTypeValuesMatchingExistingEntitiesShouldBeSelected()
        {
            using (var db = Factory.CreateDbContext())
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
                    .Select(n => n.Value)
                    .ToList();

                // Numbers 151 to 200 out of 151 to 250 should be selected.
                var existingNumbers = db.BulkSelectExisting<long, Number>(new BulkSelectRequest<long>
                {
                    Items = nums,
                    KeyPropertyMappings = new[]
                    {
                        new KeyPropertyMapping
                        {
                            ItemPropertyName = null,
                            EntityPropertyName = "Value"
                        },
                    }
                }).ToArray();

                Assert.AreEqual(50, existingNumbers.Length);
                for (int i = 0; i < 50; i++)
                {
                    Assert.AreEqual((object) nums[i], existingNumbers[i]);
                }
            }
        }

        [TestMethod]
        public void EntitiesOfDifferentTypeMatchingExistingEntitiesShouldBeSelected()
        {
            using (var db = Factory.CreateDbContext())
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
                    .Select(n => new Num { Val = n.Value })
                    .ToList();

                // Numbers 151 to 200 out of 151 to 250 should be selected.
                var existingNumbers = db.BulkSelectExisting<Num, Number>(new BulkSelectRequest<Num>
                {
                    Items = nums,
                    KeyPropertyMappings = new[]
                    {
                        new KeyPropertyMapping
                        {
                            ItemPropertyName = "Val",
                            EntityPropertyName = "Value"
                        },
                    }
                }).ToArray();

                Assert.AreEqual(50, existingNumbers.Length);
                for (int i = 0; i < 50; i++)
                {
                    Assert.AreEqual(nums[i], existingNumbers[i]);
                }
            }
        }

        [TestMethod]
        public void EntitiesOfSameTypeMatchingExistingEntitiesShouldBeSelected()
        {
            using (var db = Factory.CreateDbContext())
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
                numbers = GenerateNumbers(151, 100, now).ToArray();

                // Numbers 151 to 200 out of 151 to 250 should be selected.
                var existingNumbers = db.BulkSelectExisting<Number, Number>(new BulkSelectRequest<Number>
                {
                    Items = numbers,
                    KeyPropertyMappings = new[]
                    {
                        new KeyPropertyMapping
                        {
                            ItemPropertyName = "Value",
                            EntityPropertyName = "Value"
                        },
                    }
                }).ToArray();

                Assert.AreEqual(50, existingNumbers.Length);
                for (int i = 0; i < 50; i++)
                {
                    Assert.AreSame(numbers[i], existingNumbers[i]);
                }
            }
        }

        [TestMethod]
        public void ExistingEntitiesShouldBeSelectedUsingRuntimeTypes()
        {
            using (var db = Factory.CreateDbContext())
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
                numbers = GenerateNumbers(151, 100, now).ToArray();

                // Numbers 151 to 200 out of 151 to 250 should be selected.
                var request = typeof(BulkSelectRequest<>).MakeGenericType(typeof(Number));
                var r = Activator.CreateInstance(request, new[] { "Value" }, numbers.ToList(), null);

                Type ex = typeof(DbContextExtensions);
                MethodInfo mi = ex.GetMethod("BulkSelectExisting");
                MethodInfo miGeneric = mi.MakeGenericMethod(new[] { typeof(Number), typeof(Number) });
                object[] args = { db, r };
                var existingNumbers = (List<Number>)miGeneric.Invoke(null, args);

                Assert.AreEqual(50, existingNumbers.Count);
                for (int i = 0; i < 50; i++)
                {
                    Assert.AreSame(numbers[i], existingNumbers[i]);
                }
            }
        }
    }
}