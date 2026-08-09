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
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.EFCore.Model;
using Tanneryd.BulkOperations.EFCore.Tests.Models.EF.ComplexCollision;

namespace Tanneryd.BulkOperations.EFCore.Tests.UnitTests.Insert
{
    /// <summary>
    /// Regression: expando bulk-copy property discovery must union keys across
    /// the batch and take CLR types from the first non-null value so a null on
    /// entities[0] does not drop the column. All-null keys fall back to
    /// mapping-declared CLR types (complex-type flatten / M2M join).
    /// </summary>
    [TestClass]
    public class GetPropertiesExpandoTests
    {
        private const string DummyConnectionString =
            @"data source=(localdb)\MSSQLLocalDB;initial catalog=GetPropertiesExpandoTests;Integrated Security=SSPI;TrustServerCertificate=true";

        private static readonly DbContextOptions<ComplexCollisionContext> Options =
            new DbContextOptionsBuilder<ComplexCollisionContext>()
                .UseSqlServer(DummyConnectionString)
                .Options;

        [TestMethod]
        public void GetProperties_ShouldReturnEmpty_ForEmptyBatch()
        {
            var props = DbContextExtensions.GetProperties(new ArrayList());
            Assert.AreEqual(0, props.Length);
        }

        [TestMethod]
        public void GetProperties_ShouldInferTypeFromLaterNonNull_WhenFirstRowIsNull()
        {
            dynamic first = new ExpandoObject();
            first.Name = null;
            first.Age = null;
            dynamic second = new ExpandoObject();
            second.Name = "Ada";
            second.Age = 36;

            var props = DbContextExtensions.GetProperties(new ArrayList { (object)first, (object)second });
            var byName = props.ToDictionary(p => p.Name, p => p.Type);

            Assert.AreEqual(typeof(string), byName["Name"]);
            Assert.AreEqual(typeof(int), byName["Age"]);
        }

        [TestMethod]
        public void GetProperties_ShouldOmitAllNullKey_WithoutColumnMappings()
        {
            dynamic row = new ExpandoObject();
            row.Name = "Ada";
            row.OptionalNote = null;

            var props = DbContextExtensions.GetProperties(new ArrayList { (object)row });

            CollectionAssert.AreEqual(new[] { "Name" }, props.Select(p => p.Name).ToArray());
        }

        [TestMethod]
        public void GetProperties_ShouldKeepAllNullKey_WhenMappingsSupplyClrType()
        {
            using var ctx = new ComplexCollisionContext(Options);
            var mappings = new MappingsExtractor(ctx)
                .GetMappings(typeof(ComplexCollisionContact))
                .ColumnMappingByPropertyName;

            dynamic row = new ExpandoObject();
            row.Name = "Ada";
            row.Home_Street = null;
            row.Work_Street = null;

            var props = DbContextExtensions.GetProperties(new ArrayList { (object)row }, mappings);
            var byName = props.ToDictionary(p => p.Name, p => p.Type);

            Assert.AreEqual(typeof(string), byName["Name"]);
            Assert.AreEqual(typeof(string), byName["Home_Street"],
                "All-null complex flatten columns must keep mapping CLR types.");
            Assert.AreEqual(typeof(string), byName["Work_Street"]);
        }

        [TestMethod]
        public void GetProperties_ShouldUseRegularReflection_ForNonExpandoBatch()
        {
            var props = DbContextExtensions.GetProperties(
                new ArrayList { new ComplexCollisionContact { Name = "Ada" } });

            Assert.IsTrue(props.Any(p => p.Name == nameof(ComplexCollisionContact.Name)));
            Assert.IsTrue(props.Any(p => p.Name == nameof(ComplexCollisionContact.Home)));
        }
    }
}
