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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.EF6.Model;
using Tanneryd.BulkOperations.EF6.NET48.Tests.Models.EF.CompositeNavDot;

namespace Tanneryd.BulkOperations.EF6.NET48.Tests.Tests.Select
{
    /// <summary>
    /// Nav-dot SelectExisting must join on every composite FK pair. Joining on
    /// only the first pair matches the wrong child when parents share that column.
    /// </summary>
    [TestClass]
    public class BulkSelectExistingCompositeNavDotTests
    {
        [TestInitialize]
        public void Initialize()
        {
            using (var ctx = new CompositeNavDotContext())
            {
                if (ctx.Database.Exists())
                    ctx.Database.Delete();
                ctx.Database.Create();
            }
        }

        [TestCleanup]
        public void Cleanup()
        {
            using (var ctx = new CompositeNavDotContext())
            {
                if (ctx.Database.Exists())
                    ctx.Database.Delete();
            }
        }

        [TestMethod]
        public void BulkSelectExisting_NavDot_ShouldRequireAllCompositeFkPairs()
        {
            using (var db = new CompositeNavDotContext())
            {
                // Parents share TenantId (first FK column). A join on only that
                // pair would match both children when filtering Parent.Code.
                db.Parents.Add(new CompositeNavParent { TenantId = 1, LocalId = 1, Code = "Alpha" });
                db.Parents.Add(new CompositeNavParent { TenantId = 1, LocalId = 2, Code = "Beta" });
                db.SaveChanges();

                db.Children.Add(new CompositeNavChild
                {
                    Label = "child-alpha",
                    ParentTenantId = 1,
                    ParentLocalId = 1,
                });
                db.Children.Add(new CompositeNavChild
                {
                    Label = "child-beta",
                    ParentTenantId = 1,
                    ParentLocalId = 2,
                });
                db.SaveChanges();
            }

            using (var db = new CompositeNavDotContext())
            {
                var probe = new CompositeNavChild { Label = "Alpha" };

                var existing = db.BulkSelectExisting<CompositeNavChild, CompositeNavChild>(
                    new BulkSelectRequest<CompositeNavChild>
                    {
                        Items = new[] { probe },
                        KeyPropertyMappings = new[]
                        {
                            new KeyPropertyMapping
                            {
                                EntityPropertyName = "Parent.Code",
                                ItemPropertyName = nameof(CompositeNavChild.Label),
                            },
                        },
                        ColumnPropertyMappings = new[]
                        {
                            new KeyPropertyMapping
                            {
                                EntityPropertyName = nameof(CompositeNavChild.Label),
                                ItemPropertyName = nameof(CompositeNavChild.Label),
                            },
                        },
                    });

                Assert.AreEqual(1, existing.Count);
                Assert.AreEqual("child-alpha", existing[0].Label);
            }
        }
    }
}
