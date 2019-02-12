using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.EF6.Model;
using Tanneryd.BulkOperations.EF6.Tests.DM.Teams.UsingUserGeneratedGuidKeys;
using Tanneryd.BulkOperations.EF6.Tests.EF;

namespace Tanneryd.BulkOperations.EF6.Tests
{
    [TestClass]
    public class BulkInsertTeamTests : BulkOperationTestBase
    {
        [TestInitialize]
        public void Initialize()
        {
            InitializeTeamContext();
            CleanUp(); // make sure we start from scratch, previous tests might have been aborted before cleanup

        }

        [TestCleanup]
        public void CleanUp()
        {
            CleanupTeamContext();
        }

        [TestMethod]
        public void OrderOfNExpected()
        {
            using (var db = new UserGeneratedTeamContext())
            {
                var coaches = new List<Coach>();
                for (int i = 0; i < 1000; i++)
                {
                    var c = new Coach
                    {
                        Id = Guid.NewGuid(),
                        Firstname = $"Coach {i}",
                        Lastname = $"Lastname",
                    };
                    for (int j = 0; j < 25; j++)
                    {
                        var t = new Team
                        {
                            Id = Guid.NewGuid(),
                            Name = $"Team {j}"
                        };
                        c.Teams.Add(t);
                    }

                    coaches.Add(c);
                }
               

                db.BulkInsertAll(new BulkInsertRequest<Coach>
                {
                    Entities = coaches,
                    Recursive = true
                });

                var actual = db.Coaches
                    .Include(c => c.Teams)
                    .ToArray();

                Assert.AreEqual(1000, actual.Count());
                foreach (var coach in actual.ToArray())
                {
                    Assert.AreEqual(25, coach.Teams.Count);
                }
            }
        }

        [TestMethod]
        public void AlreadyExistingEntityWithUserGeneratedKeyShouldNotBeInserted()
        {
            using (var db = new UserGeneratedTeamContext())
            {
                var team1 = new Team
                {
                    Id = Guid.NewGuid(),
                    Name = "Team #1",
                };

                db.BulkInsertAll(new BulkInsertRequest<Team>
                {
                    Entities = new[] {team1}.ToList()
                });


                Assert.AreEqual(1, db.Teams.Count());

                db.BulkInsertAll(new BulkInsertRequest<Team>
                {
                    Entities = new[] {team1}.ToList()
                });

                Assert.AreEqual(1, db.Teams.Count());

            }
        }

        [TestMethod]
        public void AlreadyExistingEntityWithDbGeneratedKeyShouldNotBeInserted()
        {
            using (var db = new DbGeneratedTeamContext())
            {
                var team1 = new DM.Teams.UsingDbGeneratedGuidKeys.Team
                {
                    Name = "Team #1",
                };

                db.BulkInsertAll(new BulkInsertRequest<DM.Teams.UsingDbGeneratedGuidKeys.Team>
                {
                    Entities = new[] {team1}.ToList()
                });


                Assert.AreEqual(1, db.Teams.Count());

                db.BulkInsertAll(new BulkInsertRequest<DM.Teams.UsingDbGeneratedGuidKeys.Team>
                {
                    Entities = new[] {team1}.ToList()
                });

                Assert.AreEqual(1, db.Teams.Count());

            }
        }
    }
}
