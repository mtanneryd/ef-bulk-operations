using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.EF6.Model;
using Tanneryd.BulkOperations.EF6.Tests.DM.Blog;
using Tanneryd.BulkOperations.EF6.Tests.EF;

namespace Tanneryd.BulkOperations.EF6.Tests.Tests.Update
{
    [TestClass]
    public class BulkUpdateTests : BulkOperationTestBase
    {
        [TestInitialize]
        public void Initialize()
        {
            CleanupBlogContext();
            InitializeBlogContext();
        }

        [TestCleanup]
        public void CleanUp()
        {
            CleanupBlogContext();
        }

        [TestMethod]
        public void ModifiedEntityShouldBeUpdated()
        {
            using (var db = new BlogContext())
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
                    AllowNotNullSelfReferences = false,
                    SortUsingClusteredIndex = true,
                    Recursive = true
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