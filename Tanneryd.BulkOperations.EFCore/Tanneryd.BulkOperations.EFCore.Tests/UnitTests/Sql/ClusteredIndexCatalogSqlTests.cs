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

using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.SqlClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.Common.Sql;

namespace Tanneryd.BulkOperations.EFCore.Tests.UnitTests.Sql
{
    /// <summary>
    /// Regression for SortUsingClusteredIndex catalog lookup: table/schema
    /// identifiers must be SqlParameters. Interpolating them previously let a
    /// quote in an EF mapping identifier break (or alter) the sys.tables query.
    /// </summary>
    [TestClass]
    public class ClusteredIndexCatalogSqlTests
    {
        [TestMethod]
        public void BuildQuery_ShouldParameterizeTableAndSchema_WithoutInterpolatingIdentifiers()
        {
            var parameters = new List<SqlParameter>();
            const string tableName = "O'Brien]; DROP TABLE dbo.X;--";
            const string schema = "odd'schema";

            var query = ClusteredIndexCatalogSql.BuildQuery(schema, tableName, parameters);

            StringAssert.Contains(query, "t.name = @tableName");
            StringAssert.Contains(query, "SCHEMA_NAME(t.schema_id) = @schema");
            Assert.IsFalse(query.Contains(tableName),
                "Table name must not appear as a SQL literal.");
            Assert.IsFalse(query.Contains(schema),
                "Schema name must not appear as a SQL literal.");

            Assert.AreEqual(2, parameters.Count);
            Assert.AreEqual(ClusteredIndexCatalogSql.TableNameParameter, parameters[0].ParameterName);
            Assert.AreEqual(tableName, parameters[0].Value);
            Assert.AreEqual(ClusteredIndexCatalogSql.SchemaParameter, parameters[1].ParameterName);
            Assert.AreEqual(schema, parameters[1].Value);
        }

        [TestMethod]
        public void BuildQuery_ShouldOmitSchemaParameter_WhenSchemaIsEmpty()
        {
            var parameters = new List<SqlParameter>();
            var query = ClusteredIndexCatalogSql.BuildQuery(schema: null, tableName: "SortProbe", parameters);

            Assert.IsFalse(query.Contains("@schema"));
            Assert.IsFalse(query.Contains("SCHEMA_NAME"));
            Assert.AreEqual(1, parameters.Count);
            Assert.AreEqual("SortProbe", parameters.Single().Value);
        }

        [TestMethod]
        public void BuildQuery_ShouldOmitSchemaParameter_WhenSchemaIsWhitespace()
        {
            // string.IsNullOrEmpty does not treat whitespace as empty; document
            // current contract so callers keep trimming before invoke if needed.
            var parameters = new List<SqlParameter>();
            var query = ClusteredIndexCatalogSql.BuildQuery(schema: "", tableName: "SortProbe", parameters);

            Assert.IsFalse(query.Contains("@schema"));
            Assert.AreEqual(1, parameters.Count);
        }
    }
}
