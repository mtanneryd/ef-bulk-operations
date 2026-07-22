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
            var scope = Current.Value;
            if (scope != null)
                scope.Created++;
        }

        public static void NotifyDisposed()
        {
            var scope = Current.Value;
            if (scope != null)
                scope.Disposed++;
        }

        internal sealed class Scope : IDisposable
        {
            private readonly Scope _previous;
            private bool _disposed;

            public Scope(Scope previous)
            {
                _previous = previous;
            }

            public int Created { get; set; }
            public int Disposed { get; set; }

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
