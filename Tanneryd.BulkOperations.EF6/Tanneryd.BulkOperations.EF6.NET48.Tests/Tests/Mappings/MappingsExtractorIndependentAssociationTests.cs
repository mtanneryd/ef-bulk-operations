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
using System.Data.Entity;
using System.Data.Entity.Core.Mapping;
using System.Data.Entity.Core.Metadata.Edm;
using System.Data.Entity.Infrastructure;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.EF6;
using Tanneryd.BulkOperations.EF6.NET48.Tests.Models.EF.IndependentAssociation;

namespace Tanneryd.BulkOperations.EF6.NET48.Tests.Tests.Mappings
{
    /// <summary>
    /// MapKey independent associations have AssociationType.Constraint == null.
    /// GetMappings must not NRE, and must not treat them as many-to-many
    /// (no CLR FK property for recursive insert to populate).
    /// </summary>
    [TestClass]
    public class MappingsExtractorIndependentAssociationTests
    {
        [TestInitialize]
        public void Initialize()
        {
            using (var ctx = new IndependentAssociationContext())
            {
                if (ctx.Database.Exists())
                    ctx.Database.Delete();
                ctx.Database.Create();
            }
        }

        [TestCleanup]
        public void Cleanup()
        {
            using (var ctx = new IndependentAssociationContext())
            {
                if (ctx.Database.Exists())
                    ctx.Database.Delete();
            }
        }

        [TestMethod]
        public void MapKey_IndependentAssociation_ShouldHaveNullConstraint()
        {
            using (var db = new IndependentAssociationContext())
            {
                var objectContext = ((IObjectContextAdapter)db).ObjectContext;
                var workspace = objectContext.MetadataWorkspace;
                var storageMapping =
                    (EntityContainerMapping)workspace.GetItem<GlobalItem>(
                        objectContext.DefaultContainerName, DataSpace.CSSpace);
                var entitySetMap = storageMapping.EntitySetMappings.Single(m =>
                    m.EntitySet.ElementType.Name == nameof(IaCourse));
                var nav = entitySetMap.EntityTypeMappings.Single().EntityType.DeclaredMembers
                    .OfType<NavigationProperty>()
                    .Single(p => p.Name == nameof(IaCourse.Department));
                var relType = (AssociationType)nav.RelationshipType;

                Assert.IsFalse(relType.IsForeignKey);
                Assert.IsNull(
                    relType.Constraint,
                    "MapKey independent associations have no conceptual ReferentialConstraint.");
                Assert.IsTrue(
                    storageMapping.AssociationSetMappings.Any(m =>
                        m.AssociationSet.ElementType.Name == relType.Name),
                    "Independent associations still appear in AssociationSetMappings (entity table, not a join).");
            }
        }

        [TestMethod]
        public void GetMappings_MapKeyIndependentAssociation_ShouldNotThrow_AndShouldSkipNav()
        {
            using (var db = new IndependentAssociationContext())
            {
                Model.Mappings courseMappings;
                Model.Mappings deptMappings;
                try
                {
                    courseMappings = new MappingsExtractor().GetMappings(db, typeof(IaCourse));
                    deptMappings = new MappingsExtractor().GetMappings(db, typeof(IaDepartment));
                }
                catch (NullReferenceException ex)
                {
                    Assert.Fail("GetMappings must not NRE on MapKey independent associations: " + ex);
                    return;
                }

                var courseDept = courseMappings.FromForeignKeyMappings
                    .Concat(courseMappings.ToForeignKeyMappings)
                    .SingleOrDefault(m => m.NavigationPropertyName == nameof(IaCourse.Department));
                var deptCourses = deptMappings.FromForeignKeyMappings
                    .Concat(deptMappings.ToForeignKeyMappings)
                    .SingleOrDefault(m => m.NavigationPropertyName == nameof(IaDepartment.Courses));

                Assert.IsNull(
                    courseDept,
                    "MapKey independent associations have no CLR FK; skip rather than misclassify as M2M.");
                Assert.IsNull(
                    deptCourses,
                    "Collection end of the same MapKey association must also be skipped.");
            }
        }
    }
}
