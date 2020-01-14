using System;
using System.Data.Common;
using System.Data.Entity.Infrastructure.Interception;

namespace Tanneryd.BulkOperations.EF6.NetStd
{
    public class OptionRecompileInterceptor : DbCommandInterceptor, IDisposable
    {
        public OptionRecompileInterceptor()
        {
            DbInterception.Add(this);
        }

        static void AddOptionToCommand(DbCommand command)
        {
            string optionRecompileString = "\r\nOPTION (RECOMPILE)";

            if (!command.CommandText.Contains(optionRecompileString)) //Check the option is not added already
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