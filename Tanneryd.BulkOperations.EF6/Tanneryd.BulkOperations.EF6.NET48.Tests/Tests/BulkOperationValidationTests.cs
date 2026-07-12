using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.EF6.Model;
using Tanneryd.BulkOperations.EF6.NET48.Tests.Models.DM.People;
using Tanneryd.BulkOperations.EF6.NET48.Tests.Models.DM.Prices;
using Tanneryd.BulkOperations.EF6.NET48.Tests.Models.EF;

namespace Tanneryd.BulkOperations.EF6.NET48.Tests.Tests
{
    [TestClass]
    public class BulkOperationValidationTests : BulkOperationTestBase
    {
        [TestMethod]
        public void BulkInsertAllShouldRejectNullRequest()
        {
            using var db = new UnitTestContext();
            Assert.ThrowsExactly<ArgumentNullException>(() => db.BulkInsertAll<Price>(null));
        }

        [TestMethod]
        public void BulkInsertAllShouldRejectNullEntities()
        {
            using var db = new UnitTestContext();
            var request = new BulkInsertRequest<Price> { Entities = null };
            Assert.ThrowsExactly<ArgumentNullException>(() => db.BulkInsertAll(request));
        }

        [TestMethod]
        public void BulkUpdateAllShouldRejectNullRequest()
        {
            using var db = new UnitTestContext();
            Assert.ThrowsExactly<ArgumentNullException>(() => db.BulkUpdateAll(null));
        }

        [TestMethod]
        public void BulkDeleteNotExistingShouldRejectNullRequest()
        {
            using var db = new UnitTestContext();
            Assert.ThrowsExactly<ArgumentNullException>(() =>
                db.BulkDeleteNotExisting<Person, Person>(null));
        }

        [TestMethod]
        public void BulkSelectExistingShouldRejectEmptyKeyMappings()
        {
            using var db = new UnitTestContext();
            var request = new BulkSelectRequest<Person>(Array.Empty<string>(), new[] { new Person() });
            Assert.ThrowsExactly<ArgumentException>(() => db.BulkSelectExisting<Person, Person>(request));
        }
    }
}
