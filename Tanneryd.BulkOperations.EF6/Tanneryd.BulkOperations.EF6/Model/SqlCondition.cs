namespace Tanneryd.BulkOperations.EF6.Model
{
    public class SqlCondition
    {
        public SqlCondition(string columnName, dynamic columnValue)
        {
            ColumnName = columnName;
            ColumnValue = columnValue;
        }

        public string ColumnName { get; set; }
        public dynamic ColumnValue { get; set; }
    }
}