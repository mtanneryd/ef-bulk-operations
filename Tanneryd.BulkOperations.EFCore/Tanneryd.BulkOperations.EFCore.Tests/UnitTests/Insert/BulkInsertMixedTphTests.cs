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

namespace Tanneryd.BulkOperations.EFCore.Tests.UnitTests.Insert
{
    /// <summary>
    /// Mixed TPH batches must not stamp entities[0]'s discriminator on every row.
    /// With only PK+discriminator columns that corruption is silent (wrong typed
    /// DbSet counts). Fix by rejecting the mixed batch or inserting per concrete type.
    /// </summary>
    [TestClass]
    public class BulkInsertMixedTphTests
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
        public void BulkInsert_MixedTphBatch_ShouldPersistEachConcreteType_WhenRedComesFirst()
        {
            AssertMixedBatchPersistsCorrectly(new RedTag(), new BlueTag());
        }

        [TestMethod]
        public void BulkInsert_MixedTphBatch_ShouldPersistEachConcreteType_WhenBlueComesFirst()
        {
            AssertMixedBatchPersistsCorrectly(new BlueTag(), new RedTag());
        }

        private static void AssertMixedBatchPersistsCorrectly(TagBase first, TagBase second)
        {
            using var db = CreateContext();

            db.BulkInsertAll(new BulkInsertRequest<TagBase>
            {
                Entities = new[] { first, second },
                EnableRecursiveInsert = EnableRecursiveInsert.NoAndIgnoreGeneratedPrimaryKeys,
            });

            Assert.AreEqual(
                1,
                db.RedTags.Count(),
                "Expected exactly one RedTag; stamping entities[0]'s discriminator on the whole batch misclassifies the other row.");
            Assert.AreEqual(
                1,
                db.BlueTags.Count(),
                "Expected exactly one BlueTag; stamping entities[0]'s discriminator on the whole batch misclassifies the other row.");
            Assert.AreEqual(2, db.Tags.Count());
        }
    }
}
