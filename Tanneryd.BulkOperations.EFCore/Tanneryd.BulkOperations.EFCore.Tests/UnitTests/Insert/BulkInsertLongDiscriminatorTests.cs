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
using Tanneryd.BulkOperations.EFCore.Tests.Models.EF.LongDiscriminator;

namespace Tanneryd.BulkOperations.EFCore.Tests.UnitTests.Insert
{
    /// <summary>
    /// String discriminator values longer than 128 characters must survive the
    /// temp-table staging path. The staging column used to be hard-coded to
    /// nvarchar(128); truncated values then failed to MERGE-match, silently
    /// misclassifying rows.
    /// </summary>
    [TestClass]
    public class BulkInsertLongDiscriminatorTests
    {
        private const string ConnectionString =
            @"data source=(localdb)\MSSQLLocalDB;initial catalog=Tanneryd.BulkOperations.EFCore.Tests.LongDiscriminator;persist security info=True;Integrated Security=SSPI;MultipleActiveResultSets=True;TrustServerCertificate=true";

        private static LongDiscriminatorContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<LongDiscriminatorContext>()
                .UseSqlServer(ConnectionString)
                .Options;
            return new LongDiscriminatorContext(options);
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
        public void BulkInsert_ShouldPersistDiscriminator_WhenValueExceeds128Chars()
        {
            using var db = CreateContext();

            db.BulkInsertAll(new BulkInsertRequest<LongTagBase>
            {
                Entities = new LongTagBase[] { new LongRedTag(), new LongBlueTag() },
                EnableRecursiveInsert = EnableRecursiveInsert.NoAndIgnoreGeneratedPrimaryKeys,
            });

            Assert.AreEqual(
                1,
                db.RedTags.Count(),
                "Expected exactly one LongRedTag; a truncated staging discriminator misclassifies the row.");
            Assert.AreEqual(
                1,
                db.BlueTags.Count(),
                "Expected exactly one LongBlueTag; a truncated staging discriminator misclassifies the row.");
            Assert.AreEqual(2, db.Tags.Count());
        }

        [TestMethod]
        public void BulkInsert_MixedLongDiscriminatorBatch_ShouldRetrieveGeneratedKeys()
        {
            using var db = CreateContext();

            var red = new LongRedTag();
            var blue = new LongBlueTag();
            db.BulkInsertAll(new BulkInsertRequest<LongTagBase>
            {
                Entities = new LongTagBase[] { red, blue },
                EnableRecursiveInsert = EnableRecursiveInsert.Yes,
            });

            Assert.AreNotEqual(0, red.Id);
            Assert.AreNotEqual(0, blue.Id);
            Assert.AreEqual(1, db.RedTags.Count());
            Assert.AreEqual(1, db.BlueTags.Count());
        }
    }
}
