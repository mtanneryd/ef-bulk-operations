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
        [TestInitialize]
        public void Initialize()
        {
            // These tests open UnitTestContext directly; ensure the catalog exists
            // after a LocalDB drop (SqlClient reports that as "login failed").
            InitializeUnitTestContext();
        }

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

        /// <summary>
        /// Regression: empty Items previously deleted the entire SqlConditions
        /// window. Safe default must reject empty Items.
        /// </summary>
        [TestMethod]
        public void BulkDeleteNotExistingShouldRejectEmptyItems()
        {
            using var db = new UnitTestContext();
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
            using var db = new UnitTestContext();
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
            using var db = new UnitTestContext();
            var request = new BulkSelectRequest<Person>(Array.Empty<string>(), new[] { new Person() });
            Assert.ThrowsExactly<ArgumentException>(() => db.BulkSelectExisting<Person, Person>(request));
        }

        /// <summary>
        /// Unresolved key property names must fail loudly. Silently ignoring them
        /// previously made BulkDeleteNotExisting a no-op (no delete, no error).
        /// </summary>
        [TestMethod]
        public void BulkDeleteNotExistingShouldRejectUnresolvedKeyPropertyNames()
        {
            using var db = new UnitTestContext();
            var request = new BulkDeleteRequest<Person>(
                new[] { new SqlCondition("MotherId", 1L) },
                new[] { "FirstNam" },
                new[] { new Person { FirstName = "Kept", LastName = "Child" } });

            var ex = Assert.ThrowsExactly<ArgumentException>(() =>
                db.BulkDeleteNotExisting<Person, Person>(request));
            StringAssert.Contains(ex.Message, "FirstNam");
        }

        [TestMethod]
        public void BulkDeleteNotExistingShouldRejectWhenAnyKeyPropertyNameIsUnresolved()
        {
            using var db = new UnitTestContext();
            var request = new BulkDeleteRequest<Person>(
                new[] { new SqlCondition("MotherId", 1L) },
                new[] { "FirstName", "NotAMappedProperty" },
                new[] { new Person { FirstName = "Kept", LastName = "Child" } });

            var ex = Assert.ThrowsExactly<ArgumentException>(() =>
                db.BulkDeleteNotExisting<Person, Person>(request));
            StringAssert.Contains(ex.Message, "NotAMappedProperty");
        }
    }
}
