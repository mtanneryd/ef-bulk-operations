/*
 * Copyright ©  2017-2026 Tånneryd IT AB
 * Licensed under the Apache License, Version 2.0.
 */

using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tanneryd.BulkOperations.Common.Sql;
using Tanneryd.BulkOperations.EF6.Model;

namespace Tanneryd.BulkOperations.EF6
{
    public static partial class DbContextExtensions
    {
        private static void ValidateDbContext(DbContext ctx)
        {
            if (ctx == null)
                throw new ArgumentNullException(nameof(ctx));

            // Inspect without opening — same idea as EF Core ValidateDbContext.
            var connection = ctx.Database.Connection;
            if (connection is System.Data.Entity.Core.EntityClient.EntityConnection entityConnection)
                connection = entityConnection.StoreConnection;

            if (connection is not SqlConnection &&
                connection is not System.Data.SqlClient.SqlConnection)
            {
                throw new NotSupportedException(
                    $"Bulk operations require a SQL Server connection. Actual connection type: {connection?.GetType().FullName ?? "null"}.");
            }

            if (string.IsNullOrWhiteSpace(connection.ConnectionString))
                throw new InvalidOperationException("The database connection string is not set.");
        }

        private static void ValidateBulkDeleteRequest<T>(BulkDeleteRequest<T> request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (request.SqlConditions == null || request.SqlConditions.Length == 0)
                throw new ArgumentException("The SqlConditions request property must be set and contain at least one condition.");

            if (request.Items == null)
                throw new ArgumentNullException(nameof(request.Items));

            if (request.Items.Count == 0 && !request.AllowDeleteAllMatchingConditions)
            {
                throw new ArgumentException(
                    "Items is empty. Refusing to delete the entire SqlConditions window. " +
                    "Provide at least one keeper in Items, or set AllowDeleteAllMatchingConditions to true " +
                    "to explicitly opt in to deleting every matching row.",
                    nameof(request.Items));
            }
        }

        private static void ValidateBulkSelectRequest<T>(BulkSelectRequest<T> request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (request.Items == null)
                throw new ArgumentNullException(nameof(request.Items));

            if (request.KeyPropertyMappings == null || request.KeyPropertyMappings.Length == 0)
                throw new ArgumentException("The KeyPropertyMappings request property must be set and contain at least one name.");
        }

        private static void ValidateBulkUpdateRequest(BulkUpdateRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (request.Entities == null)
                throw new ArgumentNullException(nameof(request.Entities));

            // Public setters allow null; treat as empty (same as the ctor defaults).
            request.UpdatedPropertyNames ??= Array.Empty<string>();
            request.KeyPropertyNames ??= Array.Empty<string>();
        }

        private static void ValidateBulkInsertRequest<T>(BulkInsertRequest<T> request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (request.Entities == null)
                throw new ArgumentNullException(nameof(request.Entities));
        }

        private static string ResolveSqlConditionColumnName(string columnName, Mappings mappings)
        {
            if (mappings.ColumnMappingByColumnName.ContainsKey(columnName))
                return columnName;

            if (mappings.ColumnMappingByPropertyName.TryGetValue(columnName, out var mapping))
                return mapping.TableColumn.Name;

            throw new ArgumentException(
                $"Column '{columnName}' is not a mapped column on table '{mappings.TableName.Fullname}'.");
        }

        private static string BuildParameterizedSqlConditions(
            SqlCondition[] sqlConditions,
            Mappings mappings,
            string tableAlias,
            ICollection<SqlParameter> parameters,
            string parameterPrefix)
        {
            var conditions = sqlConditions
                .Select(c => new SqlConditionValue(c.ColumnName, c.ColumnValue))
                .ToList();

            return SqlConditionBuilder.Build(
                conditions,
                tableAlias,
                name => ResolveSqlConditionColumnName(name, mappings),
                parameters,
                parameterPrefix);
        }

        /// <summary>
        /// Creates a session-scoped temp table with the same column types as the
        /// target (including identity/discriminator metadata) by selecting zero
        /// rows via SELECT … INTO … WHERE 1=0. The name is a GUID to avoid collisions.
        /// </summary>

