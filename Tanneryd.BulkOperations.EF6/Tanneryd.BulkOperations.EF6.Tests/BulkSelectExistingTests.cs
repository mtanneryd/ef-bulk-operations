using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.EF6.Model;
using Tanneryd.BulkOperations.EF6.Tests.DM.Numbers;
using Tanneryd.BulkOperations.EF6.Tests.DM.Teams.UsingUserGeneratedGuidKeys;
using Tanneryd.BulkOperations.EF6.Tests.EF;

namespace Tanneryd.BulkOperations.EF6.Tests
{
    [TestClass]
    public class BulkSelectExistingTests : BulkOperationTestBase
    {
        [TestInitialize]
        public void Initialize()
        {
            InitializeNumberContext();
            InitializeTeamContext();
            CleanUp();
        }

        [TestCleanup]
        public void CleanUp()
        {
            CleanupNumberContext();
            CleanupTeamContext();
        }

        [TestMethod]
        public void SelectExistingFromTableWithUserGeneratedGuidAsPrimaryKeyShouldWork()
        {
            using (var db = new UserGeneratedTeamContext())
            {
                var teams = new List<Team>();
                for (int i = 0; i < 10; i++)
                {
                    teams.Add(new Team {Id = Guid.NewGuid(), Name =$"Team #{i}"});
                }

                db.BulkInsertAll(new BulkInsertRequest<Team>
                {
                    Entities = teams
                });

                for (int i = 10; i < 20; i++)
                {
                    teams.Add(new Team {Id = Guid.NewGuid(), Name =$"Team #{i}"});
                }

                var existingTeams = db.BulkSelectExisting<Team, Team>(new BulkSelectRequest<Team>(new []{"Id"}, teams));
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
            using (var db = new UserGeneratedTeamContext())
            {
                var teams = new List<Team>();
                for (int i = 0; i < 10; i++)
                {
                    teams.Add(new Team {Id = Guid.NewGuid(), Name =$"Team #{i}"});
                }

                db.BulkInsertAll(new BulkInsertRequest<Team>
                {
                    Entities = teams
                });

                for (int i = 10; i < 20; i++)
                {
                    teams.Add(new Team {Id = Guid.NewGuid(), Name =$"Team #{i}"});
                }

                var existingTeams = db.BulkSelectNotExisting<Team, Team>(new BulkSelectRequest<Team>(new []{"Id"}, teams));
                Assert.AreEqual(10, existingTeams.Count);
                for (int i = 0; i < 10; i++)
                {
                    Assert.AreEqual(teams[i+10].Id, existingTeams[i].Id);
                    Assert.AreEqual(teams[i+10].Name, existingTeams[i].Name);
                }
            }
        }

        [TestMethod]
        public void SelectExistingFromTableWithDbGeneratedGuidAsPrimaryKeyShouldWork()
        {
            using (var db = new DbGeneratedTeamContext())
            {
                var teams = new List<DM.Teams.UsingDbGeneratedGuidKeys.Team>();
                for (int i = 0; i < 10; i++)
                {
                    teams.Add(new DM.Teams.UsingDbGeneratedGuidKeys.Team { Name =$"Team #{i}"});
                }

                db.BulkInsertAll(new BulkInsertRequest<DM.Teams.UsingDbGeneratedGuidKeys.Team>
                {
                    Entities = teams
                });

                for (int i = 10; i < 20; i++)
                {
                    teams.Add(new DM.Teams.UsingDbGeneratedGuidKeys.Team { Name =$"Team #{i}"});
                }

                var existingTeams = db.BulkSelectExisting<DM.Teams.UsingDbGeneratedGuidKeys.Team, DM.Teams.UsingDbGeneratedGuidKeys.Team>(new BulkSelectRequest<DM.Teams.UsingDbGeneratedGuidKeys.Team>(new []{"Id"}, teams));
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
            using (var db = new DbGeneratedTeamContext())
            {
                var teams = new List<DM.Teams.UsingDbGeneratedGuidKeys.Team>();
                for (int i = 0; i < 10; i++)
                {
                    teams.Add(new DM.Teams.UsingDbGeneratedGuidKeys.Team {Name =$"Team #{i}"});
                }

                db.BulkInsertAll(new BulkInsertRequest<DM.Teams.UsingDbGeneratedGuidKeys.Team>
                {
                    Entities = teams
                });

                for (int i = 10; i < 20; i++)
                {
                    teams.Add(new DM.Teams.UsingDbGeneratedGuidKeys.Team { Name =$"Team #{i}"});
                }

                var existingTeams = db.BulkSelectNotExisting<DM.Teams.UsingDbGeneratedGuidKeys.Team, DM.Teams.UsingDbGeneratedGuidKeys.Team>(new BulkSelectRequest<DM.Teams.UsingDbGeneratedGuidKeys.Team>(new []{"Id"}, teams));
                Assert.AreEqual(10, existingTeams.Count);
                for (int i = 0; i < 10; i++)
                {
                    Assert.AreEqual(teams[i+10].Id, existingTeams[i].Id);
                    Assert.AreEqual(teams[i+10].Name, existingTeams[i].Name);
                }
            }
        }

        [TestMethod]
        public void PrimitiveTypeValuesMatchingExistingEntitiesShouldBeSelected()
        {
            using (var db = new NumberContext())
            {
                var now = DateTime.Now;

                var numbers = GenerateNumbers(1, 200, now).ToArray();
                db.BulkInsertAll(new BulkInsertRequest<Number>
                {
                    Entities = numbers,
                    Recursive = true
                });


                var nums = GenerateNumbers(50, 100, now)
                    .Select(n =>  n.Value )
                    .ToList();
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
                });

                for (int i = 0; i < 100; i++)
                {
                    Assert.AreEqual(nums[i], existingNumbers[i]);
                }
            }
        }

