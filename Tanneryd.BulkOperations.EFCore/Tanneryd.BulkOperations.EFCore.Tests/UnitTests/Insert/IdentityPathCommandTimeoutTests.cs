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
using Microsoft.Data.SqlClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tanneryd.BulkOperations.EFCore.Tests.UnitTests.Insert
{
    /// <summary>
    /// Regression: the MERGE … OUTPUT identity path historically hard-coded a
    /// 30-minute command timeout. It must use BulkInsertRequest.CommandTimeout
    /// so long-running or short-timeout callers are honored. No SQL Server required.
    /// </summary>
    [TestClass]
    public class IdentityPathCommandTimeoutTests
    {
        [TestMethod]
        public void CreateIdentityPathCommand_ShouldApplyRequestTimeoutSeconds()
        {
            using var connection = new SqlConnection(
                "Server=(local);Database=unused;Trusted_Connection=True;TrustServerCertificate=True");
            var timeout = TimeSpan.FromMinutes(12);

            using var command = DbContextExtensions.CreateIdentityPathCommand(
                connection,
                transaction: null,
                commandTimeout: timeout);

            Assert.AreEqual((int)timeout.TotalSeconds, command.CommandTimeout,
                "MERGE identity commands must use the insert request timeout, not a hard-coded value.");
            Assert.AreSame(connection, command.Connection);
        }

        [TestMethod]
        public void CreateIdentityPathCommand_ShouldPreserveZeroAsNoTimeout()
        {
            using var connection = new SqlConnection(
                "Server=(local);Database=unused;Trusted_Connection=True;TrustServerCertificate=True");

            using var command = DbContextExtensions.CreateIdentityPathCommand(
                connection,
                transaction: null,
                commandTimeout: TimeSpan.Zero);

            Assert.AreEqual(0, command.CommandTimeout,
                "TimeSpan.Zero must keep its ADO.NET meaning of no timeout.");
        }
    }
}
