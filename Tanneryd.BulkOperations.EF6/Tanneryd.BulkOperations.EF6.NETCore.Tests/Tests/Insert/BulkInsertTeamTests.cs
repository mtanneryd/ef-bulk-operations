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
using Tanneryd.BulkOperations.EF6.NETCore.Tests.Models.DM.Teams.UsingDbGeneratedGuidKeys;
using Tanneryd.BulkOperations.EF6.NETCore.Tests.Models.DM.Teams.UsingUserGeneratedGuidKeys;
using Tanneryd.BulkOperations.EF6.NETCore.Tests.Models.EF;

namespace Tanneryd.BulkOperations.EF6.NETCore.Tests.Tests.Insert
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
                var coaches = new List<CoachWithUserGeneratedGuidKey>();
                for (int i = 0; i < 1000; i++)
                {
                    var c = new CoachWithUserGeneratedGuidKey
                    {
                        Id = Guid.NewGuid(),
                        Firstname = $"Coach {i}",
                        Lastname = $"Lastname",
                    };
                    for (int j = 0; j < 25; j++)
                    {
                        var t = new TeamWithUserGeneratedGuidKey()
                        {
                            Id = Guid.NewGuid(),
                            Name = $"Team {j}"
                        };
                        c.Teams.Add(t);
                    }

                    coaches.Add(c);
                }


                db.BulkInsertAll(new BulkInsertRequest<CoachWithUserGeneratedGuidKey>
                {
                    Entities = coaches,
                    EnableRecursiveInsert = EnableRecursiveInsert.Yes
                });

                var actual = db.CoachesWithUserGeneratedGuids
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
            using (var db = new UnitTestContext())
            {
                var team1 = new TeamWithUserGeneratedGuidKey()
                {
                    Id = Guid.NewGuid(),
                    Name = "Team #1",
                };

                db.BulkInsertAll(new BulkInsertRequest<TeamWithUserGeneratedGuidKey>
                {
                    Entities = new[] { team1 }.ToList()
                });


                Assert.AreEqual(1, db.TeamsWithUserGeneratedGuids.Count());

                db.BulkInsertAll(new BulkInsertRequest<TeamWithUserGeneratedGuidKey>
                {
                    Entities = new[] { team1 }.ToList()
                });

                Assert.AreEqual(1, db.TeamsWithUserGeneratedGuids.Count());

            }
        }

        [TestMethod]
        public void AlreadyExistingEntityWithDbGeneratedKeyShouldNotBeInserted()
        {
            using (var db = new UnitTestContext())
            {
                var team1 = new TeamWithDbGeneratedGuidKey()
                {
                    Name = "Team #1",
                };

                db.BulkInsertAll(new BulkInsertRequest<TeamWithDbGeneratedGuidKey>
                {
                    Entities = new[] { team1 }.ToList()
                });


                Assert.AreEqual(1, db.TeamsWithDbGeneratedGuids.Count());

                db.BulkInsertAll(new BulkInsertRequest<TeamWithDbGeneratedGuidKey>
                {
                    Entities = new[] { team1 }.ToList()
                });

                Assert.AreEqual(1, db.TeamsWithDbGeneratedGuids.Count());

            }
        }
    }
}
