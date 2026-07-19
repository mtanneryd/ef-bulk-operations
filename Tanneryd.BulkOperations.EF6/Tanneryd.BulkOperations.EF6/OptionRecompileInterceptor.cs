using System;
using System.Data.Common;
using System.Data.Entity.Infrastructure.Interception;

namespace Tanneryd.BulkOperations.EF6
{
    /// <summary>
    /// EF6 interceptor that appends OPTION (RECOMPILE) to every command so
    /// SQL Server does not reuse parameter-sniffed plans. Dispose to unregister
    /// from DbInterception; leaving it registered affects the whole AppDomain.
    /// </summary>
    public class OptionRecompileInterceptor : DbCommandInterceptor, IDisposable
    {
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

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                DbInterception.Remove(this);
            }
        }
    }
}
