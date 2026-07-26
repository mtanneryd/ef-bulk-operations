using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.EFCore.Model;

namespace Tanneryd.BulkOperations.EFCore.Tests.UnitTests
{
    [TestClass]
    public class BulkOperationValidationTests : BulkOperationTestBase
    {
        [TestInitialize]
        public void Initialize()
        {
            // Ensure LocalDB catalog exists after a drop (same as other fixtures).
            InitializeUnitTestContext();
        }

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
        /// Regression: empty Items previously deleted the entire SqlConditions
        /// window. Safe default must reject empty Items.
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

        /// <summary>
        /// Unresolved key property names must fail loudly. Silently ignoring them
        /// previously made BulkDeleteNotExisting a no-op (no delete, no error).
        /// </summary>
        [TestMethod]
        public void BulkDeleteNotExistingShouldRejectUnresolvedKeyPropertyNames()
        {
            using var db = Factory.CreateDbContext();
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
            using var db = Factory.CreateDbContext();
            var request = new BulkDeleteRequest<Person>(
                new[] { new SqlCondition("MotherId", 1L) },
                new[] { "FirstName", "NotAMappedProperty" },
                new[] { new Person { FirstName = "Kept", LastName = "Child" } });

            var ex = Assert.ThrowsExactly<ArgumentException>(() =>
                db.BulkDeleteNotExisting<Person, Person>(request));
            StringAssert.Contains(ex.Message, "NotAMappedProperty");
        }

        /// <summary>
        /// Unresolved key names must fail on BulkSelect (same as delete). Silently
        /// filtering them previously returned empty matches with no error.
        /// </summary>
        [TestMethod]
        public void BulkSelectShouldRejectUnresolvedKeyPropertyNames()
        {
            using var db = Factory.CreateDbContext();
            var request = new BulkSelectRequest<Person>(
                new[] { "FirstNam" },
                new[] { new Person { FirstName = "Ada" } });

            var ex = Assert.ThrowsExactly<ArgumentException>(() =>
                db.BulkSelect<Person, Person>(request));
            StringAssert.Contains(ex.Message, "FirstNam");
        }

        [TestMethod]
        public void BulkSelectNotExistingShouldRejectUnresolvedKeyPropertyNames()
        {
            using var db = Factory.CreateDbContext();
            var request = new BulkSelectRequest<Person>(
                new[] { "FirstNam" },
                new[] { new Person { FirstName = "Ada" } });

            var ex = Assert.ThrowsExactly<ArgumentException>(() =>
                db.BulkSelectNotExisting<Person, Person>(request));
            StringAssert.Contains(ex.Message, "FirstNam");
        }

        [TestMethod]
        public void BulkSelectExistingShouldRejectUnresolvedKeyPropertyNames()
        {
            using var db = Factory.CreateDbContext();
            var request = new BulkSelectRequest<Person>(
                new[] { "FirstNam" },
                new[] { new Person { FirstName = "Ada" } });

            var ex = Assert.ThrowsExactly<ArgumentException>(() =>
                db.BulkSelectExisting<Person, Person>(request));
            StringAssert.Contains(ex.Message, "FirstNam");
        }

        [TestMethod]
        public void BulkSelectShouldRejectWhenAnyKeyPropertyNameIsUnresolved()
        {
            using var db = Factory.CreateDbContext();
            var request = new BulkSelectRequest<Person>(
                new[] { "FirstName", "NotAMappedProperty" },
                new[] { new Person { FirstName = "Ada" } });

            var ex = Assert.ThrowsExactly<ArgumentException>(() =>
                db.BulkSelect<Person, Person>(request));
            StringAssert.Contains(ex.Message, "NotAMappedProperty");
        }

        /// <summary>
        /// BulkUpdate must reject typos with ArgumentException (same style as
        /// Select/Delete), not KeyNotFoundException from dictionary indexing.
        /// </summary>
        [TestMethod]
        public void BulkUpdateAllShouldRejectUnresolvedKeyPropertyNames()
        {
            using var db = Factory.CreateDbContext();
            var price = new Price
            {
                Date = new DateTime(2023, 5, 29),
                Name = "A",
                Value = 1m
            };

            var ex = Assert.ThrowsExactly<ArgumentException>(() =>
                db.BulkUpdateAll(new BulkUpdateRequest
                {
                    Entities = new[] { price },
                    KeyPropertyNames = new[] { "Dat" },
                    UpdatedPropertyNames = new[] { nameof(Price.Value) },
                }));

            StringAssert.Contains(ex.Message, "Dat");
            StringAssert.Contains(ex.Message, nameof(BulkUpdateRequest.KeyPropertyNames));
        }

        [TestMethod]
        public void BulkUpdateAllShouldRejectUnresolvedUpdatedPropertyNames()
        {
            using var db = Factory.CreateDbContext();
            var price = new Price
            {
                Date = new DateTime(2023, 5, 29),
                Name = "A",
                Value = 1m
            };

            var ex = Assert.ThrowsExactly<ArgumentException>(() =>
                db.BulkUpdateAll(new BulkUpdateRequest
                {
                    Entities = new[] { price },
                    UpdatedPropertyNames = new[] { "Valu" },
                }));

            StringAssert.Contains(ex.Message, "Valu");
            StringAssert.Contains(ex.Message, nameof(BulkUpdateRequest.UpdatedPropertyNames));
        }
    }
}
