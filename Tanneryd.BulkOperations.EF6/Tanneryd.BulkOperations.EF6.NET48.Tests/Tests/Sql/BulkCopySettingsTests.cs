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
using Tanneryd.BulkOperations.Common.Sql;

namespace Tanneryd.BulkOperations.EF6.NET48.Tests.Tests.Sql
{
    /// <summary>
    /// SqlBulkCopy TableLock is opt-in and null timeouts must keep the historical
    /// 10-minute default — not silently inherit select-request defaults.
    /// </summary>
    [TestClass]
    public class BulkCopySettingsTests
    {
        [TestMethod]
        public void ApplyTableLock_ShouldLeaveOptionsUnchanged_WhenDisabled()
        {
            var options = BulkCopySettings.ApplyTableLock(SqlBulkCopyOptions.KeepIdentity, useTableLock: false);

            Assert.AreEqual(SqlBulkCopyOptions.KeepIdentity, options);
            Assert.IsFalse(options.HasFlag(SqlBulkCopyOptions.TableLock));
        }

        [TestMethod]
        public void ApplyTableLock_ShouldAddTableLock_WhenEnabled()
        {
            var options = BulkCopySettings.ApplyTableLock(SqlBulkCopyOptions.KeepIdentity, useTableLock: true);

            Assert.IsTrue(options.HasFlag(SqlBulkCopyOptions.KeepIdentity));
            Assert.IsTrue(options.HasFlag(SqlBulkCopyOptions.TableLock));
        }

        [TestMethod]
        public void ResolveTimeoutSeconds_ShouldUseTenMinuteDefault_WhenTimeoutIsNull()
        {
            Assert.AreEqual(
                (int)TimeSpan.FromMinutes(10).TotalSeconds,
                BulkCopySettings.ResolveTimeoutSeconds(null));
        }

        [TestMethod]
        public void ResolveTimeoutSeconds_ShouldHonorExplicitTimeout()
        {
            Assert.AreEqual(
                90,
                BulkCopySettings.ResolveTimeoutSeconds(TimeSpan.FromSeconds(90)));
        }
    }
}
