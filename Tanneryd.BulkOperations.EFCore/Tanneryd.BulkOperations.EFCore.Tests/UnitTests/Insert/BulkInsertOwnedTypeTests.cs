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
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.EFCore.Model;
using Tanneryd.BulkOperations.EFCore.Tests.Models.EF.OwnedType;

namespace Tanneryd.BulkOperations.EFCore.Tests.UnitTests.Insert
{
    /// <summary>
    /// Owned types are unsupported: table-sharing OwnsOne columns are omitted
    /// and OwnsMany needs a separate path. Bulk ops must reject clearly.
    /// </summary>
    [TestClass]
    public class BulkInsertOwnedTypeTests
    {
        private static OwnedTypeContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<OwnedTypeContext>()
                .UseSqlServer(OwnedTypeContext.ConnectionString)
                .Options;
            return new OwnedTypeContext(options);
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
        public void BulkInsert_ShouldRejectEntity_WithOwnedNavigations()
        {
            using var db = CreateContext();

            var ex = Assert.ThrowsExactly<ArgumentException>(() =>
                db.BulkInsertAll(new BulkInsertRequest<OwnedCustomer>
                {
                    Entities = new[]
                    {
                        new OwnedCustomer
                        {
                            Name = "Ada",
                            Address = new OwnedAddress { Street = "1 Main", City = "Stockholm" },
                        },
                    },
                }));

            StringAssert.Contains(ex.Message, "owned", StringComparison.OrdinalIgnoreCase);
            StringAssert.Contains(ex.Message, nameof(OwnedCustomer.Address));
            StringAssert.Contains(ex.Message, nameof(OwnedCustomer.Lines));
        }

        [TestMethod]
        public void BulkInsert_Recursive_ShouldRejectEntity_WithOwnedNavigations()
        {
            using var db = CreateContext();

            var ex = Assert.ThrowsExactly<ArgumentException>(() =>
                db.BulkInsertAll(new BulkInsertRequest<OwnedCustomer>
                {
                    Entities = new[]
                    {
                        new OwnedCustomer
                        {
                            Name = "Grace",
                            Address = new OwnedAddress { Street = "2 Main", City = "Uppsala" },
                            Lines = { new OwnedOrderLine { Sku = "A", Quantity = 1 } },
                        },
                    },
                    EnableRecursiveInsert = EnableRecursiveInsert.Yes,
                }));

            StringAssert.Contains(ex.Message, "owned", StringComparison.OrdinalIgnoreCase);
        }

        [TestMethod]
        public void BulkUpdate_ShouldRejectEntity_WithOwnedNavigations()
        {
            using var db = CreateContext();
            db.Customers.Add(new OwnedCustomer
            {
                Name = "seed",
                Address = new OwnedAddress { Street = "Seed", City = "Malmo" },
            });
            db.SaveChanges();

            var customer = db.Customers.Single();
            customer.Name = "updated";

            var ex = Assert.ThrowsExactly<ArgumentException>(() =>
                db.BulkUpdateAll(new BulkUpdateRequest
                {
                    Entities = new[] { customer },
                    UpdatedPropertyNames = new[] { nameof(OwnedCustomer.Name) },
                }));

            StringAssert.Contains(ex.Message, "owned", StringComparison.OrdinalIgnoreCase);
        }
    }
}
