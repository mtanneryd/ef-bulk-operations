using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;

namespace Tanneryd.BulkOperations.Common.Sql
{
    internal static class SqlConditionBuilder
    {
        public static string Build(
            IReadOnlyList<SqlConditionValue> conditions,
            string tableAlias,
            Func<string, string> resolveColumnName,
            ICollection<SqlParameter> parameters,
            string parameterPrefix)
        {
            var condStatements = new List<string>();
            for (var i = 0; i < conditions.Count; i++)
            {
                var condition = conditions[i];
                if (string.IsNullOrWhiteSpace(condition?.ColumnName))
                    throw new ArgumentException("SqlCondition column names must be set.");

                var columnName = resolveColumnName(condition.ColumnName);
                var paramName = $"@{parameterPrefix}{i}";

                if (condition.ColumnValue == null || condition.ColumnValue is DBNull)
                {
                    condStatements.Add($"[{tableAlias}].[{columnName}] IS NULL");
                }
                else
                {
                    condStatements.Add($"[{tableAlias}].[{columnName}] = {paramName}");
                    parameters.Add(new SqlParameter(paramName, condition.ColumnValue));
                }
            }

            return string.Join(" AND ", condStatements);
        }
    }

    internal sealed class SqlConditionValue
    {
        public SqlConditionValue(string columnName, object columnValue)
        {
            ColumnName = columnName;
            ColumnValue = columnValue;
        }

        public string ColumnName { get; }
        public object ColumnValue { get; }
    }
}
