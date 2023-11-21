/*
* Copyright ©  2017-2019 Tånneryd IT AB
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
using Tanneryd.BulkOperations.EF6.NET47.Tests.Models.DM.Blog;
using Tanneryd.BulkOperations.EF6.NET47.Tests.Models.DM.Prices;
using Tanneryd.BulkOperations.EF6.NET47.Tests.Models.EF;

namespace Tanneryd.BulkOperations.EF6.NET47.Tests.Tests.Update
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

        [TestMethod]
        public void ModifiedEntityShouldBeUpdated()
        {
            using (var db = new UnitTestContext())
            {
                var blog = new Blog { Name = "My Blog" };
                var firstPost = new Post
                {
                    Text = "My first blogpost.",
                    PostKeywords = new List<Keyword>() { new Keyword { Text = "first" } }
                };
                var secondPost = new Post
                {
                    Text = "My second blogpost.",
                    PostKeywords = new List<Keyword>() { new Keyword { Text = "second" } }
                };
                blog.BlogPosts.Add(firstPost);
                blog.BlogPosts.Add(secondPost);
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