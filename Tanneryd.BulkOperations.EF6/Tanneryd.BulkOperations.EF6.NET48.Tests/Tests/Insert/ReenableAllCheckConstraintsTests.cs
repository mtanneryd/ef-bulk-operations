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
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tanneryd.BulkOperations.EF6.NET48.Tests.Tests.Insert
{
    /// <summary>
    /// Regression: the finally block in BulkInsertAllAsync must attempt to
    /// re-enable constraints on ALL tables even when one re-enable throws,
    /// and it must never mask the original insert exception with a
    /// re-enable failure.
    /// </summary>
    [TestClass]
    public class ReenableAllCheckConstraintsTests
    {
        [TestMethod]
        public async Task ShouldAttemptAllTables_WhenFirstReenableFails()
        {
            var attempted = new List<string>();
            var tables = new[] { "[dbo].[A]", "[dbo].[B]", "[dbo].[C]" };

            try
            {
                await DbContextExtensions.ReenableAllCheckConstraintsAsync(
                    tables,
                    name =>
                    {
                        attempted.Add(name);
                        return name == "[dbo].[A]"
                            ? Task.FromException(new InvalidOperationException("boom"))
                            : Task.CompletedTask;
                    },
                    throwOnFailure: true).ConfigureAwait(false);
            }
            catch (InvalidOperationException)
            {
                // Expected; single failures are rethrown as-is (see dedicated test below).
            }

            CollectionAssert.AreEqual(tables, attempted,
                "All tables must be attempted even when an earlier re-enable fails.");
        }

        [TestMethod]
        public async Task ShouldRethrowOriginalException_WhenSingleReenableFails()
        {
            // Single failure must surface with its original type (e.g. SqlException
            // in production), not wrapped in AggregateException, for back-compat.
            InvalidOperationException caught = null;
            try
            {
                await DbContextExtensions.ReenableAllCheckConstraintsAsync(
                    new[] { "[dbo].[A]", "[dbo].[B]" },
                    name => name == "[dbo].[A]"
                        ? Task.FromException(new InvalidOperationException("boom"))
                        : Task.CompletedTask,
                    throwOnFailure: true).ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                caught = ex;
            }

            Assert.IsNotNull(caught, "Expected the original exception type, not AggregateException.");
            Assert.AreEqual("boom", caught.Message);
        }

        [TestMethod]
        public async Task ShouldThrowAggregateWithAllFailures_WhenThrowOnFailureIsTrue()
        {
            var tables = new[] { "[dbo].[A]", "[dbo].[B]", "[dbo].[C]" };

            AggregateException caught = null;
            try
            {
                await DbContextExtensions.ReenableAllCheckConstraintsAsync(
                    tables,
                    name => name == "[dbo].[B]"
                        ? Task.CompletedTask
                        : Task.FromException(new InvalidOperationException(name)),
                    throwOnFailure: true).ConfigureAwait(false);
            }
            catch (AggregateException ex)
            {
                caught = ex;
            }

            Assert.IsNotNull(caught, "Expected an AggregateException when re-enable fails after a successful insert.");
            Assert.AreEqual(2, caught.InnerExceptions.Count);
            CollectionAssert.AreEquivalent(
                new[] { "[dbo].[A]", "[dbo].[C]" },
                caught.InnerExceptions.Select(e => e.Message).ToArray());
        }

        [TestMethod]
        public async Task ShouldNotThrow_WhenThrowOnFailureIsFalse()
        {
            var attempted = new List<string>();
            var tables = new[] { "[dbo].[A]", "[dbo].[B]" };

            // throwOnFailure=false models the path where the insert itself already
            // failed: the original exception must not be masked by re-enable errors.
            await DbContextExtensions.ReenableAllCheckConstraintsAsync(
                tables,
                name =>
                {
                    attempted.Add(name);
                    return Task.FromException(new InvalidOperationException("boom"));
                },
                throwOnFailure: false).ConfigureAwait(false);

            CollectionAssert.AreEqual(tables, attempted);
        }

        [TestMethod]
        public async Task ShouldNotThrow_WhenAllReenablesSucceed()
        {
            var attempted = new List<string>();

            await DbContextExtensions.ReenableAllCheckConstraintsAsync(
                new[] { "[dbo].[A]", "[dbo].[B]" },
                name =>
                {
                    attempted.Add(name);
                    return Task.CompletedTask;
                },
                throwOnFailure: true).ConfigureAwait(false);

            Assert.AreEqual(2, attempted.Count);
        }

        [TestMethod]
        public async Task ShouldDoNothing_WhenNoTablesWereTouched()
        {
            await DbContextExtensions.ReenableAllCheckConstraintsAsync(
                Array.Empty<string>(),
                _ => Task.FromException(new InvalidOperationException("must not be called")),
                throwOnFailure: true).ConfigureAwait(false);
        }
    }
}
