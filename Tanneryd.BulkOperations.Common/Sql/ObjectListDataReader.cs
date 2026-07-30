using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.Common;
using System.Linq;

namespace Tanneryd.BulkOperations.Common.Sql
{
    /// <summary>
    /// Streams rows from an object list without materializing them into a <see cref="DataTable"/>.
    /// Column schema is taken from an empty template table (names/types only).
    /// </summary>
    internal sealed class ObjectListDataReader : DbDataReader
    {
        private readonly DataTable _schema;
        private readonly ReadOnlyCollection<DataColumn> _columns;
        private readonly IList _items;
        private readonly Func<object, int, object[]> _materialize;
        private int _index = -1;
        private object[] _current;
        private bool _closed;

        public ObjectListDataReader(
            DataTable schema,
            IList items,
            Func<object, int, object[]> materialize)
        {
            _schema = schema ?? throw new ArgumentNullException(nameof(schema));
            _columns = new ReadOnlyCollection<DataColumn>(
                new List<DataColumn>(_schema.Columns.Cast<DataColumn>()));
            _items = items ?? throw new ArgumentNullException(nameof(items));
            _materialize = materialize ?? throw new ArgumentNullException(nameof(materialize));
        }

        public override int FieldCount => _columns.Count;

        public override bool HasRows => _items.Count > 0;

        public override bool IsClosed => _closed;

        public override int RecordsAffected => -1;

        public override int Depth => 0;

        public override object this[int ordinal] => GetValue(ordinal);

        public override object this[string name] => GetValue(GetOrdinal(name));

        public override bool Read()
        {
            if (_closed)
                throw new InvalidOperationException("DataReader is closed.");

            _index++;
            if (_index >= _items.Count)
            {
                _current = null;
                return false;
            }

            _current = _materialize(_items[_index], _index)
                       ?? throw new InvalidOperationException("Row materializer returned null.");
            if (_current.Length != FieldCount)
            {
                throw new InvalidOperationException(
                    $"Row materializer returned {_current.Length} values; expected {FieldCount}.");
            }

            return true;
        }

        public override bool NextResult() => false;

        public override string GetName(int ordinal) => _columns[ordinal].ColumnName;

        public override int GetOrdinal(string name)
        {
            var index = _schema.Columns.IndexOf(name);
            if (index < 0)
                throw new IndexOutOfRangeException($"Column '{name}' not found.");
            return index;
        }

        public override Type GetFieldType(int ordinal) => _columns[ordinal].DataType;

        public override string GetDataTypeName(int ordinal) => GetFieldType(ordinal).Name;

        public override object GetValue(int ordinal)
        {
            EnsureRow();
            var value = _current[ordinal];
            return value ?? DBNull.Value;
        }

        public override int GetValues(object[] values)
        {
            EnsureRow();
            var count = Math.Min(values.Length, FieldCount);
            for (var i = 0; i < count; i++)
                values[i] = _current[i] ?? DBNull.Value;
            return count;
        }

        public override bool IsDBNull(int ordinal)
        {
            EnsureRow();
            var value = _current[ordinal];
            return value == null || value == DBNull.Value;
        }

        public override bool GetBoolean(int ordinal) => (bool)GetValue(ordinal);
        public override byte GetByte(int ordinal) => (byte)GetValue(ordinal);
        public override char GetChar(int ordinal) => (char)GetValue(ordinal);
        public override DateTime GetDateTime(int ordinal) => (DateTime)GetValue(ordinal);
        public override decimal GetDecimal(int ordinal) => (decimal)GetValue(ordinal);
        public override double GetDouble(int ordinal) => (double)GetValue(ordinal);
        public override float GetFloat(int ordinal) => (float)GetValue(ordinal);
        public override Guid GetGuid(int ordinal) => (Guid)GetValue(ordinal);
        public override short GetInt16(int ordinal) => (short)GetValue(ordinal);
        public override int GetInt32(int ordinal) => (int)GetValue(ordinal);
        public override long GetInt64(int ordinal) => (long)GetValue(ordinal);
        public override string GetString(int ordinal) => (string)GetValue(ordinal);

        public override long GetBytes(int ordinal, long dataOffset, byte[] buffer, int bufferOffset, int length)
        {
            var data = (byte[])GetValue(ordinal);
            return CopyArray(data, dataOffset, buffer, bufferOffset, length);
        }

        public override long GetChars(int ordinal, long dataOffset, char[] buffer, int bufferOffset, int length)
        {
            // Copy straight from the string into the caller's buffer; ToCharArray
            // would allocate the whole string on every chunked read.
            var data = (string)GetValue(ordinal);
            if (buffer == null)
                return data.Length;

            var available = data.Length - (int)dataOffset;
            var toCopy = Math.Min(length, available);
            data.CopyTo((int)dataOffset, buffer, bufferOffset, toCopy);
            return toCopy;
        }

        public override DataTable GetSchemaTable()
        {
            var schemaTable = new DataTable("SchemaTable");
            schemaTable.Columns.Add("ColumnName", typeof(string));
            schemaTable.Columns.Add("ColumnOrdinal", typeof(int));
            schemaTable.Columns.Add("DataType", typeof(Type));
            schemaTable.Columns.Add("AllowDBNull", typeof(bool));

            for (var i = 0; i < _columns.Count; i++)
            {
                var column = _columns[i];
                schemaTable.Rows.Add(column.ColumnName, i, column.DataType, true);
            }

            return schemaTable;
        }

        public override System.Collections.IEnumerator GetEnumerator() =>
            new DbEnumerator(this);

        public override void Close()
        {
            _closed = true;
            _current = null;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                Close();
            base.Dispose(disposing);
        }

        private void EnsureRow()
        {
            if (_closed)
                throw new InvalidOperationException("DataReader is closed.");
            if (_current == null)
                throw new InvalidOperationException("No current row. Call Read() first.");
        }

        private static long CopyArray<T>(T[] data, long dataOffset, T[] buffer, int bufferOffset, int length)
        {
            if (buffer == null)
                return data.Length;

            var available = data.Length - (int)dataOffset;
            var toCopy = Math.Min(length, available);
            Array.Copy(data, (int)dataOffset, buffer, bufferOffset, toCopy);
            return toCopy;
        }
    }
}
