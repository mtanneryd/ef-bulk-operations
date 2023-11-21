using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tanneryd.BulkOperations.EFCore.SQLite.Tests.UnitTests.Insert
{
    [TestClass]
    public class BulkInsertGitIssuesTests : BulkOperationTestBase
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
        public void BulkInsertIntoTableWithoutNavPropertiesShouldWork()
        {
            using (var db = Factory.CreateDbContext())
            {
                var entities = new List<Price>
                {
                    new Price() {Date = new DateTime(2019, 1, 1), Name = "ERICB", Value = 80},
                    new Price() {Date = new DateTime(2019, 1, 2), Name = "ERICB", Value = 81},
                    new Price() {Date = new DateTime(2019, 1, 3), Name = "ERICB", Value = 82},
                    new Price() {Date = new DateTime(2019, 1, 4), Name = "ERICB", Value = 0},
                    new Price() {Date = new DateTime(2019, 1, 5), Name = "ERICB", Value = 86}
                };


                var request = new BulkInsertRequest<Price>
                {
                    AllowNotNullSelfReferences = AllowNotNullSelfReferences.No,
                    EnableRecursiveInsert = EnableRecursiveInsert.NoButRetrieveGeneratedPrimaryKeys,
                    Entities = entities.ToArray()
                };

                db.BulkInsertAll(request);
            }
        }
    }
}