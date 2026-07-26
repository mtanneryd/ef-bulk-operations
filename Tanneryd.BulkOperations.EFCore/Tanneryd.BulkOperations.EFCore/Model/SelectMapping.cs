using System;

namespace Tanneryd.BulkOperations.EFCore.Model
{
    public class SelectMapping
    {
       public string SelectPropertyName { get; set; }
       public Type SelectPropertyType { get; set; }
       public string SelectPropertySqlType { get; set; }
       public TableName TableName { get; set; }
       public string ItemPropertyName { get; set; }
       /// <summary>
       /// Principal (related) table column names for the FK join, paired by index
       /// with <see cref="FkToColumnNames"/>.
       /// </summary>
       public string[] FkFromColumnNames { get; set; }
       /// <summary>
       /// Dependent (entity) table FK column names for the FK join, paired by index
       /// with <see cref="FkFromColumnNames"/>.
       /// </summary>
       public string[] FkToColumnNames { get; set; }
    }
}
