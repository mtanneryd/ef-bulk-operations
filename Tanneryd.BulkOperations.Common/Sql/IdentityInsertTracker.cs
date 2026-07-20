using System;
using System.Threading;

namespace Tanneryd.BulkOperations.Common.Sql
{
    /// <summary>
    /// Optional IDENTITY_INSERT ON/OFF counters. Used by tests to verify
    /// SET IDENTITY_INSERT is disabled after enable. No-op when no scope is active.
    /// </summary>
    internal static class IdentityInsertTracker
    {
        private static readonly AsyncLocal<Scope> Current = new AsyncLocal<Scope>();

        public static Scope BeginScope()
        {
            var scope = new Scope(Current.Value);
            Current.Value = scope;
            return scope;
        }

        public static void NotifyEnabled()
        {
            var scope = Current.Value;
            if (scope != null)
                scope.Enabled++;
        }

        public static void NotifyDisabled()
        {
            var scope = Current.Value;
            if (scope != null)
                scope.Disabled++;
        }

        internal sealed class Scope : IDisposable
        {
            private readonly Scope _previous;
            private bool _disposed;

            public Scope(Scope previous)
            {
                _previous = previous;
            }

            public int Enabled { get; set; }
            public int Disabled { get; set; }

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
