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
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.EFCore.Model;

using Tanneryd.BulkOperations.EFCore.Tests.Models.DM.Teams.UsingDbGeneratedGuidKeys;
using Tanneryd.BulkOperations.EFCore.Tests.Models.DM.Teams.UsingUserGeneratedGuidKeys;

namespace Tanneryd.BulkOperations.EFCore.Tests.UnitTests.Select
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
                var teams = new List<TeamWithUserGeneratedGuidKey>();

                // Add ten teams to the database (Team 0 - Team 9)
                for (int i = 0; i < 10; i++)
                {
                    teams.Add(new TeamWithUserGeneratedGuidKey() { Id = Guid.NewGuid(), Name = $"Team #{i}" });
                }

                // Save the ten first teams to the database.
                db.BulkInsertAll(new BulkInsertRequest<TeamWithUserGeneratedGuidKey>
                {
                    Entities = teams,
                });

                // Add another ten teams (Team 10 - Team 19) to
                // the list but not to the database.
                for (int i = 10; i < 20; i++)
                {
                    teams.Add(new TeamWithUserGeneratedGuidKey() { Id = Guid.NewGuid(), Name = $"Team #{i}" });
                }

                // The only teams we should get back out of the 20 teams (Team 0 - Team 19)
                // are the first ten that we saved to the database.
                var existingTeams = db.BulkSelectExisting<TeamWithUserGeneratedGuidKey, TeamWithUserGeneratedGuidKey>(new BulkSelectRequest<TeamWithUserGeneratedGuidKey>(new[] { "Id" }, teams));
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
                var teams = new List<TeamWithUserGeneratedGuidKey>();

                // Add ten teams to the database (Team 0 - Team 9)
                for (int i = 0; i < 10; i++)
                {
                    teams.Add(new TeamWithUserGeneratedGuidKey() { Id = Guid.NewGuid(), Name = $"Team #{i}" });
                }

                // Save the ten first teams to the database.
                db.BulkInsertAll(new BulkInsertRequest<TeamWithUserGeneratedGuidKey>
                {
                    Entities = teams
                });

                // Add another ten teams (Team 10 - Team 19) to
                // the list but not to the database.
                for (int i = 10; i < 20; i++)
                {
                    teams.Add(new TeamWithUserGeneratedGuidKey() { Id = Guid.NewGuid(), Name = $"Team #{i}" });
                }

                // The only teams we should get back out of the 20 teams (Team 0 - Team 19)
                // are the last ten that we did not save to the database.
                var existingTeams = db.BulkSelectNotExisting<TeamWithUserGeneratedGuidKey, TeamWithUserGeneratedGuidKey>(new BulkSelectRequest<TeamWithUserGeneratedGuidKey>(new[] { "Id" }, teams));
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
                var teams = new List<TeamWithDbGeneratedGuidKey>();

                // Add ten teams to the database (Team 0 - Team 9)
                for (int i = 0; i < 10; i++)
                {
                    teams.Add(new TeamWithDbGeneratedGuidKey() { Name = $"Team #{i}" });
                }

                // Save the ten first teams to the database.
                db.BulkInsertAll(new BulkInsertRequest<TeamWithDbGeneratedGuidKey>
                {
                    Entities = teams
                });

                // Add another ten teams (Team 10 - Team 19) to
                // the list but not to the database.
                for (int i = 10; i < 20; i++)
                {
                    teams.Add(new TeamWithDbGeneratedGuidKey { Name = $"Team #{i}" });
                }

                // The only teams we should get back out of the 20 teams (Team 0 - Team 19)
                // are the first ten that we saved to the database.
                var existingTeams = db.BulkSelectExisting<TeamWithDbGeneratedGuidKey, TeamWithDbGeneratedGuidKey>(new BulkSelectRequest<TeamWithDbGeneratedGuidKey>(new[] { "Id" }, teams));
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
                var teams = new List<TeamWithDbGeneratedGuidKey>();

                // Add ten teams to the database (Team 0 - Team 9)
                for (int i = 0; i < 10; i++)
                {
                    teams.Add(new TeamWithDbGeneratedGuidKey { Name = $"Team #{i}" });
                }

                // Save the ten first teams to the database.
                db.BulkInsertAll(new BulkInsertRequest<TeamWithDbGeneratedGuidKey>
                {
                    Entities = teams
                });

                // Add another ten teams (Team 10 - Team 19) to
                // the list but not to the database.
                for (int i = 10; i < 20; i++)
                {
                    teams.Add(new TeamWithDbGeneratedGuidKey { Name = $"Team #{i}" });
                }

                // The only teams we should get back out of the 20 teams (Team 0 - Team 19)
                // are the last ten that we did not save to the database.
                var existingTeams = db.BulkSelectNotExisting<TeamWithDbGeneratedGuidKey, TeamWithDbGeneratedGuidKey>(new BulkSelectRequest<TeamWithDbGeneratedGuidKey>(new[] { "Id" }, teams));
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

        /// <summary>
        /// ColumnPropertyMappings must read SELECT [t1].* values by store column
        /// name. Invoice.Id maps to column PrimaryKey; indexing the reader by the
        /// CLR property name fails.
        /// </summary>
        [TestMethod]
        public void BulkSelectExisting_ShouldHydrateColumnPropertyMappings_WhenPropertyNameDiffersFromColumnName()
        {
            using (var db = Factory.CreateDbContext())
            {
                var id = Guid.NewGuid();
                db.Invoices.Add(new Invoice
                {
                    Id = id,
                    Net = 100m,
                    Gross = 125m,
                });
                db.SaveChanges();

                var probe = new Invoice
                {
                    Id = Guid.Empty,
                    Net = 100m,
                    Gross = 125m,
                };

                var existing = db.BulkSelectExisting<Invoice, Invoice>(new BulkSelectRequest<Invoice>
                {
                    Items = new[] { probe },
                    KeyPropertyMappings = KeyPropertyMapping.IdentityMappings(new[]
                    {
                        nameof(Invoice.Net),
                        nameof(Invoice.Gross),
                    }),
                    ColumnPropertyMappings = new[]
                    {
                        new KeyPropertyMapping
                        {
                            EntityPropertyName = nameof(Invoice.Id),
                            ItemPropertyName = nameof(Invoice.Id),
                        },
                    },
                });

                Assert.AreEqual(1, existing.Count);
                Assert.AreSame(probe, existing[0]);
                Assert.AreEqual(id, probe.Id);
            }
        }

        /// <summary>
        /// Nav-dot keys (e.g. Parity.Id) join the related table using FK/select
        /// names taken from CLR properties. Parity.Id stores as column Key, so
        /// the generated ON/WHERE must use the store column name.
        /// </summary>
        [TestMethod]
        public void BulkSelectExisting_ShouldMatchViaNavDotKey_WhenPropertyNameDiffersFromColumnName()
        {
            using (var db = Factory.CreateDbContext())
            {
                var now = DateTime.Now;
                var numbers = GenerateNumbers(1, 5, now).ToArray();
                db.BulkInsertAll(new BulkInsertRequest<Number>
                {
                    Entities = numbers,
                    EnableRecursiveInsert = EnableRecursiveInsert.Yes,
                });

                var seeded = numbers[0];
                var probe = new Number
                {
                    Value = seeded.Value,
                    ParityId = seeded.ParityId,
                    UpdatedAt = now,
                    UpdatedBy = "probe",
                };

                var existing = db.BulkSelectExisting<Number, Number>(new BulkSelectRequest<Number>
                {
                    Items = new[] { probe },
                    KeyPropertyMappings = new[]
                    {
                        new KeyPropertyMapping
                        {
                            EntityPropertyName = nameof(Number.Value),
                            ItemPropertyName = nameof(Number.Value),
                        },
                        new KeyPropertyMapping
                        {
                            EntityPropertyName = "Parity.Id",
                            ItemPropertyName = nameof(Number.ParityId),
                        },
                    },
                });

                Assert.AreEqual(1, existing.Count);
                Assert.AreSame(probe, existing[0]);
            }
        }

        /// <summary>
        /// A nav-dot key alone must still produce valid SQL (no empty key-column
        /// list / ON clause) and resolve store column names for the related-table
        /// join. Parity.Id maps to column Key.
        /// </summary>
        [TestMethod]
        public void BulkSelectExisting_ShouldMatchWhenOnlyNavDotKeyIsProvided()
        {
            using (var db = Factory.CreateDbContext())
            {
                var now = DateTime.Now;
                var numbers = GenerateNumbers(1, 1, now).ToArray();
                db.BulkInsertAll(new BulkInsertRequest<Number>
                {
                    Entities = numbers,
                    EnableRecursiveInsert = EnableRecursiveInsert.Yes,
                });

                var seeded = numbers[0];
                var probe = new Number
                {
                    Value = -1,
                    ParityId = seeded.ParityId,
                    UpdatedAt = now,
                    UpdatedBy = "probe",
                };

                var existing = db.BulkSelectExisting<Number, Number>(new BulkSelectRequest<Number>
                {
                    Items = new[] { probe },
                    KeyPropertyMappings = new[]
                    {
                        new KeyPropertyMapping
                        {
                            EntityPropertyName = "Parity.Id",
                            ItemPropertyName = nameof(Number.ParityId),
                        },
                    },
                });

                Assert.AreEqual(1, existing.Count);
                Assert.AreSame(probe, existing[0]);
            }
        }

        /// <summary>
        /// Nav-dot-only SelectExisting stages the related key as an extra temp
        /// column. Guid/uniqueidentifier extras must not use CAST(0 AS …), which
        /// is invalid SQL and fails temp-table creation.
        /// </summary>
        [TestMethod]
        public void BulkSelectExisting_ShouldMatchWhenOnlyGuidNavDotKeyIsProvided()
        {
            using (var db = Factory.CreateDbContext())
            {
                var teamId = Guid.NewGuid();
                var team = new TeamWithUserGeneratedGuidKey
                {
                    Id = teamId,
                    Name = "Guid-nav team",
                };
                var player = new PlayerWithUserGeneratedGuidKey
                {
                    Id = Guid.NewGuid(),
                    Firstname = "Ada",
                    Lastname = "Lovelace",
                    TeamId = teamId,
                    Team = team,
                };

                db.BulkInsertAll(new BulkInsertRequest<TeamWithUserGeneratedGuidKey>
                {
                    Entities = new[] { team },
                });
                db.BulkInsertAll(new BulkInsertRequest<PlayerWithUserGeneratedGuidKey>
                {
                    Entities = new[] { player },
                });

                var probe = new PlayerWithUserGeneratedGuidKey
                {
                    Id = Guid.NewGuid(),
                    Firstname = "probe",
                    Lastname = "probe",
                    TeamId = teamId,
                };

                var existing = db.BulkSelectExisting<PlayerWithUserGeneratedGuidKey, PlayerWithUserGeneratedGuidKey>(
                    new BulkSelectRequest<PlayerWithUserGeneratedGuidKey>
                    {
                        Items = new[] { probe },
                        KeyPropertyMappings = new[]
                        {
                            new KeyPropertyMapping
                            {
                                EntityPropertyName = "Team.Id",
                                ItemPropertyName = nameof(PlayerWithUserGeneratedGuidKey.TeamId),
                            },
                        },
                    });

                Assert.AreEqual(1, existing.Count);
                Assert.AreSame(probe, existing[0]);
            }
        }
    }
}