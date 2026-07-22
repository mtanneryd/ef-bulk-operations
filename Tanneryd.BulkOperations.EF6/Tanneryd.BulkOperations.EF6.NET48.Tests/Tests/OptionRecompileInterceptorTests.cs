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
using System.Data.Common;
using System.Data.Entity.Infrastructure.Interception;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.EF6;
using Tanneryd.BulkOperations.EF6.NET48.Tests.Models.EF;

namespace Tanneryd.BulkOperations.EF6.NET48.Tests.Tests
{
    /// <summary>
    /// L3: OptionRecompileInterceptor registers with DbInterception in its
    /// constructor (AppDomain-wide). Dispose must unregister it; otherwise every
    /// subsequent EF6 command in the process gets OPTION (RECOMPILE).
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class OptionRecompileInterceptorTests : BulkOperationTestBase
    {
        private const string OptionRecompile = "\r\nOPTION (RECOMPILE)";

        [TestInitialize]
        public void Initialize()
        {
            InitializeUnitTestContext();
            CleanupUnitTestContext();
        }

        [TestCleanup]
        public void CleanUp()
        {
            CleanupUnitTestContext();
        }

        /// <summary>
        /// Constructing the interceptor registers it: ordinary EF queries should
        /// see OPTION (RECOMPILE) appended to CommandText before execution.
        /// </summary>
        [TestMethod]
        public void Constructor_ShouldRegisterInterceptor_AndAppendOptionRecompile()
        {
            // Register capture after OptionRecompile so Executing sees the rewritten text.
            var capture = new CommandTextCapturingInterceptor();
            OptionRecompileInterceptor interceptor = null;
            try
            {
                interceptor = new OptionRecompileInterceptor();
                DbInterception.Add(capture);

                using (var db = new UnitTestContext())
                {
                    db.Prices.ToList();
                }

                Assert.IsTrue(
                    capture.Commands.Any(c => c.Contains(OptionRecompile)),
                    "Expected at least one EF command to include OPTION (RECOMPILE) while registered.");
            }
            finally
            {
                interceptor?.Dispose();
                DbInterception.Remove(capture);
            }
        }

        /// <summary>
        /// Dispose unregisters from DbInterception so later EF commands in the
        /// AppDomain are not rewritten.
        /// </summary>
        [TestMethod]
        public void Dispose_ShouldUnregisterInterceptor_SoLaterCommandsAreUnchanged()
        {
            var capture = new CommandTextCapturingInterceptor();
            try
            {
                using (new OptionRecompileInterceptor())
                {
                    DbInterception.Add(capture);

                    using (var db = new UnitTestContext())
                    {
                        db.Prices.ToList();
                    }

                    Assert.IsTrue(
                        capture.Commands.Any(c => c.Contains(OptionRecompile)),
                        "Sanity: interceptor must rewrite commands while alive.");
                }

                capture.Commands.Clear();

                using (var db = new UnitTestContext())
                {
                    db.Prices.ToList();
                }

                Assert.IsFalse(
                    capture.Commands.Any(c => c.Contains(OptionRecompile)),
                    "After Dispose, EF commands must not receive OPTION (RECOMPILE).");
            }
            finally
            {
                DbInterception.Remove(capture);
            }
        }

        /// <summary>
        /// Captures CommandText on Executing so we can assert what EF actually
        /// sends after earlier interceptors (including OptionRecompile) mutate it.
        /// Must be registered after OptionRecompileInterceptor so it observes the
        /// rewritten text; always Remove in a finally to avoid AppDomain leaks.
        /// </summary>
        private sealed class CommandTextCapturingInterceptor : DbCommandInterceptor
        {
            public List<string> Commands { get; } = new List<string>();

            public override void NonQueryExecuting(
                DbCommand command,
                DbCommandInterceptionContext<int> interceptionContext)
            {
                Commands.Add(command.CommandText);
            }

            public override void ReaderExecuting(
                DbCommand command,
                DbCommandInterceptionContext<DbDataReader> interceptionContext)
            {
                Commands.Add(command.CommandText);
            }

            public override void ScalarExecuting(
                DbCommand command,
                DbCommandInterceptionContext<object> interceptionContext)
            {
                Commands.Add(command.CommandText);
            }
        }
    }
}
