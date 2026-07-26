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

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.EF6.Model;
using Tanneryd.BulkOperations.EF6.NET48.Tests.Models.EF.ComplexTph;

namespace Tanneryd.BulkOperations.EF6.NET48.Tests.Tests.Insert
{
    /// <summary>
    /// Complex flatten produces ExpandoObject rows; CreateEntitiesDataReader must
    /// still append the TPH discriminator so the reader matches the bulk-copy schema.
    /// </summary>
    [TestClass]
    public class BulkInsertComplexTphTests
    {
        [TestInitialize]
        public void Initialize()
        {
            using (var ctx = new ComplexTphContext())
            {
                if (ctx.Database.Exists())
                    ctx.Database.Delete();
                ctx.Database.Create();
            }
        }

        [TestCleanup]
        public void Cleanup()
        {
            using (var ctx = new ComplexTphContext())
            {
                if (ctx.Database.Exists())
                    ctx.Database.Delete();
            }
        }

        [TestMethod]
        public void BulkInsert_TphWithComplexProperty_ShouldStampDiscriminator()
        {
            using (var db = new ComplexTphContext())
            {
                db.BulkInsertAll(new BulkInsertRequest<ComplexTphRed>
                {
                    Entities = new[]
                    {
                        new ComplexTphRed
                        {
                            Name = "red-1",
                            Location = new ComplexTphLocation { City = "Stockholm" },
                        },
                    },
                });
            }

            using (var verify = new ComplexTphContext())
            {
                Assert.AreEqual(1, verify.Reds.Count());
                Assert.AreEqual(0, verify.Blues.Count());
                var row = verify.Reds.Single();
                Assert.AreEqual("red-1", row.Name);
                Assert.AreEqual("Stockholm", row.Location.City);
            }
        }

        [TestMethod]
        public void BulkInsert_TphWithComplexProperty_ShouldStampDiscriminator_PerConcreteType()
        {
            using (var db = new ComplexTphContext())
            {
                db.BulkInsertAll(new BulkInsertRequest<ComplexTphBlue>
                {
                    Entities = new[]
                    {
                        new ComplexTphBlue
                        {
                            Name = "blue-1",
                            Location = new ComplexTphLocation { City = "Malmo" },
                        },
                    },
                });
            }

            using (var verify = new ComplexTphContext())
            {
                Assert.AreEqual(0, verify.Reds.Count());
                Assert.AreEqual(1, verify.Blues.Count());
                Assert.AreEqual("Malmo", verify.Blues.Single().Location.City);
            }
        }
    }
}
