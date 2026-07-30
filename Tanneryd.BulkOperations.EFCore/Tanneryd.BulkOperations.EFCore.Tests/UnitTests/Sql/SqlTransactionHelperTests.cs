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

using Microsoft.Data.SqlClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.Common.Sql;

namespace Tanneryd.BulkOperations.EFCore.Tests.UnitTests.Sql
{
    /// <summary>
    /// Regression: the constraint re-enable in the bulk insert finally block must
    /// not attempt to execute commands on a dead ("zombie") caller transaction —
    /// a rolled-back or server-aborted transaction has already undone the NOCHECK
    /// ALTER TABLE, and using it throws InvalidOperationException, masking the
    /// original error.
    /// </summary>
    [TestClass]
    public class SqlTransactionHelperTests
    {
        private const string ConnectionString =
            @"data source=(localdb)\MSSQLLocalDB;initial catalog=master;Integrated Security=SSPI;TrustServerCertificate=true";

        [TestMethod]
        public void IsZombied_ShouldBeFalse_ForNullTransaction()
        {
            Assert.IsFalse(SqlTransactionHelper.IsZombied(null),
                "No transaction means the operation runs auto-committed; nothing to skip.");
        }

        [TestMethod]
        public void IsZombied_ShouldBeFalse_ForActiveTransaction()
        {
            using var connection = new SqlConnection(ConnectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            Assert.IsFalse(SqlTransactionHelper.IsZombied(transaction));

            transaction.Rollback();
        }

        [TestMethod]
        public void IsZombied_ShouldBeTrue_ForRolledBackTransaction()
        {
            using var connection = new SqlConnection(ConnectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();
            transaction.Rollback();

            Assert.IsTrue(SqlTransactionHelper.IsZombied(transaction),
                "A rolled-back transaction must be detected so re-enable is skipped.");
        }

        [TestMethod]
        public void IsZombied_ShouldBeTrue_ForCommittedTransaction()
        {
            using var connection = new SqlConnection(ConnectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();
            transaction.Commit();

            Assert.IsTrue(SqlTransactionHelper.IsZombied(transaction),
                "A committed transaction can no longer carry commands; re-enable must be skipped.");
        }
    }
}
