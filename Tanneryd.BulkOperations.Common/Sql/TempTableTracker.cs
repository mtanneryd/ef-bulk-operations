using System;
using System.Threading;

namespace Tanneryd.BulkOperations.Common.Sql
{
    /// <summary>
    /// Optional create/drop counters for session temp tables. Used by tests to
    /// verify temps are dropped even when bulk ops fail or cancel.
    /// No-op when no scope is active.
    /// </summary>
    internal static class TempTableTracker
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

        public static void NotifyDropped()
        {
            var scope = Current.Value;
            if (scope != null)
                scope.Dropped++;
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
            public int Dropped { get; set; }

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
