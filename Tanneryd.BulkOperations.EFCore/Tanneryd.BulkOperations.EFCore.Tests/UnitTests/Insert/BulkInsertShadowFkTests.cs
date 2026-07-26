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
using Tanneryd.BulkOperations.EFCore.Tests.Models.EF.ShadowFk;

namespace Tanneryd.BulkOperations.EFCore.Tests.UnitTests.Insert
{
    /// <summary>
    /// Shadow FK columns have no CLR property. Bulk insert must stage them via
    /// Entry and recursive insert must stamp them without NullReferenceException.
    /// </summary>
    [TestClass]
    public class BulkInsertShadowFkTests
    {
        private static ShadowFkContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<ShadowFkContext>()
                .UseSqlServer(ShadowFkContext.ConnectionString)
                .Options;
            return new ShadowFkContext(options);
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
        public void BulkInsert_ShouldPersistShadowForeignKey_WhenSetViaEntry()
        {
            int authorId;
            using (var db = CreateContext())
            {
                var author = new ShadowAuthor { Name = "Ada" };
                db.Authors.Add(author);
                db.SaveChanges();
                authorId = author.Id;

                var book = new ShadowBook { Title = "Notes" };
                db.Entry(book).Property("AuthorId").CurrentValue = authorId;

                db.BulkInsertAll(new BulkInsertRequest<ShadowBook>
                {
                    Entities = new[] { book },
                });
            }

            using (var verify = CreateContext())
            {
                var book = verify.Books.Include(b => b.Author).Single();
                Assert.AreEqual("Notes", book.Title);
                Assert.AreEqual(authorId, verify.Entry(book).Property("AuthorId").CurrentValue);
                Assert.AreEqual("Ada", book.Author.Name);
            }
        }

        [TestMethod]
        public void BulkInsert_Recursive_ShouldStampShadowForeignKey_OnDependent()
        {
            using (var db = CreateContext())
            {
                var author = new ShadowAuthor { Name = "Grace" };
                author.Books.Add(new ShadowBook { Title = "Compiler" });

                db.BulkInsertAll(new BulkInsertRequest<ShadowAuthor>
                {
                    Entities = new[] { author },
                    EnableRecursiveInsert = EnableRecursiveInsert.Yes,
                });
            }

            using (var verify = CreateContext())
            {
                Assert.AreEqual(1, verify.Authors.Count());
                var book = verify.Books.Include(b => b.Author).Single();
                Assert.AreEqual("Compiler", book.Title);
                Assert.AreEqual("Grace", book.Author.Name);
                Assert.AreEqual(
                    book.Author.Id,
                    verify.Entry(book).Property("AuthorId").CurrentValue);
            }
        }
    }
}
