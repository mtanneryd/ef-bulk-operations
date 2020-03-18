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
using System.Data.Entity;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.EF6.NetStd;
using Tanneryd.BulkOperations.EF6.NetStd.Model;
using Tanneryd.BulkOperations.EF6.Tests.DM.Invoice;
using Tanneryd.BulkOperations.EF6.Tests.EF;

namespace Tanneryd.BulkOperations.EF6.Tests.Tests.Insert
{
    [TestClass]
    public class BulkInsertInvoice : BulkOperationTestBase
    {
        [TestInitialize]
        public void Initialize()
        {
            InitializeInvoiceContext();
            CleanUp(); // make sure we start from scratch, previous tests might have been aborted before cleanup

        }

        [TestCleanup]
        public void CleanUp()
        {
            CleanupInvoiceContext();
        }

        [TestMethod]
        public void InsertBatchInvoiceWithInvoiceForEachExistingJournal()
        {
            using (var db = new InvoiceContext())
            {
                for(int i = 0; i < 10; i++)
                {
                    db.Journals.Add(new Journal() { Id = Guid.NewGuid() });
                }
                db.SaveChanges();

                var batchInvoice = new BatchInvoice { Id = Guid.NewGuid() };

                foreach(var journal in db.Journals.ToList())
                {
                    var invoice = new Invoice() { Id = Guid.NewGuid(), Gross = 10, Net = 3 };
                    invoice.Journals.Add(new InvoiceItem() { JournalId = journal.Id });
                    batchInvoice.Invoices.Add(new BatchInvoiceItem() { Id = Guid.NewGuid(), Invoice = invoice });
                }

                var req = new BulkInsertRequest<BatchInvoice>
                {
                    Entities = new[] { batchInvoice }.ToList(),
                    AllowNotNullSelfReferences = AllowNotNullSelfReferences.No,
                    SortUsingClusteredIndex = true,
                    EnableRecursiveInsert = EnableRecursiveInsert.Yes
                };
                var response = db.BulkInsertAll(req);

                Assert.AreEqual(1, db.BatchInvoices.ToArray().Count());
                Assert.AreEqual(10, db.BatchInvoiceItems.ToArray().Count());
                Assert.AreEqual(10, db.Invoices.ToArray().Count());
                Assert.AreEqual(10, db.InvoiceItems.ToArray().Count());
            }
        }       
    }
}