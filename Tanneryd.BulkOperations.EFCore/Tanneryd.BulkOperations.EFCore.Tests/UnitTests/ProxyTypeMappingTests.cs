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
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.EFCore.Model;
using Tanneryd.BulkOperations.EFCore.Tests.Models.DM.Teams.UsingUserGeneratedGuidKeys;

namespace Tanneryd.BulkOperations.EFCore.Tests.UnitTests
{
    /// <summary>
    /// Mapping lookup must unwrap proxy-like CLR types (subclasses) to the
    /// mapped entity ClrType. EF6 already matches on BaseType; EF Core keyed the
    /// dictionary by exact type and threw for subclasses.
    /// </summary>
    [TestClass]
    public class ProxyTypeMappingTests : BulkOperationTestBase
    {
        /// <summary>
        /// Stand-in for a lazy-loading / change-tracking proxy: a concrete subclass
        /// whose runtime type is not itself in the EF model.
        /// [NotMapped] keeps parity with EF6 (which auto-discovers subclasses in-assembly).
        /// </summary>
        [NotMapped]
        private sealed class PriceProxy : Price
        {
        }

        /// <summary>
        /// Stand-in proxy for recursive many-to-many insert. Join-side selection
        /// must compare the mapped entity type name, not the proxy runtime name.
        /// </summary>
        [NotMapped]
        private sealed class CoachProxy : CoachWithUserGeneratedGuidKey
        {
        }

        [TestInitialize]
        public void Initialize()
        {
            InitializeUnitTestContext();
            CleanupUnitTestContext();
        }

        [TestCleanup]
        public void CleanUp()
        {
            CleanupUnitTestContext();
        }

        [TestMethod]
        public void BulkUpdate_ShouldResolveMappings_WhenEntityIsProxySubclass()
        {
            int id;
            using (var seed = Factory.CreateDbContext())
            {
                var price = new Price
                {
                    Date = new DateTime(2023, 5, 29),
                    Name = "Original",
                    Value = 1m,
                };
                seed.Prices.Add(price);
                seed.SaveChanges();
                id = price.Id;
            }

            using (var db = Factory.CreateDbContext())
            {
                db.BulkUpdateAll(new BulkUpdateRequest
                {
                    Entities = new object[]
                    {
                        new PriceProxy
                        {
                            Id = id,
                            Date = new DateTime(2023, 5, 29),
                            Name = "Updated-via-proxy",
                            Value = 2m,
                        },
                    },
                    KeyPropertyNames = new[] { nameof(Price.Id) },
                    UpdatedPropertyNames = new[] { nameof(Price.Name), nameof(Price.Value) },
                });
            }

            using (var verify = Factory.CreateDbContext())
            {
                var fromDb = verify.Prices.Single(p => p.Id == id);
                Assert.AreEqual("Updated-via-proxy", fromDb.Name);
                Assert.AreEqual(2m, fromDb.Value);
            }
        }

        [TestMethod]
        public void BulkInsert_ShouldResolveMappings_WhenEntityIsProxySubclass()
        {
            using (var db = Factory.CreateDbContext())
            {
                db.BulkInsertAll(new BulkInsertRequest<PriceProxy>
                {
                    Entities =
                    [
                        new PriceProxy
                        {
                            Date = new DateTime(2023, 6, 1),
                            Name = "Inserted-via-proxy",
                            Value = 9m,
                        },
                    ],
                });
            }

            using (var verify = Factory.CreateDbContext())
            {
                var fromDb = verify.Prices.Single(p => p.Name == "Inserted-via-proxy");
                Assert.AreEqual(9m, fromDb.Value);
            }
        }

        /// <summary>
        /// Recursive M2M insert picks Source vs Target join columns by comparing
        /// the association end CLR type name to entity.GetType().Name. A proxy
        /// subclass name does not match, so CoachId/TeamId are swapped and the
        /// join insert fails FK checks (or links the wrong ids).
        /// </summary>
        [TestMethod]
        public void BulkInsert_RecursiveManyToMany_ShouldWriteJoinRows_WhenEntityIsProxySubclass()
        {
            var coachId = Guid.NewGuid();
            var teamId = Guid.NewGuid();

            using (var db = Factory.CreateDbContext())
            {
                var coach = new CoachProxy
                {
                    Id = coachId,
                    Firstname = "Proxy",
                    Lastname = "Coach",
                };
                coach.Teams.Add(new TeamWithUserGeneratedGuidKey
                {
                    Id = teamId,
                    Name = "Proxy Team",
                });

                db.BulkInsertAll(new BulkInsertRequest<CoachProxy>
                {
                    Entities = [coach],
                    EnableRecursiveInsert = EnableRecursiveInsert.Yes,
                });
            }

            using (var verify = Factory.CreateDbContext())
            {
                var coach = verify.CoachesWithUserGeneratedGuids
                    .Include(c => c.Teams)
                    .Single(c => c.Id == coachId);
                Assert.AreEqual(1, coach.Teams.Count);
                Assert.AreEqual(teamId, coach.Teams.Single().Id);
                Assert.AreEqual(
                    1,
                    verify.Database.SqlQueryRaw<int>(
                        "SELECT COUNT(*) AS [Value] FROM [dbo].[CoachTeamsWithUserGeneratedGuid] WHERE [CoachId] = {0} AND [TeamId] = {1}",
                        coachId,
                        teamId).Single(),
                    "Join row must use coach.Id in CoachId and team.Id in TeamId.");
            }
        }
    }
}
