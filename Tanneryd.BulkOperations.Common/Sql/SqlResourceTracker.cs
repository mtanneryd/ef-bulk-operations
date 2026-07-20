using System;
using System.Threading;

namespace Tanneryd.BulkOperations.Common.Sql
{
    /// <summary>
    /// Optional create/dispose counters for SQL client resources. Used by tests to
    /// verify commands and bulk-copy sessions are disposed (review finding H4).
    /// No-op when no scope is active.
    /// </summary>
    internal static class SqlResourceTracker
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
