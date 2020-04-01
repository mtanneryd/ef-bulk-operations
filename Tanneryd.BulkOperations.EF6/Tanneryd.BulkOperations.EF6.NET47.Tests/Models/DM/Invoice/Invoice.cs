/*
* Copyright ©  2017-2019 Tånneryd IT AB
* 
* Licensed under the Apache License, Version 2.0 (the "License");
* you may not use this file except in compliance with the License.
* You may obtain a copy of the License at
* 
*   http://www.apache.org/licenses/LICENSE-2.0
* 
* Unless required by applicable law or agreed to in writing, software
* distributed under the License is distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and
* limitations under the License.
*/

using System;
using System.Collections.Generic;

namespace Tanneryd.BulkOperations.EF6.NET47.Tests.Models.DM.Invoice
{
    public class Invoice
    {
        public Invoice()
        {
            BatchInvoices = new HashSet<BatchInvoiceItem>();
            Journals = new HashSet<InvoiceItem>();
        }
        public Guid Id { get; set; }
        public decimal Net { get; set; }
        public decimal Gross { get; set; }
        public decimal Tax { get; private set; }
        public ICollection<BatchInvoiceItem> BatchInvoices { get; set; }
        public ICollection<InvoiceItem> Journals { get; set; }
    }

    public class Journal
    {
        public Journal()
        {
            Invoices= new HashSet<InvoiceItem>();
        }

        public Guid Id { get; set; }
        public ICollection<InvoiceItem> Invoices { get; set; }
    }

    public class BatchInvoice
    {
        public BatchInvoice()
        {
            Invoices = new HashSet<BatchInvoiceItem>();
        }
        public Guid Id { get; set; }
        public ICollection<BatchInvoiceItem> Invoices { get; set; }
    }

    public class BatchInvoiceItem
    {
        public Guid Id { get; set; }
        public Guid BatchInvoiceId { get; set; }
        public BatchInvoice BatchInvoice { get; set; }
        public Guid InvoiceId { get; set; }
        public Invoice Invoice { get; set; }
    }

    public class InvoiceItem
    {
        public int Id { get; set; }
        public Guid JournalId { get; set; }
        public Journal Journal { get; set; }
        public Guid InvoiceId { get; set; }
        public Invoice Invoice { get; set; }
    }
}