        private static async Task<string> CreateTempTableAsync(
            SqlServerConnection connection,
            SqlTransaction transaction,
            TableName tableName,
            Discriminator discriminator,
            string[] columnNames,
            IncludeRowNumber includeRowNumber = IncludeRowNumber.No,
            CancellationToken cancellationToken = default,
            ISet<string> castToVarBinary8Columns = null,
            TableColumn[] extraColumnNames = null)
        {
            extraColumnNames = extraColumnNames ?? Array.Empty<TableColumn>();
            var selectClause = string.Join(",", columnNames.Select(p =>
                castToVarBinary8Columns != null && castToVarBinary8Columns.Contains(p)
                    ? $"CAST([{p}] AS varbinary(8)) AS [{p}]"
                    : $"[{p}]"));

            if (discriminator != null)
            {
                selectClause = string.IsNullOrEmpty(selectClause)
                    ? $"[{discriminator.Column.Name}]"
                    : $"[{discriminator.Column.Name}]," + selectClause;
            }

            if (includeRowNumber == IncludeRowNumber.Yes)
            {
                selectClause = string.IsNullOrEmpty(selectClause)
                    ? "cast(1 as int) as rowno"
                    : "cast(1 as int) as rowno," + selectClause;
            }

            foreach (var extraColumnName in extraColumnNames)
            {
                // CAST(NULL AS T) types the column for SELECT INTO … WHERE 1=0.
                // Avoid typed zero literals: CAST(0 AS uniqueidentifier) is invalid.
                if (string.IsNullOrEmpty(extraColumnName.SqlType))
                {
                    throw new ArgumentException(
                        "Extra temp-table column '" + extraColumnName.Name + "' is missing SqlType.");
                }
                var extra = $"cast(null as {extraColumnName.SqlType}) as [{extraColumnName.Name}]";
                selectClause = string.IsNullOrEmpty(selectClause) ? extra : selectClause + "," + extra;
            }

            var guid = Guid.NewGuid().ToString("N");
            var tempTableName = $"tempdb..#{guid}";
            var query = $@"   
                        IF OBJECT_ID('{tempTableName}') IS NOT NULL DROP TABLE {tempTableName}

                        SELECT {selectClause}
                        INTO {tempTableName}
                        FROM {tableName.Fullname}
                        WHERE 1=0";
            await connection.ExecuteNonQueryAsync(query, transaction, TimeSpan.FromSeconds(30), cancellationToken)
                .ConfigureAwait(false);
            TempTableTracker.NotifyCreated();

            return tempTableName;
        }

        private static Task DropTempTableAsync(
            SqlServerConnection connection,
            SqlTransaction transaction,
            string tempTableName,
            CancellationToken cancellationToken = default)
        {
            return connection.DropTempTableAsync(transaction, tempTableName, cancellationToken);
        }

