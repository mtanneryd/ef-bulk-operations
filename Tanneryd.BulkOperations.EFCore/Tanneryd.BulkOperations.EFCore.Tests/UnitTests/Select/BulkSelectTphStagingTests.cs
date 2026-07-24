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
using Tanneryd.BulkOperations.EFCore.Tests.Models.EF.DiscriminatorOnly;

namespace Tanneryd.BulkOperations.EFCore.Tests.UnitTests.Select
{
    /// <summary>
    /// Select/Delete staging for TPH types must not add a discriminator column to
    /// the temp table/DataTable unless the materializer also writes discriminator
    /// values. Key-only staging (Id + rowno) is enough for these APIs. EF Core
    /// already omits the discriminator here; these tests guard that parity.
    /// </summary>
    [TestClass]
    public class BulkSelectTphStagingTests
    {
        private const string ConnectionString =
            @"data source=(localdb)\MSSQLLocalDB;initial catalog=Tanneryd.BulkOperations.EFCore.Tests.DiscriminatorOnly;persist security info=True;Integrated Security=SSPI;MultipleActiveResultSets=True;TrustServerCertificate=true";

        private static DiscriminatorOnlyContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<DiscriminatorOnlyContext>()
                .UseSqlServer(ConnectionString)
                .Options;
            return new DiscriminatorOnlyContext(options);
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
        public void BulkSelectExisting_ShouldSucceed_ForTphEntityWithDiscriminator()
        {
            using var db = CreateContext();
            var tags = InsertTwoRedTags(db);

            var existing = db.BulkSelectExisting<RedTag, RedTag>(
                new BulkSelectRequest<RedTag>(new[] { "Id" }, tags));

            Assert.AreEqual(2, existing.Count);
            CollectionAssert.AreEquivalent(
                tags.Select(t => t.Id).ToArray(),
                existing.Select(t => t.Id).ToArray());
        }

        [TestMethod]
        public void BulkSelectNotExisting_ShouldSucceed_ForTphEntityWithDiscriminator()
        {
            using var db = CreateContext();
            var tags = InsertTwoRedTags(db);
            var probe = new[]
            {
                tags[0],
                new RedTag { Id = tags[1].Id + 1000 },
            };

            var missing = db.BulkSelectNotExisting<RedTag, RedTag>(
                new BulkSelectRequest<RedTag>(new[] { "Id" }, probe));

            Assert.AreEqual(1, missing.Count);
            Assert.AreEqual(probe[1].Id, missing[0].Id);
        }

        [TestMethod]
        public void BulkSelect_ShouldSucceed_ForTphEntityWithDiscriminator()
        {
            using var db = CreateContext();
            var tags = InsertTwoRedTags(db);
            var keys = tags.Select(t => new RedTag { Id = t.Id }).ToArray();

            var selected = db.BulkSelect<RedTag, RedTag>(
                new BulkSelectRequest<RedTag>(new[] { "Id" }, keys));

            Assert.AreEqual(2, selected.Count);
            CollectionAssert.AreEquivalent(
                tags.Select(t => t.Id).ToArray(),
                selected.Select(t => t.Id).ToArray());
        }

        [TestMethod]
        public void BulkDeleteNotExisting_ShouldSucceed_ForTphEntityWithDiscriminator()
        {
            using var db = CreateContext();
            var tags = InsertTwoRedTags(db);

            db.BulkDeleteNotExisting<RedTag, RedTag>(new BulkDeleteRequest<RedTag>(
                new[] { new SqlCondition("Id", tags[1].Id) },
                new[] { "Id" })
            {
                Items = new[] { tags[0] }.ToList(),
            });

            Assert.AreEqual(1, db.RedTags.Count());
            Assert.AreEqual(tags[0].Id, db.RedTags.Single().Id);
        }

        private static RedTag[] InsertTwoRedTags(DiscriminatorOnlyContext db)
        {
            var tags = new[]
            {
                new RedTag(),
                new RedTag(),
            };

            db.BulkInsertAll(new BulkInsertRequest<RedTag>
            {
                Entities = tags,
                EnableRecursiveInsert = EnableRecursiveInsert.NoButRetrieveGeneratedPrimaryKeys,
            });

            Assert.IsTrue(tags.All(t => t.Id > 0), "Expected identity keys to be written back.");
            return tags;
        }
    }
}
