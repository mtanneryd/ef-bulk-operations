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
            Current.Value?.NotifyCreated();
        }

        public static void NotifyDropped()
        {
            Current.Value?.NotifyDropped();
        }

        internal sealed class Scope : IDisposable
        {
            private readonly Scope _previous;
            private bool _disposed;
            private int _created;
            private int _dropped;

            public Scope(Scope previous)
            {
                _previous = previous;
            }

            // Interlocked: AsyncLocal flows into child tasks, so parallel
            // branches may increment concurrently.
            public int Created => Volatile.Read(ref _created);
            public int Dropped => Volatile.Read(ref _dropped);

            internal void NotifyCreated() => Interlocked.Increment(ref _created);
            internal void NotifyDropped() => Interlocked.Increment(ref _dropped);

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
