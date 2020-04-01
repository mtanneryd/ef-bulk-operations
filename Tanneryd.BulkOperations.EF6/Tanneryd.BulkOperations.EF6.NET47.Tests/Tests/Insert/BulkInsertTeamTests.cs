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
using System.Data.Entity;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.EF6.Model;
using Tanneryd.BulkOperations.EF6.NET47.Tests.Models.DM.Teams.UsingUserGeneratedGuidKeys;
using Tanneryd.BulkOperations.EF6.NET47.Tests.Models.EF;

namespace Tanneryd.BulkOperations.EF6.NET47.Tests.Tests.Insert
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
                    EnableRecursiveInsert = EnableRecursiveInsert.Yes
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
                    Entities = new[] { team1 }.ToList()
                });


                Assert.AreEqual(1, db.Teams.Count());

                db.BulkInsertAll(new BulkInsertRequest<Team>
                {
                    Entities = new[] { team1 }.ToList()
                });

                Assert.AreEqual(1, db.Teams.Count());

            }
        }

        [TestMethod]
        public void AlreadyExistingEntityWithDbGeneratedKeyShouldNotBeInserted()
        {
            using (var db = new DbGeneratedTeamContext())
            {
                var team1 = new Models.DM.Teams.UsingDbGeneratedGuidKeys.Team
                {
                    Name = "Team #1",
                };

                db.BulkInsertAll(new BulkInsertRequest<Models.DM.Teams.UsingDbGeneratedGuidKeys.Team>
                {
                    Entities = new[] { team1 }.ToList()
                });


                Assert.AreEqual(1, db.Teams.Count());

                db.BulkInsertAll(new BulkInsertRequest<Models.DM.Teams.UsingDbGeneratedGuidKeys.Team>
                {
                    Entities = new[] { team1 }.ToList()
                });

                Assert.AreEqual(1, db.Teams.Count());

            }
        }
    }
}
