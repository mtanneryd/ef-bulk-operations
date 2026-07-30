/*
 * Copyright ©  2017-2026 Tånneryd IT AB
 * Licensed under the Apache License, Version 2.0.
 */

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tanneryd.BulkOperations.Common.Sql;
using Tanneryd.BulkOperations.EFCore.Model;

namespace Tanneryd.BulkOperations.EFCore
{
    public static partial class DbContextExtensions
    {
        /// <summary>
        /// Returns a MappingsExtractor for the context's <see cref="IModel"/>.
        /// Cached weakly by model identity (not context CLR type) so the same
        /// DbContext class with different models does not reuse stale mappings,
        /// and models that are no longer referenced can be collected together
        /// with their cached extractor. The extractor only retains the model,
        /// never the context instance.
        /// </summary>
        internal static MappingsExtractor GetMappingExtractor(DbContext ctx)
        {
            return _mappingExtractorsByModel.GetValue(ctx.Model, static model => new MappingsExtractor(model));
        }

        private static void ValidateDbContext(DbContext ctx)
        {
            if (ctx == null)
                throw new ArgumentNullException(nameof(ctx));

            var connection = ctx.Database.GetDbConnection();
            if (connection is not SqlConnection)
                throw new NotSupportedException("Bulk operations require a Microsoft.Data.SqlClient.SqlConnection.");

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

        /// <summary>
        /// Same style as Select/Delete key validation — typos must not surface as KeyNotFoundException.
        /// </summary>
        private static void ThrowIfUnresolvedMappedPropertyNames(
            IEnumerable<string> propertyNames,
            IDictionary<string, TableColumnMapping> columnMappings,
            string requestPropertyName)
        {
            if (propertyNames == null)
                return;

            var unresolved = propertyNames
                .Where(name => !string.IsNullOrEmpty(name) && !columnMappings.ContainsKey(name))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (unresolved.Length == 0)
                return;

            throw new ArgumentException(
                requestPropertyName + " contain property name(s) that are not mapped on the target entity: " +
                string.Join(", ", unresolved) + ".");
        }

        private static void ValidateBulkInsertRequest<T>(BulkInsertRequest<T> request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (request.Entities == null)
                throw new ArgumentNullException(nameof(request.Entities));
        }

        /// <summary>
        /// Owned types are not supported: table-sharing OwnsOne columns are omitted
        /// from owner mappings, and OwnsMany / separate-table ownership need a
        /// dedicated insert path. Fail clearly instead of silent data loss.
        /// </summary>
        private static void EnsureBulkOperationsSupportEntityType(DbContext ctx, Type clrType)
        {
            if (clrType == null || clrType == typeof(ExpandoObject))
                return;

            IEntityType entityType = null;
            for (var current = clrType; current != null && current != typeof(object); current = current.BaseType)
            {
                entityType = ctx.Model.FindEntityType(current);
                if (entityType != null)
                    break;
            }

            if (entityType == null)
                return;

            if (entityType.IsOwned())
            {
                throw new ArgumentException(
                    $"Bulk operations do not support owned entity types. '{clrType.Name}' is configured as an owned type.");
            }

            var ownedNavNames = entityType.GetNavigations()
                .Where(n => n.ForeignKey.IsOwnership)
                .Select(n => n.Name)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray();

            if (ownedNavNames.Length == 0)
                return;

            throw new ArgumentException(
                $"Bulk operations do not support owned entity types. Type '{entityType.ClrType.Name}' declares owned navigation(s): {string.Join(", ", ownedNavNames)}. " +
                "Table-sharing OwnsOne flattening and OwnsMany/separate-table ownership are not implemented.");
        }

        private static string ResolveSqlConditionColumnName(string columnName, Mappings mappings)
        {
            if (mappings.ColumnMappingByColumnName.ContainsKey(columnName))
                return columnName;

            if (mappings.ColumnMappingByPropertyName.TryGetValue(columnName, out var mapping))
                return mapping.TableColumn.Column.Name;

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
        /// target by selecting zero rows via SELECT … INTO … WHERE 1=0.
        /// The name is a GUID to avoid collisions.
        /// </summary>

        private static async Task<string> CreateTempTableAsync(
            SqlConnection connection,
            SqlTransaction transaction,
            TableName tableName,
            string[] columnNames,
            TableColumn[] extraColumnNames,
            IncludeRowNumber includeRowNumber = IncludeRowNumber.No,
            CancellationToken cancellationToken = default,
            ISet<string> castToVarBinary8Columns = null)
        {
            var selectClause = string.Join(",", columnNames.Select(p =>
                castToVarBinary8Columns != null && castToVarBinary8Columns.Contains(p)
                    ? $"CAST([{p}] AS varbinary(8)) AS [{p}]"
                    : $"[{p}]"));

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
            using var cmd = SqlCommandFactory.Create(query, connection, transaction, TimeSpan.FromSeconds(30));
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            TempTableTracker.NotifyCreated();

            return tempTableName;
        }

        private static Task DropTempTableAsync(
            SqlConnection connection,
            SqlTransaction transaction,
            string tempTableName,
            CancellationToken cancellationToken = default)
        {
            return TempTableSqlHelper.DropAsync(connection, transaction, tempTableName, cancellationToken);
        }

        private static TableColumn[] GetDiscriminatorExtraColumns(Discriminator discriminator)
        {
            if (discriminator == null)
                return Array.Empty<TableColumn>();

            var clrType = Nullable.GetUnderlyingType(discriminator.Column.ClrType) ?? discriminator.Column.ClrType;
            var sqlType = clrType == typeof(string) ? "nvarchar(128)"
                : clrType == typeof(int) ? "int"
                : clrType == typeof(long) ? "bigint"
                : "sql_variant";

            return
            [
                new TableColumn
                {
                    Name = discriminator.Column.Name,
                    Type = clrType,
                    SqlType = sqlType,
                    UseQuotes = clrType == typeof(string)
                }
            ];
        }

        /// <summary>
        /// Builds a tracked SqlBulkCopy session and DataTable column schema for the mapped
        /// properties. Rows are streamed via <see cref="ObjectListDataReader"/>; do not fill
        /// the DataTable. TableLock (TABLOCK) is opt-in. Unmapped CLR properties are skipped.
        /// Callers must Dispose the returned session.
        /// </summary>
        private static TrackedSqlBulkCopy CreateBulkCopy(
            DataTable table,
            BulkPropertyInfo[] properties,
            Dictionary<string, TableColumnMapping> columnMappings,
            SqlConnection connection,
            SqlTransaction transaction,
            string tableName,
            TableColumn[] extraColumnNames,
            SqlBulkCopyOptions options = SqlBulkCopyOptions.Default,
            IncludeRowNumber includeRowNumber = IncludeRowNumber.No,
            TimeSpan? bulkCopyTimeout = null,
            bool useTableLock = false)
        {
            if (useTableLock)
                options |= SqlBulkCopyOptions.TableLock;

            var bulkCopy = new SqlBulkCopy(connection, options, transaction)
            {
                DestinationTableName = tableName,
                EnableStreaming = true,
                // BatchSize 0 = ADO.NET default (single batch). Avoid the previous hard-coded
                // 1_000_000 which forced oversized internal batches under load.
                BulkCopyTimeout = (int)(bulkCopyTimeout ?? TimeSpan.FromMinutes(10)).TotalSeconds
            };

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
                    // Since we cannot trust the CLR type properties to be in the same order as
                    // the table columns we use the SqlBulkCopy column mappings.
                    table.Columns.Add(new DataColumn(property.Name, propertyType));
                    var clrPropertyName = property.Name;
                    var tableColumnName = columnMappings[property.Name].TableColumn.Column.Name;
                    bulkCopy.ColumnMappings.Add(new SqlBulkCopyColumnMapping(clrPropertyName, tableColumnName));
                }
            }

            foreach (var extraColumnName in extraColumnNames)
            {
                table.Columns.Add(new DataColumn(extraColumnName.Name, extraColumnName.Type));
                bulkCopy.ColumnMappings.Add(new SqlBulkCopyColumnMapping(extraColumnName.Name, extraColumnName.Name));
            }

            if (includeRowNumber == IncludeRowNumber.Yes)
            {
                table.Columns.Add(new DataColumn("rowno", typeof(int)));
                bulkCopy.ColumnMappings.Add(new SqlBulkCopyColumnMapping("rowno", "rowno"));
            }

            return new TrackedSqlBulkCopy(bulkCopy);
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
            var connection = await GetSqlConnectionAsync(ctx, cancellationToken).ConfigureAwait(false);
            using var cmd = CreateSqlCommand(query, connection, transaction, timeout);
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            s0.Stop();
            return s0.Elapsed;
        }

