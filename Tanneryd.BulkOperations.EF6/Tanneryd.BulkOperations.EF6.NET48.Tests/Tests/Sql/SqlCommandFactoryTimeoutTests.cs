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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.Common.Sql;

namespace Tanneryd.BulkOperations.EF6.NET48.Tests.Tests.Sql
{
    /// <summary>
    /// The TimeSpan -> CommandTimeout conversion must reject negative values,
    /// clamp values too large for an int instead of overflowing to a negative
    /// timeout, and preserve TimeSpan.Zero's ADO.NET meaning of no timeout.
    /// </summary>
    [TestClass]
    public class SqlCommandFactoryTimeoutTests
    {
        [TestMethod]
        public void ToCommandTimeoutSeconds_ShouldThrow_ForNegativeTimeout()
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(
                () => SqlCommandFactory.ToCommandTimeoutSeconds(TimeSpan.FromSeconds(-1)));
        }

        [TestMethod]
        public void ToCommandTimeoutSeconds_ShouldReturnZero_ForZeroTimeout()
        {
            Assert.AreEqual(0, SqlCommandFactory.ToCommandTimeoutSeconds(TimeSpan.Zero));
        }

        [TestMethod]
        public void ToCommandTimeoutSeconds_ShouldReturnWholeSeconds_ForRegularTimeout()
        {
            Assert.AreEqual(90, SqlCommandFactory.ToCommandTimeoutSeconds(TimeSpan.FromSeconds(90.7)));
        }

        [TestMethod]
        public void ToCommandTimeoutSeconds_ShouldClamp_ForTimeSpanMaxValue()
        {
            Assert.AreEqual(int.MaxValue, SqlCommandFactory.ToCommandTimeoutSeconds(TimeSpan.MaxValue));
        }
    }
}
