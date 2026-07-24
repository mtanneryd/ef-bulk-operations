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

using System.Data.Entity;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.EF6;
using Tanneryd.BulkOperations.EF6.Model;
using Tanneryd.BulkOperations.EF6.NET48.Tests.Models.EF.CompositeManyToMany;

namespace Tanneryd.BulkOperations.EF6.NET48.Tests.Tests.Mappings
{
    /// <summary>
    /// Many-to-many association ends with composite principal keys must map
    /// every PropertyMapping (not only [0]) so recursive insert can write a
    /// complete join row.
    /// </summary>
    [TestClass]
    public class MappingsExtractorCompositeManyToManyTests
    {
        [TestInitialize]
        public void Initialize()
        {
            using (var ctx = new CompositeManyToManyContext())
            {
                if (ctx.Database.Exists())
                    ctx.Database.Delete();
                ctx.Database.Create();
            }
        }

        [TestCleanup]
        public void Cleanup()
        {
            using (var ctx = new CompositeManyToManyContext())
            {
                if (ctx.Database.Exists())
                    ctx.Database.Delete();
            }
        }

        [TestMethod]
        public void ManyToMany_CompositePrincipalKey_ShouldMapAllJoinColumnsOnEachEnd()
        {
            using (var db = new CompositeManyToManyContext())
            {
                var mappings = new MappingsExtractor().GetMappings(db, typeof(TenantItem));
                var labels = mappings.FromForeignKeyMappings
                    .Concat(mappings.ToForeignKeyMappings)
                    .SingleOrDefault(m => m.NavigationPropertyName == nameof(TenantItem.Labels));

                Assert.IsNotNull(labels, "TenantItem.Labels must appear in foreign-key mappings.");
                Assert.IsNotNull(
                    labels.AssociationMapping,
                    "TenantItem.Labels must have AssociationMapping so join rows can be bulk-copied.");
                Assert.AreEqual("TenantItemLabels", labels.AssociationMapping.TableName.Name);

                var sourceNames = labels.AssociationMapping.Sources
                    .Select(s => s.TableColumn.Name)
                    .OrderBy(n => n)
                    .ToArray();
                CollectionAssert.AreEqual(
                    new[] { "ItemId", "TenantId" },
                    sourceNames,
                    "Composite left-end join keys must include every PropertyMapping, not only the first.");

                Assert.AreEqual(1, labels.AssociationMapping.Targets.Length);
                Assert.AreEqual("LabelId", labels.AssociationMapping.Targets[0].TableColumn.Name);
            }
        }
    }
}
