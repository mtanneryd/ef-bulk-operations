// <auto-generated>
// ReSharper disable All

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Tanneryd.BulkOperations.EFCore.Tests
{
    // InvoiceItem
    public class InvoiceItem
    {
        public int PrimaryKey { get; set; } // PrimaryKey (Primary key)
        public Guid JournalId { get; set; } // JournalId
        public Guid InvoiceId { get; set; } // InvoiceId

        // Foreign keys

        /// <summary>
        /// Parent Invoice pointed by [InvoiceItem].([InvoiceId]) (FK_dbo.InvoiceItem_dbo.Invoice_InvoiceId)
        /// </summary>
        public Invoice Invoice { get; set; } // FK_dbo.InvoiceItem_dbo.Invoice_InvoiceId

        /// <summary>
        /// Parent Journal pointed by [InvoiceItem].([JournalId]) (FK_dbo.InvoiceItem_dbo.Journal_JournalId)
        /// </summary>
        public Journal Journal { get; set; } // FK_dbo.InvoiceItem_dbo.Journal_JournalId
    }

}
// </auto-generated>
