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
using Tanneryd.BulkOperations.EFCore.Model;

namespace Tanneryd.BulkOperations.EFCore.Tests.UnitTests.Model
{
    /// <summary>
    /// DefaultValue must tolerate null SqlType (extra/nav-dot columns can omit it)
    /// and fall back to CLR type instead of NullReferenceException.
    /// </summary>
    [TestClass]
    public class TableColumnDefaultValueTests
    {
        [TestMethod]
        public void DefaultValue_ShouldNotThrow_WhenSqlTypeIsNull()
        {
            var column = new TableColumn { Type = typeof(int), SqlType = null };
            Assert.AreEqual("0", column.DefaultValue);
        }

        [TestMethod]
        public void DefaultValue_ShouldUseClrType_WhenSqlTypeIsNull()
        {
            Assert.AreEqual(
                "<empty>",
                new TableColumn { Type = typeof(string), SqlType = null }.DefaultValue);
            Assert.AreEqual(
                "0x0",
                new TableColumn { Type = typeof(Guid), SqlType = null, UseQuotes = false }.DefaultValue);
            Assert.AreEqual(
                "00000000-0000-0000-0000-000000000000",
                new TableColumn { Type = typeof(Guid), SqlType = null, UseQuotes = true }.DefaultValue);
        }
    }
}