        [TestMethod]
        public void EntitiesOfDifferentTypeMatchingExistingEntitiesShouldBeSelected()
        {
            using (var db = new NumberContext())
            {
                var now = DateTime.Now;

                var numbers = GenerateNumbers(1, 200, now).ToArray();
                db.BulkInsertAll(new BulkInsertRequest<Number>
                {
                    Entities = numbers,
                    Recursive = true
                });


                var nums = GenerateNumbers(50, 100, now)
                    .Select(n => new Num { Val = n.Value })
                    .ToList();
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
                });

                for (int i = 0; i < 100; i++)
                {
                    Assert.AreSame(nums[i], existingNumbers[i]);
                }
            }
        }

        [TestMethod]
        public void EntitiesOfSameTypeMatchingExistingEntitiesShouldBeSelected()
        {
            using (var db = new NumberContext())
            {
                var now = DateTime.Now;

                var numbers = GenerateNumbers(1, 200, now).ToArray();
                db.BulkInsertAll(new BulkInsertRequest<Number>
                {
                    Entities = numbers,
                    Recursive = true
                });


                numbers = GenerateNumbers(50, 100, now).ToArray();
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
                });

                for (int i = 0; i < 100; i++)
                {
                    Assert.AreSame(numbers[i], existingNumbers[i]);
                }
            }
        }

        [TestMethod]
        public void ExistingEntitiesShouldBeSelectedUsingRuntimeTypes()
        {
            using (var db = new NumberContext())
            {
                var now = DateTime.Now;

                var numbers = GenerateNumbers(1, 200, now).ToArray();
                db.BulkInsertAll(new BulkInsertRequest<Number>
                {
                    Entities = numbers,
                    Recursive = true
                });


                numbers = GenerateNumbers(50, 100, now).ToArray();

                var request = typeof(BulkSelectRequest<>).MakeGenericType(typeof(Number));
                var r = Activator.CreateInstance(request, new [] { "Value" }, numbers.ToList(), null);

                Type ex = typeof(DbContextExtensions);
                MethodInfo mi = ex.GetMethod("BulkSelectExisting");
                MethodInfo miGeneric = mi.MakeGenericMethod(new[] {typeof(Number),typeof(Number)});
                object[] args = {db, r};
                var existingNumbers = (List<Number>) miGeneric.Invoke(null, args);

                for (int i = 0; i < 100; i++)
                {
                    Assert.AreSame(numbers[i], existingNumbers[i]);
                }
            }
        }
    }
}