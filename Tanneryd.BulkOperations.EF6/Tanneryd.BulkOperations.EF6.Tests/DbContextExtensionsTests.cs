using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.EF6.Tests.DM.School;
using Tanneryd.BulkOperations.EF6.Tests.EF;

namespace Tanneryd.BulkOperations.EF6.Tests
{
    [TestClass]
    public class DbContextExtensionsTests
    {
        [TestMethod]
        public void SchoolTableNames()
        {
            using (var ctx = new SchoolContext())
            {
                var tableName = DbContextExtensions.GetTableName(ctx, typeof(Course));
                Assert.AreEqual("dbo", tableName.Schema);
                Assert.AreEqual("Course", tableName.Name);

                tableName = DbContextExtensions.GetTableName(ctx, typeof(Department));
                Assert.AreEqual("dbo", tableName.Schema);
                Assert.AreEqual("Department", tableName.Name);

                tableName = DbContextExtensions.GetTableName(ctx, typeof(Instructor));
                Assert.AreEqual("dbo", tableName.Schema);
                Assert.AreEqual("Instructor", tableName.Name);

                tableName = DbContextExtensions.GetTableName(ctx, typeof(OfficeAssignment));
                Assert.AreEqual("dbo", tableName.Schema);
                Assert.AreEqual("OfficeAssignment", tableName.Name);
            }
        }
        
        [TestMethod]
        public void ReportTableNames()
        {
            using (var ctx = new ReportContext())
            {
                var tableName = DbContextExtensions.GetTableName(ctx, typeof(Period));
                Assert.AreEqual("Some.Complex_Schema Name", tableName.Schema);
                Assert.AreEqual("Period", tableName.Name);

                tableName = DbContextExtensions.GetTableName(ctx, typeof(SummaryReportFROMTableASExtent));
                Assert.AreEqual(nameof(SummaryReportFROMTableASExtent), tableName.Name);

                tableName = DbContextExtensions.GetTableName(ctx, typeof(DetailReportWithFROM));
                Assert.AreEqual("In # Some.Complex_Schema @Name", tableName.Schema);
                Assert.AreEqual("SELECT WORSE FROM NAMES AS Extent1", tableName.Name);
            }
        }
    }
}