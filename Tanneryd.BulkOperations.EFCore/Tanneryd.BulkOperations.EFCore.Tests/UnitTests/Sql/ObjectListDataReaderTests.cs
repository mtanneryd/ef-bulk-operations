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
using System.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.Common.Sql;

namespace Tanneryd.BulkOperations.EFCore.Tests.UnitTests.Sql
{
    /// <summary>
    /// ObjectListDataReader streams entity rows into SqlBulkCopy. GetChars must
    /// copy via string.CopyTo (not ToCharArray) so large string columns do not
    /// allocate a full char[] on every chunked read; GetBytes must chunk via
    /// Array.Copy the same way for binary columns; null cells must surface as
    /// DBNull for SqlBulkCopy.
    /// </summary>
    [TestClass]
    public class ObjectListDataReaderTests
    {
        [TestMethod]
        public void GetChars_ShouldReturnFullLength_WhenBufferIsNull()
        {
            using var reader = CreateReader(new object[] { "abcdef" });
            Assert.IsTrue(reader.Read());

            Assert.AreEqual(6, reader.GetChars(0, 0, null, 0, 0));
        }

        [TestMethod]
        public void GetChars_ShouldCopyChunk_WithoutAllocatingWholeStringAsCharArray()
        {
            using var reader = CreateReader(new object[] { "abcdefghij" });
            Assert.IsTrue(reader.Read());

            var buffer = new char[4];
            var copied = reader.GetChars(0, 3, buffer, 0, 4);

            Assert.AreEqual(4, copied);
            CollectionAssert.AreEqual("defg".ToCharArray(), buffer);
        }

        [TestMethod]
        public void GetChars_ShouldClamp_WhenRequestRunsPastEndOfString()
        {
            using var reader = CreateReader(new object[] { "xyz" });
            Assert.IsTrue(reader.Read());

            var buffer = new char[8];
            var copied = reader.GetChars(0, 1, buffer, 0, 8);

            Assert.AreEqual(2, copied);
            Assert.AreEqual('y', buffer[0]);
            Assert.AreEqual('z', buffer[1]);
        }

        [TestMethod]
        public void GetBytes_ShouldReturnFullLength_WhenBufferIsNull()
        {
            using var reader = CreateByteReader(new byte[] { 1, 2, 3, 4, 5, 6 });
            Assert.IsTrue(reader.Read());

            Assert.AreEqual(6, reader.GetBytes(0, 0, null, 0, 0));
        }

        [TestMethod]
        public void GetBytes_ShouldCopyChunk_WithDataOffset()
        {
            using var reader = CreateByteReader(new byte[] { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 });
            Assert.IsTrue(reader.Read());

            var buffer = new byte[4];
            var copied = reader.GetBytes(0, 3, buffer, 0, 4);

            Assert.AreEqual(4, copied);
            CollectionAssert.AreEqual(new byte[] { 40, 50, 60, 70 }, buffer);
        }

        [TestMethod]
        public void GetBytes_ShouldClamp_WhenRequestRunsPastEndOfArray()
        {
            using var reader = CreateByteReader(new byte[] { 1, 2, 3 });
            Assert.IsTrue(reader.Read());

            var buffer = new byte[8];
            var copied = reader.GetBytes(0, 1, buffer, 0, 8);

            Assert.AreEqual(2, copied);
            Assert.AreEqual((byte)2, buffer[0]);
            Assert.AreEqual((byte)3, buffer[1]);
        }

        [TestMethod]
        public void GetValue_ShouldReturnDbNull_ForNullCell()
        {
            using var reader = CreateReader(new object[] { null });
            Assert.IsTrue(reader.Read());

            Assert.AreSame(DBNull.Value, reader.GetValue(0));
            Assert.IsTrue(reader.IsDBNull(0));
        }

        [TestMethod]
        public void Read_ShouldThrow_WhenMaterializerReturnsWrongFieldCount()
        {
            var schema = new DataTable();
            schema.Columns.Add("A", typeof(string));
            schema.Columns.Add("B", typeof(int));

            using var reader = new ObjectListDataReader(
                schema,
                new List<object> { new object() },
                (_, __) => new object[] { "only-one" });

            Assert.ThrowsExactly<InvalidOperationException>(() => reader.Read());
        }

        [TestMethod]
        public void GetValue_ShouldThrow_BeforeRead()
        {
            using var reader = CreateReader(new object[] { "x" });

            Assert.ThrowsExactly<InvalidOperationException>(() => reader.GetValue(0));
        }

        private static ObjectListDataReader CreateReader(object[] row)
        {
            var schema = new DataTable();
            schema.Columns.Add("Col0", typeof(string));
            return new ObjectListDataReader(
                schema,
                new List<object> { new object() },
                (_, __) => row);
        }

        private static ObjectListDataReader CreateByteReader(byte[] row)
        {
            var schema = new DataTable();
            schema.Columns.Add("Col0", typeof(byte[]));
            return new ObjectListDataReader(
                schema,
                new List<object> { new object() },
                (_, __) => new object[] { row });
        }
    }
}
