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
       public string FkFromPropertyName { get; set; }
       public string FkToPropertyName { get; set; }
    }
}