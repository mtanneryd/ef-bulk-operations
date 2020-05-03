using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.EF6.Model;
using Tanneryd.BulkOperations.EF6.NET47.ModelFirst.Tests.Models.EF;


namespace Tanneryd.BulkOperations.EF6.NET47.ModelFirst.Tests.Tests.Insert
{
    [TestClass]
    public class BulkInsertComputedColumns : BulkOperationTestBase
    {
        [TestInitialize]
        public void Initialize()
        {
            InitializeUnitTestContext();
            CleanUp();
        }

        [TestCleanup]
        public void CleanUp()
        {
            CleanupUnitTestContext();
        }

        [TestMethod]
        public void InsertEntityWithComputedColumnInTableWithIdentityPrimaryKeyShouldWork()
        {
            using (var db = new UnitTestsContext())
            {
                var instructor = new Instructor
                {
                    FirstName = "Måns",
                    LastName = "Tånneryd",
                    HireDate = DateTime.Now.Date,
                };

                db.BulkInsertAll(new[] {instructor});
            }
        }

        [TestMethod]
        public void InsertEntityWithComputedColumnInTableWithUserGeneratedPrimaryKeyShouldWork()
        {
            using (var db = new UnitTestsContext())
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
