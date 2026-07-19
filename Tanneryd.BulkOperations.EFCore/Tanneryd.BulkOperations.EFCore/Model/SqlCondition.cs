namespace Tanneryd.BulkOperations.EFCore.Model
{
    /// <summary>
    /// A single equality (or IS NULL) predicate used by bulk delete filtering.
    /// Values are parameterized; null/DBNull becomes IS NULL.
    /// </summary>
    public class SqlCondition
    {
        public SqlCondition(string columnName, dynamic columnValue)
        {
            ColumnName = columnName;
            ColumnValue = columnValue;
        }

        /// <summary>
        /// Mapped table column name or entity property name.
        /// </summary>
        public string ColumnName { get; set; }

        public dynamic ColumnValue { get; set; }
    }
}
