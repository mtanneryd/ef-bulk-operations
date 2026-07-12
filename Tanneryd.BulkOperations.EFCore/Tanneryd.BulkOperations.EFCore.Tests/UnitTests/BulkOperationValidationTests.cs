using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.EFCore.Model;

namespace Tanneryd.BulkOperations.EFCore.Tests.UnitTests
{
    [TestClass]
    public class BulkOperationValidationTests : BulkOperationTestBase
    {
        [TestMethod]
        public void BulkInsertAllShouldRejectNullRequest()
        {
            using var db = Factory.CreateDbContext();
            Assert.ThrowsExactly<ArgumentNullException>(() => db.BulkInsertAll<Price>(null));
        }

        [TestMethod]
        public void BulkInsertAllShouldRejectNullEntities()
        {
            using var db = Factory.CreateDbContext();
            var request = new BulkInsertRequest<Price> { Entities = null };
            Assert.ThrowsExactly<ArgumentNullException>(() => db.BulkInsertAll(request));
        }

        [TestMethod]
        public void BulkUpdateAllShouldRejectNullRequest()
        {
            using var db = Factory.CreateDbContext();
            Assert.ThrowsExactly<ArgumentNullException>(() => db.BulkUpdateAll(null));
        }

        [TestMethod]
        public void BulkDeleteNotExistingShouldRejectNullRequest()
        {
            using var db = Factory.CreateDbContext();
            Assert.ThrowsExactly<ArgumentNullException>(() =>
                db.BulkDeleteNotExisting<Person, Person>(null));
        }

        [TestMethod]
        public void BulkSelectExistingShouldRejectEmptyKeyMappings()
        {
            using var db = Factory.CreateDbContext();
            var request = new BulkSelectRequest<Person>(Array.Empty<string>(), new[] { new Person() });
            Assert.ThrowsExactly<ArgumentException>(() => db.BulkSelectExisting<Person, Person>(request));
        }
    }
}
