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
using Microsoft.Data.SqlClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.EF6.Model;
using Tanneryd.BulkOperations.EF6.NET48.Tests.Models.DM.Companies;
using Tanneryd.BulkOperations.EF6.NET48.Tests.Models.EF;

namespace Tanneryd.BulkOperations.EF6.NET48.Tests.Tests.Insert
{
    /// <summary>
    /// After AllowNotNullSelfReferences NOCHECK, a failing WITH CHECK must still
    /// fall back to WITH NOCHECK so FKs are re-enabled. On System.Data.SqlClient
    /// the failure is a legacy SqlException; catching only Microsoft.Data.SqlClient
    /// skips that fallback and leaves constraints disabled.
    /// </summary>
    [TestClass]
    public class BulkInsertLegacyConstraintReenableTests : BulkOperationTestBase
    {
        private const string CompanyParentFkName = "FK_dbo.Company_dbo.Company_ParentCompanyId";
        private const string ConnectionString =
            @"data source=(localdb)\MSSQLLocalDB;initial catalog=Tanneryd.BulkOperations.EF6.NET48.Tests.Models.EF.UnitTestContext;persist security info=True;Integrated Security=SSPI;MultipleActiveResultSets=True;TrustServerCertificate=true";

        [TestInitialize]
        public void Initialize()
        {
            InitializeUnitTestContext();
            CleanUp();
        }

        [TestCleanup]
        public void CleanUp()
        {
            using (var db = new UnitTestContext())
            {
                EnsureCompanyParentFkEnabled(db);
            }

            CleanupUnitTestContext();
        }

        [TestMethod]
        public void BulkInsert_OnLegacySqlClient_ShouldReenableConstraints_WhenWithCheckFails()
        {
            using (var probe = new LegacyUnitTestContext())
            {
                Assert.IsInstanceOfType(
                    probe.Database.Connection,
                    typeof(System.Data.SqlClient.SqlConnection),
                    "LegacyUnitTestContext must use System.Data.SqlClient for this regression.");
            }

            Exception caught = null;
            try
            {
                var company = new Company
                {
                    Name = "Legacy-World Inc",
                };
                var john = new Employee
                {
                    Name = "Legacy-John",
                    Company = company,
                };

                using var db = new LegacyUnitTestContext();
                db.BulkInsertAll(new BulkInsertRequest<Employee>
                {
                    Entities = new[] { john },
                    EnableRecursiveInsert = EnableRecursiveInsert.Yes,
                    AllowNotNullSelfReferences = AllowNotNullSelfReferences.Yes,
                });
                Assert.Fail("Expected SqlException from WITH CHECK validation was not thrown.");
            }
            catch (Microsoft.Data.SqlClient.SqlException microsoftSqlException)
            {
                caught = microsoftSqlException;
            }
            catch (System.Data.SqlClient.SqlException legacySqlException)
            {
                caught = legacySqlException;
            }

            Assert.IsNotNull(caught, "Expected SqlException from WITH CHECK validation.");
            Assert.IsInstanceOfType(
                caught,
                typeof(System.Data.SqlClient.SqlException),
                "Legacy path should surface System.Data.SqlClient.SqlException.");

            Assert.IsFalse(
                IsCompanyParentFkDisabled(),
                "Expected WITH NOCHECK fallback to re-enable the Company self-FK even when WITH CHECK fails under System.Data.SqlClient.");
        }

        private static bool IsCompanyParentFkDisabled()
        {
            using var connection = new SqlConnection(ConnectionString);
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText =
                $"SELECT CAST(is_disabled AS int) FROM sys.foreign_keys WHERE name = N'{CompanyParentFkName}'";
            var result = cmd.ExecuteScalar();
            return result != null && result != DBNull.Value && Convert.ToInt32(result) == 1;
        }

        private static void EnsureCompanyParentFkEnabled(UnitTestContext db)
        {
            db.Database.ExecuteSqlCommand(
                $"ALTER TABLE [dbo].[Company] WITH NOCHECK CHECK CONSTRAINT [{CompanyParentFkName}]");
        }
    }
}
