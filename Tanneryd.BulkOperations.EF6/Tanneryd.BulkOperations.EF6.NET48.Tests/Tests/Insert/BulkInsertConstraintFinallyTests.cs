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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.EF6.Model;
using Tanneryd.BulkOperations.EF6.NET48.Tests.Models.DM.Companies;
using Tanneryd.BulkOperations.EF6.NET48.Tests.Models.EF;

namespace Tanneryd.BulkOperations.EF6.NET48.Tests.Tests.Insert
{
    /// <summary>
    /// Regression: BulkInsertAllAsync finally re-enables CHECK constraints with the
    /// caller's CancellationToken, so a cancel after NOCHECK can leave FKs disabled.
    /// </summary>
    [TestClass]
    public class BulkInsertConstraintFinallyTests : BulkOperationTestBase
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
        public async Task BulkInsertAsync_ShouldReenableConstraints_WhenTokenCancelledAfterNocheck()
        {
            using var db = new UnitTestContext();
            using var cts = new CancellationTokenSource();

            // Enough rows to keep the MERGE running after NOCHECK so we can cancel.
            var companies = Enumerable.Range(0, 2500).Select(i =>
            {
                var company = new Company { Name = $"SelfRef-Company-{i}" };
                company.ParentCompany = company;
                return company;
            }).ToList();

            var request = new BulkInsertRequest<Company>
            {
                Entities = companies,
                EnableRecursiveInsert = EnableRecursiveInsert.Yes,
                AllowNotNullSelfReferences = AllowNotNullSelfReferences.Yes,
            };

            var insertTask = db.BulkInsertAllAsync(request, cts.Token);

            var sawDisabled = await WaitForCompanyParentFkDisabledAsync(TimeSpan.FromSeconds(15))
                .ConfigureAwait(false);
            Assert.IsTrue(sawDisabled,
                "Expected to observe NOCHECK on the Company self-FK before cancelling.");

            cts.Cancel();

            try
            {
                await insertTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation wins the race with MERGE/finally.
            }
            catch (SqlException)
            {
                // Ignore; we only care that constraints are restored.
            }

            Assert.IsFalse(
                await IsCompanyParentFkDisabledAsync().ConfigureAwait(false),
                "Expected CHECK CONSTRAINT re-enable in finally even when the caller token is cancelled.");
        }

        private static async Task<bool> WaitForCompanyParentFkDisabledAsync(TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (await IsCompanyParentFkDisabledAsync().ConfigureAwait(false))
                    return true;

                await Task.Delay(20).ConfigureAwait(false);
            }

            return false;
        }

        private static async Task<bool> IsCompanyParentFkDisabledAsync()
        {
            using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync().ConfigureAwait(false);
            using var cmd = connection.CreateCommand();
            cmd.CommandText =
                $"SELECT CAST(is_disabled AS int) FROM sys.foreign_keys WHERE name = N'{CompanyParentFkName}'";
            var result = await cmd.ExecuteScalarAsync().ConfigureAwait(false);
            return result != null && result != DBNull.Value && Convert.ToInt32(result) == 1;
        }

        private static void EnsureCompanyParentFkEnabled(UnitTestContext db)
        {
            db.Database.ExecuteSqlCommand(
                $"ALTER TABLE [dbo].[Company] WITH NOCHECK CHECK CONSTRAINT [{CompanyParentFkName}]");
        }
    }
}
