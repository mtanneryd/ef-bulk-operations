using System;
using System.Collections.Generic;
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
        public void AlreadyExistingEntityWithUserGeneratedKeyShouldNotBeInserted()
        {
            using (var db = new TeamContext())
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
    }
}
