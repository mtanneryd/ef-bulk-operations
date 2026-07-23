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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.EFCore.Model;
using Tanneryd.BulkOperations.EFCore.Tests.Models.DM.Teams.UsingUserGeneratedGuidKeys;

namespace Tanneryd.BulkOperations.EFCore.Tests.UnitTests.Insert
{
    /// <summary>
    /// Recursive insert must tolerate Guid FKs that are already set (e.g. app
    /// code copied the parent PK onto the child). Comparing those values with
    /// dynamic == 0 throws RuntimeBinderException.
    /// </summary>
    [TestClass]
    public class BulkInsertAssignedGuidForeignKeyTests : BulkOperationTestBase
    {
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
        public void RecursiveInsert_ShouldSucceed_WhenGuidForeignKeyIsAlreadyAssigned()
        {
            using var db = Factory.CreateDbContext();

            var team = new TeamWithUserGeneratedGuidKey
            {
                Id = Guid.NewGuid(),
                Name = "Assigned-FK Team",
            };
            db.BulkInsertAll(new BulkInsertRequest<TeamWithUserGeneratedGuidKey>
            {
                Entities = new[] { team },
            });

            var player = new PlayerWithUserGeneratedGuidKey
            {
                Id = Guid.NewGuid(),
                Firstname = "Ada",
                Lastname = "Lovelace",
                TeamId = team.Id,
                Team = team,
            };

            db.BulkInsertAll(new BulkInsertRequest<PlayerWithUserGeneratedGuidKey>
            {
                Entities = new[] { player },
                EnableRecursiveInsert = EnableRecursiveInsert.Yes,
            });

            var stored = db.PlayersWithUserGeneratedGuids.Single();
            Assert.AreEqual(player.Id, stored.Id);
            Assert.AreEqual(team.Id, stored.TeamId);
            Assert.AreEqual(1, db.TeamsWithUserGeneratedGuids.Count());
        }

        [TestMethod]
        public void RecursiveInsert_ShouldSucceed_WhenBlogIdIsAlreadyAssignedOnPost()
        {
            using var db = Factory.CreateDbContext();

            var blog = new Blog { Name = "Assigned-FK Blog" };
            db.BulkInsertAll(new BulkInsertRequest<Blog>
            {
                Entities = new[] { blog },
            });

            var post = new Post
            {
                Blog = blog,
                BlogId = blog.Id,
                Text = "Post with BlogId already set",
            };

            db.BulkInsertAll(new BulkInsertRequest<Post>
            {
                Entities = new[] { post },
                EnableRecursiveInsert = EnableRecursiveInsert.Yes,
            });

            var stored = db.Posts.Single();
            Assert.AreEqual(post.Id, stored.Id);
            Assert.AreEqual(blog.Id, stored.BlogId);
            Assert.AreEqual(1, db.Blogs.Count());
        }

        [TestMethod]
        public void RecursiveInsert_ShouldStillWork_WhenGuidForeignKeyIsEmpty()
        {
            using var db = Factory.CreateDbContext();

            var blog = new Blog { Name = "Empty-FK Blog" };
            var post = new Post
            {
                Blog = blog,
                BlogId = Guid.Empty,
                Text = "Post with empty BlogId",
            };

            db.BulkInsertAll(new BulkInsertRequest<Post>
            {
                Entities = new[] { post },
                EnableRecursiveInsert = EnableRecursiveInsert.Yes,
            });

            var stored = db.Posts.Single();
            Assert.AreEqual(blog.Id, stored.BlogId);
            Assert.AreEqual(1, db.Blogs.Count());
        }
    }
}
