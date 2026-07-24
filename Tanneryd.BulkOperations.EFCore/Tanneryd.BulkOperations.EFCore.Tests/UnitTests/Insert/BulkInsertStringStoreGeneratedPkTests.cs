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
using Tanneryd.BulkOperations.EFCore.Tests.Models.EF.StringStoreGeneratedPk;

namespace Tanneryd.BulkOperations.EFCore.Tests.UnitTests.Insert
{
    /// <summary>
    /// SelectNewEntities must use typed unset checks for store-generated
    /// non-Guid PKs. Comparing a string PK with dynamic == 0 throws
    /// RuntimeBinderException before any rows are inserted.
    /// </summary>
    [TestClass]
    public class BulkInsertStringStoreGeneratedPkTests
    {
        private static StringStoreGeneratedPkContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<StringStoreGeneratedPkContext>()
                .UseSqlServer(StringStoreGeneratedPkContext.ConnectionString)
                .Options;
            return new StringStoreGeneratedPkContext(options);
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
        public void BulkInsert_ShouldSucceed_WhenStoreGeneratedStringPrimaryKeyIsUnset()
        {
            using var db = CreateContext();

            var item = new CatalogItem
            {
                // Empty string is "unset" for string keys; null fails SqlBulkCopy
                // into a NOT NULL staging column.
                Code = "",
                Name = "widget",
            };

            db.BulkInsertAll(new BulkInsertRequest<CatalogItem>
            {
                Entities = new[] { item },
            });

            Assert.AreEqual(1, db.CatalogItems.Count());
            Assert.IsFalse(
                string.IsNullOrEmpty(item.Code),
                "Store-generated string PK must be written back via MERGE OUTPUT.");
            Assert.AreEqual(item.Code, db.CatalogItems.Single().Code);
        }

        [TestMethod]
        public void BulkInsert_ShouldSkip_WhenStoreGeneratedStringPrimaryKeyIsAlreadySet()
        {
            using var db = CreateContext();

            var item = new CatalogItem
            {
                Code = "SKU-1",
                Name = "preassigned",
            };

            db.BulkInsertAll(new BulkInsertRequest<CatalogItem>
            {
                Entities = new[] { item },
            });

            Assert.AreEqual(
                0,
                db.CatalogItems.Count(),
                "SelectNewEntities must treat a non-empty string PK as already set and skip insert.");
        }
    }
}
