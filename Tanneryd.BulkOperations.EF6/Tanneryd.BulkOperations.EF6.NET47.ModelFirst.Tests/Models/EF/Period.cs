//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Manual changes to this file may cause unexpected behavior in your application.
//     Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Tanneryd.BulkOperations.EF6.NET47.ModelFirst.Tests.Models.EF
{
    using System;
    using System.Collections.Generic;
    
    public partial class Period
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")]
        public Period()
        {
            this.SummaryReportFROMTableASExtents = new HashSet<SummaryReportFROMTableASExtent>();
            this.SELECT_WORSE_FROM_NAMES_AS_Extent1 = new HashSet<SELECT_WORSE_FROM_NAMES_AS_Extent1>();
        }
    
        public int PeriodID { get; set; }
        public string Name { get; set; }
    
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<SummaryReportFROMTableASExtent> SummaryReportFROMTableASExtents { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<SELECT_WORSE_FROM_NAMES_AS_Extent1> SELECT_WORSE_FROM_NAMES_AS_Extent1 { get; set; }
    }
}