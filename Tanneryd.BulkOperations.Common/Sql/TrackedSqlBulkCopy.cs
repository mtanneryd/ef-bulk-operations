using Microsoft.Data.SqlClient;
using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace Tanneryd.BulkOperations.Common.Sql
{
    /// <summary>
    /// Disposable wrapper around <see cref="SqlBulkCopy"/> that reports create/dispose
    /// to <see cref="SqlResourceTracker"/>.
    /// </summary>
    internal sealed class TrackedSqlBulkCopy : IDisposable
    {
        private readonly SqlBulkCopy _bulkCopy;
        private bool _disposed;

        public TrackedSqlBulkCopy(SqlBulkCopy bulkCopy)
        {
            _bulkCopy = bulkCopy ?? throw new ArgumentNullException(nameof(bulkCopy));
            SqlResourceTracker.NotifyCreated();
        }

        public SqlBulkCopyColumnMappingCollection ColumnMappings => _bulkCopy.ColumnMappings;

        public string DestinationTableName
        {
            get => _bulkCopy.DestinationTableName;
            set => _bulkCopy.DestinationTableName = value;
        }

        public bool EnableStreaming
        {
            get => _bulkCopy.EnableStreaming;
            set => _bulkCopy.EnableStreaming = value;
        }

        public int BatchSize
        {
            get => _bulkCopy.BatchSize;
            set => _bulkCopy.BatchSize = value;
        }

        public int BulkCopyTimeout
        {
            get => _bulkCopy.BulkCopyTimeout;
            set => _bulkCopy.BulkCopyTimeout = value;
        }

        public Task WriteToServerAsync(IDataReader reader, CancellationToken cancellationToken = default) =>
            _bulkCopy.WriteToServerAsync(reader, cancellationToken);

        public void WriteToServer(IDataReader reader) =>
            _bulkCopy.WriteToServer(reader);

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _bulkCopy.Close();
            SqlResourceTracker.NotifyDisposed();
        }
    }
}
