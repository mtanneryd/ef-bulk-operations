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

        /// <summary>
        /// Regression for review finding H5: empty Items currently deletes the
        /// entire SqlConditions window. Safe default must reject empty Items.
        /// </summary>
        [TestMethod]
        public void BulkDeleteNotExistingShouldRejectEmptyItems()
        {
            using var db = Factory.CreateDbContext();
            var request = new BulkDeleteRequest<Person>(
                new[] { new SqlCondition("MotherId", 1L) },
                new[] { "FirstName", "LastName" },
                Array.Empty<Person>());

            Assert.ThrowsExactly<ArgumentException>(() =>
                db.BulkDeleteNotExisting<Person, Person>(request));
        }

        [TestMethod]
        public void BulkDeleteNotExistingShouldAllowEmptyItemsWhenOptInIsSet()
        {
            using var db = Factory.CreateDbContext();
            var request = new BulkDeleteRequest<Person>(
                new[] { new SqlCondition("MotherId", 1L) },
                new[] { "FirstName", "LastName" },
                Array.Empty<Person>())
            {
                AllowDeleteAllMatchingConditions = true
            };

            // Validation must accept empty Items when the opt-in flag is set.
            // Execution may still fail later (e.g. no matching rows); that is fine.
            try
            {
                db.BulkDeleteNotExisting<Person, Person>(request);
            }
            catch (ArgumentException)
            {
                Assert.Fail("Empty Items with AllowDeleteAllMatchingConditions should not be rejected.");
            }
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
