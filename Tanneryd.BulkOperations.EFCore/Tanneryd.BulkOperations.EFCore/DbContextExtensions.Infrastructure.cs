/*
 * Copyright ©  2017-2020 Tånneryd IT AB
 * Licensed under the Apache License, Version 2.0.
 */

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
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
        /// Returns a MappingsExtractor for the DbContext CLR type. Extractors are
        /// cached statically; the lock guards concurrent first-use initialization.
        /// </summary>
        private static MappingsExtractor GetMappingExtractor(DbContext ctx)
        {
            var contextType = ctx.GetType();
            lock (_mutex)
            {
                if (!_mappingExtractorsByContextType.TryGetValue(contextType, out var extractor))
                {
                    extractor = new MappingsExtractor(ctx);
                    _mappingExtractorsByContextType[contextType] = extractor;
                }

                return extractor;
            }
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
        private static string CreateTempTable(
            SqlConnection connection,
            SqlTransaction transaction,
            TableName tableName,
            string[] columnNames,
            TableColumn[] extraColumnNames,
            IncludeRowNumber includeRowNumber = IncludeRowNumber.No)
        {
            return CreateTempTableAsync(
                connection,
                transaction,
                tableName,
                columnNames,
                extraColumnNames,
                includeRowNumber).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        private static async Task<string> CreateTempTableAsync(
            SqlConnection connection,
            SqlTransaction transaction,
            TableName tableName,
            string[] columnNames,
            TableColumn[] extraColumnNames,
            IncludeRowNumber includeRowNumber = IncludeRowNumber.No,
            CancellationToken cancellationToken = default)
        {
            var selectClause = string.Join(",", columnNames.Select(p => $"[{p}]"));

            if (includeRowNumber == IncludeRowNumber.Yes)
            {
                selectClause = "cast(1 as int) as rowno," + selectClause;
            }

            foreach (var extraColumnName in extraColumnNames)
            {
                if (extraColumnName.UseQuotes)
                    selectClause = selectClause + $",cast('{extraColumnName.DefaultValue}' as {extraColumnName.SqlType}) as {extraColumnName.Name}";
                else
                    selectClause = selectClause + $",cast({extraColumnName.DefaultValue} as {extraColumnName.SqlType}) as {extraColumnName.Name}";
            }

            var guid = Guid.NewGuid().ToString("N");
            var tempTableName = $"tempdb..#{guid}";
            var query = $@"   
                        IF OBJECT_ID('{tempTableName}') IS NOT NULL DROP TABLE {tempTableName}

                        SELECT {selectClause}
                        INTO {tempTableName}
                        FROM {tableName.Fullname}
                        WHERE 1=0";
            var cmd = SqlCommandFactory.Create(query, connection, transaction, TimeSpan.FromSeconds(30));
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            return tempTableName;
        }

        private static void DropTempTable(
            SqlConnection connection,
            SqlTransaction transaction,
            string tempTableName)
        {
            DropTempTableAsync(connection, transaction, tempTableName).ConfigureAwait(false).GetAwaiter().GetResult();
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
        /// Builds a SqlBulkCopy instance and DataTable columns for the mapped properties.
        /// Always ORs in TableLock (TABLOCK) for throughput; expect reduced concurrency
        /// on the destination during the copy. Unmapped CLR properties are skipped.
        /// </summary>
        private static SqlBulkCopy CreateBulkCopy(
            DataTable table,
            BulkPropertyInfo[] properties,
            Dictionary<string, TableColumnMapping> columnMappings,
            SqlConnection connection,
            SqlTransaction transaction,
            string tableName,
            TableColumn[] extraColumnNames,
            SqlBulkCopyOptions options = SqlBulkCopyOptions.Default,
            IncludeRowNumber includeRowNumber = IncludeRowNumber.No)
        {
            options = options | SqlBulkCopyOptions.TableLock;
            var bulkCopy = new SqlBulkCopy(connection, options, transaction)
            {
                DestinationTableName = tableName,
                EnableStreaming = true,
                BatchSize = 1000000,
                BulkCopyTimeout = 10 * 60
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
            var connection = await GetSqlConnectionAsync(ctx, cancellationToken).ConfigureAwait(false);
            var cmd = CreateSqlCommand(query, connection, transaction, timeout);
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            s0.Stop();
            return s0.Elapsed;
        }
    }
}
