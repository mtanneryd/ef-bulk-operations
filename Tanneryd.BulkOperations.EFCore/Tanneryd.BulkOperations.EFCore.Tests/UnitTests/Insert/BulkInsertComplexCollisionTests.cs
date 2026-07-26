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

using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.EFCore.Model;
using Tanneryd.BulkOperations.EFCore.Tests.Models.EF.ComplexCollision;

namespace Tanneryd.BulkOperations.EFCore.Tests.UnitTests.Insert
{
    /// <summary>
    /// Sibling complex properties with the same leaf CLR names must flatten
    /// under distinct store column keys (not collide on Street/City).
    /// </summary>
    [TestClass]
    public class BulkInsertComplexCollisionTests
    {
        private static ComplexCollisionContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<ComplexCollisionContext>()
                .UseSqlServer(ComplexCollisionContext.ConnectionString)
                .Options;
            return new ComplexCollisionContext(options);
        }

        [TestInitialize]
        public void Initialize()
        {
            using var ctx = CreateContext();
            ctx.Database.EnsureDeleted();
            ctx.Database.EnsureCreated();
        }

        [TestCleanup]
        public void Cleanup()
        {
            using var ctx = CreateContext();
            ctx.Database.EnsureDeleted();
        }

        [TestMethod]
        public void BulkInsert_ShouldPersistSiblingComplexProperties_WithSameLeafNames()
        {
            using (var db = CreateContext())
            {
                db.BulkInsertAll(new BulkInsertRequest<ComplexCollisionContact>
                {
                    Entities = new[]
                    {
                        new ComplexCollisionContact
                        {
                            Name = "Ada",
                            Home = new CollisionAddress { Street = "1 Home St", City = "Stockholm" },
                            Work = new CollisionAddress { Street = "2 Work Ave", City = "Uppsala" },
                        },
                    },
                });
            }

            using (var verify = CreateContext())
            {
                var row = verify.Contacts.Single();
                Assert.AreEqual("Ada", row.Name);
                Assert.AreEqual("1 Home St", row.Home.Street);
                Assert.AreEqual("Stockholm", row.Home.City);
                Assert.AreEqual("2 Work Ave", row.Work.Street);
                Assert.AreEqual("Uppsala", row.Work.City);
            }
        }
    }
}
