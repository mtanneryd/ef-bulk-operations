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
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.EF6.Model;
using Tanneryd.BulkOperations.EF6.NET48.Tests.Models.DM.Blog;
using Tanneryd.BulkOperations.EF6.NET48.Tests.Models.DM.Invoice;
using Tanneryd.BulkOperations.EF6.NET48.Tests.Models.DM.Miscellaneous;
using Tanneryd.BulkOperations.EF6.NET48.Tests.Models.DM.Prices;
using Tanneryd.BulkOperations.EF6.NET48.Tests.Models.EF;

namespace Tanneryd.BulkOperations.EF6.NET48.Tests.Tests.Update
{
    [TestClass]
    public class BulkUpdateTests : BulkOperationTestBase
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
        public void ModifiedEntitiesWithNullableColumnShouldBeUpdated()
        {
            using var db1 = new UnitTestContext();
            using var db2 = new UnitTestContext();
            var initialPrices = new Price[]
            {
                new Price
                {
                    Date = new DateTime(2023, 5, 29),
                    Name = "A",
                    Value = 1m
                },
                new Price
                {
                    Date = new DateTime(2023, 5, 29),
                    Name = "B",
                    Value = null
                },
                new Price
                {
                    Date = new DateTime(2023, 5, 29),
                    Name = "C",
                    Value = null
                },
            };
            db1.Prices.AddRange(initialPrices);
            db1.SaveChanges();

            var updatedPrices = new Price[]
            {
                new Price
                {
                    Date = new DateTime(2023, 5, 29),
                    Name = "A",
                    Value = 1.50m
                },
                new Price
                {
                    Date = new DateTime(2023, 5, 29),
                    Name = "B",
                    Value = null
                },
                new Price
                {
                    Date = new DateTime(2023, 5, 29),
                    Name = "C",
                    Value = 2.0m
                },
            };

            db1.BulkUpdateAll(new BulkUpdateRequest
            {
                Entities = updatedPrices,
                KeyPropertyNames = new[] { "Date", "Name" },
                UpdatedPropertyNames = new[] { "Value" },
            });
            var prices = db2.Prices.OrderBy(p => p.Name).ToArray();
            Assert.AreEqual("A", prices[0].Name);
            Assert.AreEqual(new DateTime(2023, 5, 29), prices[0].Date);
            Assert.AreEqual(1.50m, prices[0].Value);
            Assert.AreEqual("B", prices[1].Name);
            Assert.AreEqual(new DateTime(2023, 5, 29), prices[1].Date);
            Assert.AreEqual(null, prices[1].Value);
            Assert.AreEqual("C", prices[2].Name);
            Assert.AreEqual(new DateTime(2023, 5, 29), prices[2].Date);
            Assert.AreEqual(2.0m, prices[2].Value);
        }

        /// <summary>
        /// Regression: BulkUpdateAll must resolve KeyPropertyNames to column names before
        /// matching TableColumn.Name. Invoice.Id maps to column PrimaryKey, so using the
        /// CLR property name as the key must still update.
        /// </summary>
        [TestMethod]
        public void BulkUpdate_ShouldMatchWhenKeyPropertyNameDiffersFromColumnName()
        {
            using var db = new UnitTestContext();
            var invoice = new Invoice
            {
                Id = Guid.NewGuid(),
                Net = 100m,
                Gross = 125m,
            };
            db.Invoices.Add(invoice);
            db.SaveChanges();

            invoice.Net = 200m;
            invoice.Gross = 250m;

            db.BulkUpdateAll(new BulkUpdateRequest
            {
                Entities = new[] { invoice },
                KeyPropertyNames = new[] { nameof(Invoice.Id) },
                UpdatedPropertyNames = new[] { nameof(Invoice.Net), nameof(Invoice.Gross) },
            });

            using var verify = new UnitTestContext();
            var updated = verify.Invoices.Single(i => i.Id == invoice.Id);
            Assert.AreEqual(200m, updated.Net);
            Assert.AreEqual(250m, updated.Gross);
        }

        /// <summary>
        /// UpdatedPropertyNames that only list key columns leave an empty UPDATE SET.
        /// Must reject with ArgumentException before generating invalid SQL.
        /// </summary>
        [TestMethod]
        public void BulkUpdateAllShouldRejectWhenUpdatedPropertyNamesLeaveNoColumns()
        {
            using var db = new UnitTestContext();
            var price = new Price
            {
                Date = new DateTime(2023, 5, 29),
                Name = "A",
                Value = 1m
            };
            db.Prices.Add(price);
            db.SaveChanges();

            price.Value = 2m;

            var ex = Assert.ThrowsExactly<ArgumentException>(() =>
                db.BulkUpdateAll(new BulkUpdateRequest
                {
                    Entities = new[] { price },
                    KeyPropertyNames = new[] { nameof(Price.Date), nameof(Price.Name) },
                    UpdatedPropertyNames = new[] { nameof(Price.Date), nameof(Price.Name) },
                }));

            StringAssert.Contains(ex.Message, "updat", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Tables with only key columns (no non-key mapped properties) must not
        /// emit UPDATE … SET with an empty assignment list.
        /// </summary>
        [TestMethod]
        public void BulkUpdateAllShouldRejectWhenEntityHasNoUpdatableColumns()
        {
            using var db = new UnitTestContext();
            var row = new EmptyTable();
            db.EmptyTables.Add(row);
            db.SaveChanges();

            var ex = Assert.ThrowsExactly<ArgumentException>(() =>
                db.BulkUpdateAll(new BulkUpdateRequest
                {
                    Entities = new[] { row },
                }));

            StringAssert.Contains(ex.Message, "updat", StringComparison.OrdinalIgnoreCase);
        }

        [TestMethod]
        public void ModifiedEntityShouldBeUpdated()
        {
            using (var db = new UnitTestContext())
            {
                var blog = new Blog { Name = "My Blog" };
                var firstPost = new Post
                {
                    Text = "My first blogpost.",
                    Keywords = new List<Keyword>() { new Keyword { Text = "first" } }
                };
                var secondPost = new Post
                {
                    Text = "My second blogpost.",
                    Keywords = new List<Keyword>() { new Keyword { Text = "second" } }
                };
                blog.Posts.Add(firstPost);
                blog.Posts.Add(secondPost);
                var req = new BulkInsertRequest<Blog>
                {
                    Entities = new[] { blog }.ToList(),
                    AllowNotNullSelfReferences = AllowNotNullSelfReferences.No,
                    SortUsingClusteredIndex = true,
                    EnableRecursiveInsert = EnableRecursiveInsert.Yes
                };
                var response = db.BulkInsertAll(req);

                var b = db.Blogs.Single();
                Assert.AreEqual("My Blog", b.Name);

                b.Name = "My (modified) Blog";
                db.BulkUpdateAll(new BulkUpdateRequest
                {
                    Entities = new [] { b },
                    KeyPropertyNames = new [] { "Id" }
                });

                b = db.Blogs.Single();
                Assert.AreEqual("My (modified) Blog", b.Name);
            }
        }

    }
}