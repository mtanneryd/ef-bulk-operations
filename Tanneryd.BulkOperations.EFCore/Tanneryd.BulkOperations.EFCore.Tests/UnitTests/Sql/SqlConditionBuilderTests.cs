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
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.Common.Sql;

namespace Tanneryd.BulkOperations.EFCore.Tests.UnitTests.Sql
{
    /// <summary>
    /// SqlConditionBuilder drives BulkDeleteNotExisting window predicates.
    /// Null values must become IS NULL (never "= @p"), and column names must
    /// come from the resolve callback — never raw caller fragments.
    /// </summary>
    [TestClass]
    public class SqlConditionBuilderTests
    {
        [TestMethod]
        public void Build_ShouldEmitIsNull_ForNullColumnValue()
        {
            var parameters = new List<SqlParameter>();
            var sql = SqlConditionBuilder.Build(
                new[] { new SqlConditionValue("MotherId", null) },
                "t0",
                name => name,
                parameters,
                "c");

            Assert.AreEqual("[t0].[MotherId] IS NULL", sql);
            Assert.AreEqual(0, parameters.Count);
        }

        [TestMethod]
        public void Build_ShouldEmitIsNull_ForDbNullColumnValue()
        {
            var parameters = new List<SqlParameter>();
            var sql = SqlConditionBuilder.Build(
                new[] { new SqlConditionValue("MotherId", DBNull.Value) },
                "t0",
                name => name,
                parameters,
                "c");

            Assert.AreEqual("[t0].[MotherId] IS NULL", sql);
            Assert.AreEqual(0, parameters.Count);
        }

        [TestMethod]
        public void Build_ShouldParameterize_NonNullColumnValue()
        {
            var parameters = new List<SqlParameter>();
            var sql = SqlConditionBuilder.Build(
                new[] { new SqlConditionValue("MotherId", 42L) },
                "t0",
                name => name,
                parameters,
                "c");

            Assert.AreEqual("[t0].[MotherId] = @c0", sql);
            Assert.AreEqual(1, parameters.Count);
            Assert.AreEqual("@c0", parameters[0].ParameterName);
            Assert.AreEqual(42L, parameters[0].Value);
        }

        [TestMethod]
        public void Build_ShouldUseResolvedColumnNames_AndJoinWithAnd()
        {
            var parameters = new List<SqlParameter>();
            var sql = SqlConditionBuilder.Build(
                new[]
                {
                    new SqlConditionValue("MotherId", 1L),
                    new SqlConditionValue("Status", null),
                },
                "src",
                name => name == "MotherId" ? "mother_id" : "status_code",
                parameters,
                "p");

            Assert.AreEqual("[src].[mother_id] = @p0 AND [src].[status_code] IS NULL", sql);
            Assert.AreEqual(1, parameters.Count);
            Assert.AreEqual("@p0", parameters[0].ParameterName);
        }

        [TestMethod]
        public void Build_ShouldThrow_WhenColumnNameIsMissing()
        {
            var parameters = new List<SqlParameter>();

            Assert.ThrowsExactly<ArgumentException>(() =>
                SqlConditionBuilder.Build(
                    new[] { new SqlConditionValue("  ", 1) },
                    "t0",
                    name => name,
                    parameters,
                    "c"));
        }
    }
}
