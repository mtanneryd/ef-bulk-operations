using System;
using System.Threading;

namespace Tanneryd.BulkOperations.Common.Sql
{
    /// <summary>
    /// Optional create/dispose counters for owned SqlTransactions (e.g. BulkUpdate
    /// concurrency). Used by tests to verify transactions are disposed after
    /// Commit/Rollback. No-op when no scope is active.
    /// </summary>
    internal static class SqlTransactionTracker
    {
        private static readonly AsyncLocal<Scope> Current = new AsyncLocal<Scope>();

        public static Scope BeginScope()
        {
            var scope = new Scope(Current.Value);
            Current.Value = scope;
            return scope;
        }

        public static void NotifyCreated()
        {
            Current.Value?.NotifyCreated();
        }

        public static void NotifyDisposed()
        {
            Current.Value?.NotifyDisposed();
        }

        internal sealed class Scope : IDisposable
        {
            private readonly Scope _previous;
            private bool _disposed;
            private int _created;
            private int _disposedCount;

            public Scope(Scope previous)
            {
                _previous = previous;
            }

            // Interlocked: AsyncLocal flows into child tasks, so parallel
            // branches may increment concurrently.
            public int Created => Volatile.Read(ref _created);
            public int Disposed => Volatile.Read(ref _disposedCount);

            internal void NotifyCreated() => Interlocked.Increment(ref _created);
            internal void NotifyDisposed() => Interlocked.Increment(ref _disposedCount);

            public void Dispose()
            {
                if (_disposed)
                    return;

                _disposed = true;
                Current.Value = _previous;
            }
        }
    }
}
