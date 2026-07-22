using System;
using System.Data.Common;
using System.Data.Entity.Infrastructure.Interception;

namespace Tanneryd.BulkOperations.EF6
{
    /// <summary>
    /// EF6 <see cref="DbCommandInterceptor"/> that appends OPTION (RECOMPILE) to
    /// every intercepted command so SQL Server does not reuse parameter-sniffed plans.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Lifecycle</b>
    /// </para>
    /// <list type="number">
    /// <item>
    /// <description>
    /// Construction calls <see cref="DbInterception.Add"/> and registers this instance
    /// for the entire AppDomain (not per <c>DbContext</c>).
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// While registered, every EF6 NonQuery / Reader / Scalar command in the process
    /// gets OPTION (RECOMPILE) appended (once per command text).
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <see cref="Dispose()"/> calls <see cref="DbInterception.Remove"/> and must be
    /// used (prefer <c>using</c>) when the interceptor is no longer needed. Leaving it
    /// registered keeps rewriting all subsequent EF commands in the AppDomain.
    /// </description>
    /// </item>
    /// </list>
    /// <para>
    /// Prefer a short-lived scope around the work that needs recompile, for example:
    /// </para>
    /// <code>
    /// using (new OptionRecompileInterceptor())
    /// {
    ///     // EF6 commands here receive OPTION (RECOMPILE)
    /// }
    /// // interceptor unregistered; later EF commands are unchanged
    /// </code>
    /// </remarks>
    public class OptionRecompileInterceptor : DbCommandInterceptor, IDisposable
    {
        /// <summary>
        /// Registers this interceptor with <see cref="DbInterception"/> for the AppDomain.
        /// </summary>
        public OptionRecompileInterceptor()
        {
            DbInterception.Add(this);
        }

        static void AddOptionToCommand(DbCommand command)
        {
            string optionRecompileString = "\r\nOPTION (RECOMPILE)";

            if (!command.CommandText.Contains(optionRecompileString))
            {
                command.CommandText += optionRecompileString;
            }
        }

        public override void NonQueryExecuting(
            DbCommand command, DbCommandInterceptionContext<int> interceptionContext)
        {
            AddOptionToCommand(command);
        }

        public override void ReaderExecuting(
            DbCommand command, DbCommandInterceptionContext<DbDataReader> interceptionContext)
        {
            AddOptionToCommand(command);
        }

        public override void ScalarExecuting(
            DbCommand command, DbCommandInterceptionContext<object> interceptionContext)
        {
            AddOptionToCommand(command);
        }

        /// <summary>
        /// Unregisters this interceptor from <see cref="DbInterception"/>.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Unregisters from <see cref="DbInterception"/> when <paramref name="disposing"/>
        /// is <see langword="true"/>.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                DbInterception.Remove(this);
            }
        }
    }
}
