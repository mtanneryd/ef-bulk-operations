using Microsoft.Data.SqlClient;
using System;

namespace Tanneryd.BulkOperations.Common.Sql
{
    /// <summary>
    /// Shared SqlBulkCopy option/timeout resolution. TableLock is opt-in; a null
    /// timeout keeps the historical 10-minute default used when callers omit one.
    /// </summary>
    internal static class BulkCopySettings
    {
        public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(10);

        /// <summary>
        /// Applies <see cref="SqlBulkCopyOptions.TableLock"/> only when requested.
        /// TABLOCK must never be enabled by default — it changes locking behavior
        /// for every staging/insert bulk copy.
        /// </summary>
        public static SqlBulkCopyOptions ApplyTableLock(SqlBulkCopyOptions options, bool useTableLock)
        {
            return useTableLock ? options | SqlBulkCopyOptions.TableLock : options;
        }

        /// <summary>
        /// Resolves ADO.NET <c>BulkCopyTimeout</c> seconds. Null means
        /// <see cref="DefaultTimeout"/> (not the select-request 1-minute default).
        /// </summary>
        public static int ResolveTimeoutSeconds(TimeSpan? bulkCopyTimeout)
        {
            return SqlCommandFactory.ToCommandTimeoutSeconds(bulkCopyTimeout ?? DefaultTimeout);
        }
    }
}
