//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Manual changes to this file may cause unexpected behavior in your application.
//     Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace issue_31_reproduction.dbmodel
{
    using System;
    using System.Collections.Generic;
    
    public partial class LogItem
    {
        public long Id { get; set; }
        public System.DateTime Date { get; set; }
        public string Thread { get; set; }
        public string Level { get; set; }
        public string Logger { get; set; }
        public string Message { get; set; }
        public string Exception { get; set; }
        public Nullable<System.Guid> EntityId { get; set; }
        public string EntityLogicalName { get; set; }
        public long ConfigId { get; set; }
        public string SdkMessage { get; set; }
        public Nullable<System.Guid> CrmUser { get; set; }
        public Nullable<System.Guid> CorrelationId { get; set; }
        public Nullable<System.Guid> OrganizationId { get; set; }
    }
}
