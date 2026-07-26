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
using Tanneryd.BulkOperations.EFCore.Tests.Models.EF.ClientOnAdd;

namespace Tanneryd.BulkOperations.EFCore.Tests.UnitTests.Insert
{
    /// <summary>
    /// Non-PK ValueGeneratedOnAdd without a store default must stay in column
    /// mappings so client-set values are bulk-copied (not omitted → NULL).
    /// </summary>
    [TestClass]
    public class BulkInsertClientOnAddTests
    {
        private static ClientOnAddContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<ClientOnAddContext>()
                .UseSqlServer(ClientOnAddContext.ConnectionString)
                .Options;
            return new ClientOnAddContext(options);
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
        public void BulkInsert_ShouldPersistClientValue_OnNonPkValueGeneratedOnAddColumn()
        {
            var createdAt = new DateTime(2020, 5, 6, 12, 30, 0, DateTimeKind.Unspecified);

            using (var db = CreateContext())
            {
                db.BulkInsertAll(new BulkInsertRequest<ClientOnAddProbe>
                {
                    Entities = new[]
                    {
                        new ClientOnAddProbe
                        {
                            Name = "probe",
                            CreatedAt = createdAt,
                        },
                    },
                });
            }

            using (var verify = CreateContext())
            {
                var row = verify.Probes.Single();
                Assert.AreEqual("probe", row.Name);
                Assert.AreEqual(
                    createdAt,
                    row.CreatedAt,
                    "Omitting bare ValueGeneratedOnAdd non-PK columns drops the client value.");
            }
        }

        [TestMethod]
        public void MappingsExtractor_ShouldIncludeNonPkValueGeneratedOnAddColumn()
        {
            using var db = CreateContext();
            var mappings = new MappingsExtractor(db).GetMappings(typeof(ClientOnAddProbe));

            Assert.IsTrue(
                mappings.ColumnMappingByPropertyName.ContainsKey(nameof(ClientOnAddProbe.CreatedAt)),
                "Bare ValueGeneratedOnAdd non-PK columns must be mapped for bulk insert.");
            Assert.IsFalse(
                mappings.ColumnMappingByPropertyName[nameof(ClientOnAddProbe.CreatedAt)].IsStoreGenerated,
                "Bare OnAdd without a store default is client-side, not store-generated.");
        }
    }
}