        /// <summary>
        /// Builds a SqlBulkCopy session and DataTable column schema for the mapped properties.
        /// Rows are streamed via <see cref="ObjectListDataReader"/>; do not fill the DataTable.
        /// TableLock (TABLOCK) is opt-in. Unmapped CLR properties are skipped.
        /// </summary>
        private static SqlBulkCopySession CreateBulkCopy(
            DataTable table,
            BulkPropertyInfo[] properties,
            Dictionary<string, TableColumnMapping> columnMappings,
            SqlServerConnection connection,
            SqlTransaction transaction,
            string tableName,
            Discriminator discriminator,
            SqlBulkCopyOptions options = SqlBulkCopyOptions.Default,
            IncludeRowNumber includeRowNumber = IncludeRowNumber.No,
            TimeSpan? bulkCopyTimeout = null,
            bool useTableLock = false,
            TableColumn[] extraColumnNames = null)
        {
            if (useTableLock)
                options |= SqlBulkCopyOptions.TableLock;

            extraColumnNames = extraColumnNames ?? Array.Empty<TableColumn>();
            var bulkCopy = connection.CreateBulkCopy(options, transaction, tableName);
            bulkCopy.EnableStreaming = true;
            // BatchSize left at ADO.NET default (0). Avoid the previous hard-coded 1_000_000.
            bulkCopy.BulkCopyTimeout = (int)(bulkCopyTimeout ?? TimeSpan.FromMinutes(10)).TotalSeconds;

            foreach (var property in properties)
            {
                Type propertyType = property.Type;

                // Nullable properties need special treatment.
                if (propertyType.IsGenericType &&
                    propertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    propertyType = Nullable.GetUnderlyingType(propertyType);
                }

                // Skip unmapped CLR properties.
                if (columnMappings.ContainsKey(property.Name))
                {
                    // Column order may differ from CLR property order; map by name.
                    table.Columns.Add(new DataColumn(property.Name, propertyType));
                    var clrPropertyName = property.Name;
                    var tableColumnName = columnMappings[property.Name].TableColumn.Name;
                    bulkCopy.AddColumnMapping(clrPropertyName, tableColumnName);
                }
            }

            if (discriminator != null)
            {
                Type discriminatorType = Type.GetType(discriminator.Column.PrimitiveType.ClrEquivalentType.FullName);
                table.Columns.Add(new DataColumn(discriminator.Column.Name, discriminatorType));
                bulkCopy.AddColumnMapping(discriminator.Column.Name, discriminator.Column.Name);
            }

            foreach (var extraColumnName in extraColumnNames)
            {
                table.Columns.Add(new DataColumn(extraColumnName.Name, extraColumnName.Type));
                bulkCopy.AddColumnMapping(extraColumnName.Name, extraColumnName.Name);
            }

            if (includeRowNumber == IncludeRowNumber.Yes)
            {
                table.Columns.Add(new DataColumn("rowno", typeof(int)));
                bulkCopy.AddColumnMapping("rowno", "rowno");
            }

            return bulkCopy;
        }

        /// <summary>
        /// Runs UPDATE STATISTICS … WITH ALL for the given table. Shared by the
        /// public UpdateStatistics APIs and BulkInsertAll when UpdateStatistics is set.
        /// </summary>
        private static async Task<TimeSpan> UpdateStatisticsCoreAsync(
            DbContext ctx,
            TableName tableName,
            SqlTransaction transaction,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            var s0 = Stopwatch.StartNew();
            var query = $"UPDATE STATISTICS {tableName.Fullname} WITH ALL";
            var connection = await ResolveSqlConnectionAsync(ctx, cancellationToken).ConfigureAwait(false);
            using var cmd = CreateSqlCommand(query, connection, transaction, timeout);
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            s0.Stop();
            return s0.Elapsed;
        }

        /// <summary>
        /// Re-enables CHECK/FK constraints after AllowNotNullSelfReferences NOCHECK.
        /// Prefers WITH CHECK (trusted). If existing rows violate constraints, falls
        /// back to WITH NOCHECK so constraints are enabled again, then rethrows the
        /// WITH CHECK failure so callers still observe FK/CHECK violations (e.g.
        /// missing self-references). Always uses CancellationToken.None so a
        /// cancelled caller cannot skip re-enable. Catches both
        /// Microsoft.Data.SqlClient and System.Data.SqlClient SqlException so the
        /// legacy provider still runs the NOCHECK fallback.
        /// </summary>
        private static async Task ReenableCheckConstraintsAsync(
            DbContext ctx,
            string tableFullName,
            SqlTransaction transaction)
        {
            var connection = await ResolveSqlConnectionAsync(ctx, CancellationToken.None).ConfigureAwait(false);
            try
            {
                var withCheck =
                    $"ALTER TABLE {tableFullName} WITH CHECK CHECK CONSTRAINT ALL";
                using var cmd = CreateSqlCommand(withCheck, connection, transaction, TimeSpan.FromSeconds(30));
                await cmd.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is SqlException || ex is System.Data.SqlClient.SqlException)
            {
                // Data may be inconsistent after cancel or a bad self-ref graph.
                // Still turn constraints back on, then surface the validation error.
                var withNoCheck =
                    $"ALTER TABLE {tableFullName} WITH NOCHECK CHECK CONSTRAINT ALL";
                using var cmd = CreateSqlCommand(withNoCheck, connection, transaction, TimeSpan.FromSeconds(30));
                await cmd.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(false);
                throw;
            }
        }
    }
}