        /// <summary>
        /// Re-enables constraints on every table, even when earlier tables fail.
        /// Failures only surface when <paramref name="throwOnFailure"/> is true;
        /// callers pass false when the insert itself already failed so the original
        /// exception is not masked by re-enable errors. A single failure is rethrown
        /// as-is (preserving the original exception type, e.g. SqlException);
        /// multiple failures are wrapped in an <see cref="AggregateException"/>.
        /// </summary>
        internal static async Task ReenableAllCheckConstraintsAsync(
            IEnumerable<string> tableFullNames,
            Func<string, Task> reenableAsync,
            bool throwOnFailure)
        {
            List<Exception> failures = null;
            foreach (var tableFullName in tableFullNames)
            {
                try
                {
                    await reenableAsync(tableFullName).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    (failures ??= new List<Exception>()).Add(e);
                }
            }

            if (throwOnFailure && failures != null)
            {
                if (failures.Count == 1)
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();

                throw new AggregateException(
                    "Failed to re-enable CHECK/FK constraints on one or more tables.",
                    failures);
            }
        }

        /// <summary>
        /// Re-enables CHECK/FK constraints after AllowNotNullSelfReferences NOCHECK.
        /// Prefers WITH CHECK (trusted). If existing rows violate constraints, falls
        /// back to WITH NOCHECK so constraints are enabled again, then rethrows the
        /// WITH CHECK failure so callers still observe FK/CHECK violations (e.g.
        /// missing self-references). Always uses CancellationToken.None so a
        /// cancelled caller cannot skip re-enable.
        /// </summary>
        private static async Task ReenableCheckConstraintsAsync(
            DbContext ctx,
            string tableFullName,
            SqlTransaction transaction)
        {
            // A dead caller transaction (rolled back or server-aborted, e.g. after a
            // cancelled bulk copy) has already undone the NOCHECK ALTER TABLE, so the
            // constraints are back in their original state and there is nothing to
            // re-enable. Creating commands on a zombie transaction would throw
            // InvalidOperationException and mask the original error.
            if (SqlTransactionHelper.IsZombied(transaction))
                return;

            var connection = await GetSqlConnectionAsync(ctx, CancellationToken.None).ConfigureAwait(false);
            try
            {
                var withCheck =
                    $"ALTER TABLE {tableFullName} WITH CHECK CHECK CONSTRAINT ALL";
                using var cmd = CreateSqlCommand(withCheck, connection, transaction, TimeSpan.FromSeconds(30));
                await cmd.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (SqlException)
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
