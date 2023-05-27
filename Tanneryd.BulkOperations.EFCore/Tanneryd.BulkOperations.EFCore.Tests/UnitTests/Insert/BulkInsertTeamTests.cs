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
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.EFCore.Model;

namespace Tanneryd.BulkOperations.EFCore.Tests.UnitTests.Insert
{
    [TestClass]
    public class BulkInsertTeamTests : BulkOperationTestBase
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
        public void OrderOfNExpected()
        {
            using (var db = new UnitTestContext())
            {
                var coaches = new List<CoachWithUserGeneratedGuid>();
                for (int i = 0; i < 1000; i++)
                {
                    var c = new CoachWithUserGeneratedGuid
                    {
                        Id = Guid.NewGuid(),
                        Firstname = $"Coach {i}",
                        Lastname = $"Lastname",
                    };
                    for (int j = 0; j < 25; j++)
                    {
                        var t = new TeamWithUserGeneratedGuid()
                        {
                            Id = Guid.NewGuid(),
                            Name = $"Team {j}"
                        };
                        c.CoachTeamsWithUserGeneratedGuids.Add(new CoachTeamsWithUserGeneratedGuid { TeamWithUserGeneratedGuid = t});
                    }

                    coaches.Add(c);
                }


                db.BulkInsertAll(new BulkInsertRequest<CoachWithUserGeneratedGuid>
                {
                    Entities = coaches,
                    EnableRecursiveInsert = EnableRecursiveInsert.Yes
                });

                var actual = db.CoachWithUserGeneratedGuids
                    .Include(c => c.CoachTeamsWithUserGeneratedGuids)
                    .ThenInclude(ct=>ct.TeamWithUserGeneratedGuid)
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
            using (var db = new UnitTestContext())
            {
                var team1 = new TeamWithUserGeneratedGuid()
                {
                    Id = Guid.NewGuid(),
                    Name = "Team #1",
                };

                db.BulkInsertAll(new BulkInsertRequest<TeamWithUserGeneratedGuid>
                {
                    Entities = new[] { team1 }.ToList()
                });


                Assert.AreEqual(1, db.TeamWithUserGeneratedGuids.Count());

                db.BulkInsertAll(new BulkInsertRequest<TeamWithUserGeneratedGuid>
                {
                    Entities = new[] { team1 }.ToList()
                });

                Assert.AreEqual(1, db.TeamWithUserGeneratedGuids.Count());

            }
        }

        [TestMethod]
        public void AlreadyExistingEntityWithDbGeneratedKeyShouldNotBeInserted()
        {
            using (var db = new UnitTestContext())
            {
                var team1 = new TeamWithDbGeneratedGuid()
                {
                    Name = "Team #1",
                };

                db.BulkInsertAll(new BulkInsertRequest<TeamWithDbGeneratedGuid>
                {
                    Entities = new[] { team1 }.ToList()
                });


                Assert.AreEqual(1, db.TeamWithDbGeneratedGuids.Count());

                db.BulkInsertAll(new BulkInsertRequest<TeamWithDbGeneratedGuid>
                {
                    Entities = new[] { team1 }.ToList()
                });

                Assert.AreEqual(1, db.TeamWithDbGeneratedGuids.Count());

            }
        }
    }
}
