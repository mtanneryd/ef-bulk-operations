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
using Tanneryd.BulkOperations.EF6.Model;
using Tanneryd.BulkOperations.EF6.NET48.Tests.Models.EF.ComplexAssignedPk;

namespace Tanneryd.BulkOperations.EF6.NET48.Tests.Tests.Insert
{
    /// <summary>
    /// Complex properties are flattened to ExpandoObject before insert path
    /// selection. Client-assigned PKs must still run select-not-existing on the
    /// original CLR instances (not Cast&lt;T&gt; on Expando rows).
    /// </summary>
    [TestClass]
    public class BulkInsertComplexAssignedPkTests
    {
        [TestInitialize]
        public void Initialize()
        {
            using (var ctx = new ComplexAssignedPkContext())
            {
                if (ctx.Database.Exists())
                    ctx.Database.Delete();
                ctx.Database.Create();
            }
        }

        [TestCleanup]
        public void Cleanup()
        {
            using (var ctx = new ComplexAssignedPkContext())
            {
                if (ctx.Database.Exists())
                    ctx.Database.Delete();
            }
        }

        [TestMethod]
        public void BulkInsert_ComplexType_WithClientAssignedGuidPk_ShouldPersist()
        {
            var id = Guid.NewGuid();

            using (var db = new ComplexAssignedPkContext())
            {
                db.BulkInsertAll(new BulkInsertRequest<ComplexAssignedPkProbe>
                {
                    Entities = new[]
                    {
                        new ComplexAssignedPkProbe
                        {
                            Id = id,
                            Name = "probe",
                            Location = new AssignedPkLocation { City = "Stockholm" },
                        },
                    },
                });
            }

            using (var verify = new ComplexAssignedPkContext())
            {
                var row = verify.Probes.Single();
                Assert.AreEqual(id, row.Id);
                Assert.AreEqual("probe", row.Name);
                Assert.AreEqual("Stockholm", row.Location.City);
            }
        }

        [TestMethod]
        public void BulkInsert_ComplexType_WithClientAssignedGuidPk_ShouldSkipExisting()
        {
            var existingId = Guid.NewGuid();
            var newId = Guid.NewGuid();

            using (var db = new ComplexAssignedPkContext())
            {
                db.Probes.Add(new ComplexAssignedPkProbe
                {
                    Id = existingId,
                    Name = "existing",
                    Location = new AssignedPkLocation { City = "Uppsala" },
                });
                db.SaveChanges();

                db.BulkInsertAll(new BulkInsertRequest<ComplexAssignedPkProbe>
                {
                    Entities = new[]
                    {
                        new ComplexAssignedPkProbe
                        {
                            Id = existingId,
                            Name = "should-not-overwrite",
                            Location = new AssignedPkLocation { City = "Gothenburg" },
                        },
                        new ComplexAssignedPkProbe
                        {
                            Id = newId,
                            Name = "brand-new",
                            Location = new AssignedPkLocation { City = "Malmo" },
                        },
                    },
                });
            }

            using (var verify = new ComplexAssignedPkContext())
            {
                Assert.AreEqual(2, verify.Probes.Count());
                var existing = verify.Probes.Single(p => p.Id == existingId);
                Assert.AreEqual("existing", existing.Name);
                Assert.AreEqual("Uppsala", existing.Location.City);
                Assert.AreEqual("Malmo", verify.Probes.Single(p => p.Id == newId).Location.City);
            }
        }
    }
}
