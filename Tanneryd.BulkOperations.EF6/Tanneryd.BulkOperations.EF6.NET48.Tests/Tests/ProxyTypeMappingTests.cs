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
using System.Data.Entity;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.EF6.Model;
using Tanneryd.BulkOperations.EF6.NET48.Tests.Models.DM.Prices;
using Tanneryd.BulkOperations.EF6.NET48.Tests.Models.DM.Teams.UsingUserGeneratedGuidKeys;
using Tanneryd.BulkOperations.EF6.NET48.Tests.Models.EF;

namespace Tanneryd.BulkOperations.EF6.NET48.Tests.Tests
{
    /// <summary>
    /// Mapping lookup must unwrap proxy-like CLR types (subclasses) to
    /// the mapped entity type. EF6 matches EntitySet via BaseType.Name but still
    /// calls Set(runtimeType) for table-name resolution, so subclasses fail until
    /// the type is normalized to the model entity type end-to-end.
    /// </summary>
    [TestClass]
    public class ProxyTypeMappingTests : BulkOperationTestBase
    {
        /// <summary>
        /// Stand-in for a lazy-loading / change-tracking proxy: a concrete subclass
        /// whose runtime type is not itself in the EF model.
        /// Must be [NotMapped] — EF6 otherwise discovers subclasses in this assembly
        /// as TPH types and migrator.Update() fails with AutomaticMigrationsDisabledException.
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
            using (var seed = new UnitTestContext())
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

            using (var db = new UnitTestContext())
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

            using (var verify = new UnitTestContext())
            {
                var fromDb = verify.Prices.Single(p => p.Id == id);
                Assert.AreEqual("Updated-via-proxy", fromDb.Name);
                Assert.AreEqual(2m, fromDb.Value);
            }
        }

        [TestMethod]
        public void BulkInsert_ShouldResolveMappings_WhenEntityIsProxySubclass()
        {
            using (var db = new UnitTestContext())
            {
                db.BulkInsertAll(new BulkInsertRequest<PriceProxy>
                {
                    Entities = new[]
                    {
                        new PriceProxy
                        {
                            Date = new DateTime(2023, 6, 1),
                            Name = "Inserted-via-proxy",
                            Value = 9m,
                        },
                    },
                });
            }

            using (var verify = new UnitTestContext())
            {
                var fromDb = verify.Prices.Single(p => p.Name == "Inserted-via-proxy");
                Assert.AreEqual(9m, fromDb.Value);
            }
        }

        /// <summary>
        /// Recursive M2M insert picks Source vs Target join columns by comparing
        /// the association end declaring type name to entity.GetType().Name. A proxy
        /// subclass name does not match, so CoachId/TeamId are swapped and the
        /// join insert fails FK checks (or links the wrong ids).
        /// </summary>
        [TestMethod]
        public void BulkInsert_RecursiveManyToMany_ShouldWriteJoinRows_WhenEntityIsProxySubclass()
        {
            var coachId = Guid.NewGuid();
            var teamId = Guid.NewGuid();

            using (var db = new UnitTestContext())
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
                    Entities = new[] { coach },
                    EnableRecursiveInsert = EnableRecursiveInsert.Yes,
                });
            }

            using (var verify = new UnitTestContext())
            {
                var coach = verify.CoachesWithUserGeneratedGuids
                    .Include(c => c.Teams)
                    .Single(c => c.Id == coachId);
                Assert.AreEqual(1, coach.Teams.Count);
                Assert.AreEqual(teamId, coach.Teams.Single().Id);
                Assert.AreEqual(
                    1,
                    verify.Database.SqlQuery<int>(
                        "SELECT COUNT(*) AS [Value] FROM [dbo].[CoachTeamsWithUserGeneratedGuid] WHERE [CoachId] = @p0 AND [TeamId] = @p1",
                        coachId,
                        teamId).Single(),
                    "Join row must use coach.Id in CoachId and team.Id in TeamId.");
            }
        }
    }
}
