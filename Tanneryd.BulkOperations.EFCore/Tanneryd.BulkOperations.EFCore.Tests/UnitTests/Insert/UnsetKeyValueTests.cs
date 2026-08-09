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
using System.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tanneryd.BulkOperations.EFCore.Tests.UnitTests.Insert
{
    /// <summary>
    /// Regression: SelectNewEntities / recursive-insert existence checks must
    /// treat null, empty Guid/string, default DateTime, and numeric zero as
    /// unset — without using dynamic <c>value == 0</c> comparisons that throw
    /// for Guid/string. Used by issue #47 parent-reuse and store-generated
    /// string PK insert paths.
    /// </summary>
    [TestClass]
    public class UnsetKeyValueTests
    {
        [TestMethod]
        public void IsUnsetKeyValue_ShouldBeTrue_ForNullAndDbNull()
        {
            Assert.IsTrue(DbContextExtensions.IsUnsetKeyValue(null, typeof(string)));
            Assert.IsTrue(DbContextExtensions.IsUnsetKeyValue(DBNull.Value, typeof(int)));
            Assert.IsTrue(DbContextExtensions.IsUnsetKeyValue(null, typeof(int?)));
        }

        [TestMethod]
        public void IsUnsetKeyValue_ShouldTreatEmptyGuidAndDefaultDateTimeAsUnset()
        {
            Assert.IsTrue(DbContextExtensions.IsUnsetKeyValue(Guid.Empty, typeof(Guid)));
            Assert.IsFalse(DbContextExtensions.IsUnsetKeyValue(Guid.NewGuid(), typeof(Guid)));

            Assert.IsTrue(DbContextExtensions.IsUnsetKeyValue(default(DateTime), typeof(DateTime)));
            Assert.IsFalse(DbContextExtensions.IsUnsetKeyValue(new DateTime(2020, 1, 2), typeof(DateTime)));
        }

        [TestMethod]
        public void IsUnsetKeyValue_ShouldTreatNullOrEmptyStringAsUnset()
        {
            Assert.IsTrue(DbContextExtensions.IsUnsetKeyValue(null, typeof(string)));
            Assert.IsTrue(DbContextExtensions.IsUnsetKeyValue(string.Empty, typeof(string)));
            Assert.IsFalse(DbContextExtensions.IsUnsetKeyValue("code", typeof(string)));
        }

        [TestMethod]
        public void IsUnsetKeyValue_ShouldTreatNumericZeroAsUnset()
        {
            Assert.IsTrue(DbContextExtensions.IsUnsetKeyValue(0, typeof(int)));
            Assert.IsTrue(DbContextExtensions.IsUnsetKeyValue((long)0, typeof(long)));
            Assert.IsTrue(DbContextExtensions.IsUnsetKeyValue((short)0, typeof(short)));
            Assert.IsTrue(DbContextExtensions.IsUnsetKeyValue(0m, typeof(decimal)));
            Assert.IsFalse(DbContextExtensions.IsUnsetKeyValue(1, typeof(int)));
            Assert.IsFalse(DbContextExtensions.IsUnsetKeyValue(1L, typeof(long)));
        }

        [TestMethod]
        public void IsUnsetKeyValue_ShouldNeverTreatBoolFalseAsUnset()
        {
            // bool has no "unset" sentinel; false is a real key value.
            Assert.IsFalse(DbContextExtensions.IsUnsetKeyValue(false, typeof(bool)));
            Assert.IsFalse(DbContextExtensions.IsUnsetKeyValue(true, typeof(bool)));
        }

        [TestMethod]
        public void IsUnsetKeyValue_ShouldUnwrapNullableClrType()
        {
            Assert.IsTrue(DbContextExtensions.IsUnsetKeyValue(0, typeof(int?)));
            Assert.IsFalse(DbContextExtensions.IsUnsetKeyValue(5, typeof(int?)));
            Assert.IsTrue(DbContextExtensions.IsUnsetKeyValue(Guid.Empty, typeof(Guid?)));
        }

        [TestMethod]
        public void IsKeyValueSet_ShouldBeInverseOfIsUnsetKeyValue()
        {
            Assert.IsFalse(DbContextExtensions.IsKeyValueSet(0, typeof(int)));
            Assert.IsTrue(DbContextExtensions.IsKeyValueSet(42, typeof(int)));
            Assert.IsFalse(DbContextExtensions.IsKeyValueSet(Guid.Empty, typeof(Guid)));
            Assert.IsTrue(DbContextExtensions.IsKeyValueSet(Guid.NewGuid(), typeof(Guid)));
        }

        [TestMethod]
        public void CreateUnsetKeyValue_ShouldReturnTypeDefault()
        {
            Assert.IsNull(DbContextExtensions.CreateUnsetKeyValue(typeof(string)));
            Assert.AreEqual(0, DbContextExtensions.CreateUnsetKeyValue(typeof(int)));
            Assert.AreEqual(Guid.Empty, DbContextExtensions.CreateUnsetKeyValue(typeof(Guid)));
            Assert.AreEqual(0, DbContextExtensions.CreateUnsetKeyValue(typeof(int?)));
        }
    }
}
