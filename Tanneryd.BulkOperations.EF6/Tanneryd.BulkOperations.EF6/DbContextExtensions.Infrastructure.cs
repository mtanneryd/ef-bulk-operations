/*
 * Copyright ©  2017-2025 Tånneryd IT AB
 * Licensed under the Apache License, Version 2.0.
 */

using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
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

            var connection = ResolveSqlConnection(ctx);
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
        /// 
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="transaction"></param>
        /// <param name="tableName"></param>
        /// <param name="discriminator"></param>
        /// <param name="columnNames"></param>
        /// <param name="includeRowNumber"></param>
        /// <returns></returns>
        private static string CreateTempTable(
            SqlServerConnection connection,
            SqlTransaction transaction,
            TableName tableName,
            Discriminator discriminator,
            string[] columnNames,
            IncludeRowNumber includeRowNumber = IncludeRowNumber.No)
        {
            var selectClause = string.Join(",", columnNames.Select(p => $"[{p}]"));

            if (discriminator != null)
            {
                selectClause = $"[{discriminator.Column.Name}]," + selectClause;
            }

            if (includeRowNumber == IncludeRowNumber.Yes)
            {
                selectClause = "cast(1 as int) as rowno," + selectClause;
            }

            var guid = Guid.NewGuid().ToString("N");
            var tempTableName = $"tempdb..#{guid}";
            var query = $@"   
                        IF OBJECT_ID('{tempTableName}') IS NOT NULL DROP TABLE {tempTableName}

                        SELECT {selectClause}
                        INTO {tempTableName}
                        FROM {tableName.Fullname}
                        WHERE 1=0";
            connection.ExecuteNonQuery(query, transaction, TimeSpan.FromSeconds(30));

            return tempTableName;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="transaction"></param>
        /// <param name="tempTableName"></param>
        private static void DropTempTable(
            SqlServerConnection connection,
            SqlTransaction transaction,
            string tempTableName)
        {
            connection.DropTempTable(transaction, tempTableName);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="table"></param>
        /// <param name="properties"></param>
        /// <param name="columnMappings"></param>
        /// <param name="connection"></param>
        /// <param name="transaction"></param>
        /// <param name="tableName"></param>
        /// <param name="discriminator"></param>
        /// <param name="options"></param>
        /// <param name="includeRowNumber"></param>
        /// <returns></returns>
        private static SqlBulkCopySession CreateBulkCopy(
            DataTable table,
            BulkPropertyInfo[] properties,
            Dictionary<string, TableColumnMapping> columnMappings,
            SqlServerConnection connection,
            SqlTransaction transaction,
            string tableName,
            Discriminator discriminator,
            SqlBulkCopyOptions options = SqlBulkCopyOptions.Default,
            IncludeRowNumber includeRowNumber = IncludeRowNumber.No)
        {
            options = options | SqlBulkCopyOptions.TableLock;
            var bulkCopy = connection.CreateBulkCopy(options, transaction, tableName);
            bulkCopy.EnableStreaming = true;
            bulkCopy.BatchSize = 1000000;
            bulkCopy.BulkCopyTimeout = 10 * 60;

            foreach (var property in properties)
            {
                Type propertyType = property.Type;

                // Nullable properties need special treatment.
                if (propertyType.IsGenericType &&
                    propertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    propertyType = Nullable.GetUnderlyingType(propertyType);
                }

                // Ignore all properties that we have no mappings for. We might have done so
                // already but just to be really really sure.
                if (columnMappings.ContainsKey(property.Name))
                {
                    // Since we cannot trust the CLR type properties to be in the same order as
                    // the table columns we use the SqlBulkCopy column mappings.
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

            if (includeRowNumber == IncludeRowNumber.Yes)
            {
                table.Columns.Add(new DataColumn("rowno", typeof(int)));
                bulkCopy.AddColumnMapping("rowno", "rowno");
            }

            return bulkCopy;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <param name="ctx"></param>
        /// <param name="request"></param>
        /// <returns></returns>
    }
}
