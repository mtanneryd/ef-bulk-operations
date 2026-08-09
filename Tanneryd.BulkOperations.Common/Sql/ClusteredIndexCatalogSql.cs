using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using System.Data;

namespace Tanneryd.BulkOperations.Common.Sql
{
    /// <summary>
    /// Builds the sys.indexes catalog lookup used by SortUsingClusteredIndex.
    /// Table/schema names are always parameters so quotes in identifiers cannot
    /// break or alter the SQL.
    /// </summary>
    internal static class ClusteredIndexCatalogSql
    {
        public const string TableNameParameter = "@tableName";
        public const string SchemaParameter = "@schema";

        /// <summary>
        /// Returns the catalog query and appends the matching SqlParameters.
        /// When <paramref name="schema"/> is null/empty, the schema filter and
        /// <c>@schema</c> parameter are omitted.
        /// </summary>
        public static string BuildQuery(
            string schema,
            string tableName,
            ICollection<SqlParameter> parameters)
        {
            var query = @"
                    SELECT  col.name
                    FROM sys.indexes ind
                    INNER JOIN sys.index_columns ic ON ind.object_id = ic.object_id and ind.index_id = ic.index_id
                    INNER JOIN sys.columns col ON ic.object_id = col.object_id and ic.column_id = col.column_id
                    INNER JOIN sys.tables t ON ind.object_id = t.object_id
                    WHERE t.name = @tableName AND ind.type_desc = 'CLUSTERED'";

            parameters.Add(new SqlParameter(TableNameParameter, SqlDbType.NVarChar, 128) { Value = tableName });

            if (!string.IsNullOrEmpty(schema))
            {
                query += " AND SCHEMA_NAME(t.schema_id) = @schema";
                parameters.Add(new SqlParameter(SchemaParameter, SqlDbType.NVarChar, 128) { Value = schema });
            }

            query += " ORDER BY ic.index_column_id;";
            return query;
        }
    }
}
