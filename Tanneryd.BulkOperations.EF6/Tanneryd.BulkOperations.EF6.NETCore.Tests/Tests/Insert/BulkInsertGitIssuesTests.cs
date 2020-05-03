using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.EF6.Model;
using Tanneryd.BulkOperations.EF6.NETCore.Tests.Models.DM.Logs;
using Tanneryd.BulkOperations.EF6.NETCore.Tests.Models.DM.Prices;
using Tanneryd.BulkOperations.EF6.NETCore.Tests.Models.EF;

namespace Tanneryd.BulkOperations.EF6.NETCore.Tests.Tests.Insert
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
            using (var db = new UnitTestContext())
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
        
        [TestMethod]
        public void BulkInsertingEntitiesUsingTablePerHierarchyShouldWorkWhenIgnoringGeneratedPrimaryKeys()
        {
            using (var db = new UnitTestContext())
            {
                var warnings = new[]
                {
                    new LogWarning
                    {
                        Timestamp = DateTime.Now,
                        Message = "my warning message 1",
                        Recommendation = "my recommendation 1",
                        Id = 0,
                    },
                    new LogWarning
                    {
                        Timestamp = DateTime.Now,
                        Message = "my warning message 2",
                        Id = 0,
                    },
                    new LogWarning
                    {
                        Timestamp = DateTime.Now,
                        Message = "my warning message 3",
                        Id = 0,
                    }
                };

                var request1 = new BulkInsertRequest<LogWarning>
                {
                    AllowNotNullSelfReferences = AllowNotNullSelfReferences.No,
                    EnableRecursiveInsert = EnableRecursiveInsert.NoAndIgnoreGeneratedPrimaryKeys,
                    Entities = warnings
                };

                db.BulkInsertAll(request1);

                var dbWarnings = db.LogWarnings
                    .OrderBy(item => item.Message)
                    .ToArray();
                Assert.AreEqual(3, dbWarnings.Length);
                Assert.AreEqual("my warning message 1", dbWarnings[0].Message);
                Assert.AreEqual("my warning message 2", dbWarnings[1].Message);
                Assert.AreEqual("my warning message 3", dbWarnings[2].Message);

                var errors = new[]
                {
                    new LogError
                    {
                        Timestamp = DateTime.Now,
                        Message = "my error message 1",
                        Severity = 1,
                        Id = 0,
                    },
                    new LogError
                    {
                        Timestamp = DateTime.Now,
                        Message = "my error message 2",
                        Severity = 7,
                        Id = 0,
                    },
                    new LogError
                    {
                        Timestamp = DateTime.Now,
                        Message = "my error message 3",
                        Id = 0,
                    }
                };

                var request2 = new BulkInsertRequest<LogError>
                {
                    AllowNotNullSelfReferences = AllowNotNullSelfReferences.No,
                    EnableRecursiveInsert = EnableRecursiveInsert.NoAndIgnoreGeneratedPrimaryKeys,
                    Entities = errors
                };
                db.BulkInsertAll(request2);

                var dbErrors = db.LogErrors
                    .OrderBy(item => item.Message)
                    .ToArray();
                Assert.AreEqual(3, dbErrors.Length);
                Assert.AreEqual("my error message 1", dbErrors[0].Message);
                Assert.AreEqual("my error message 2", dbErrors[1].Message);
                Assert.AreEqual("my error message 3", dbErrors[2].Message);
            }
        }

        [TestMethod]
        public void BulkInsertingEntitiesUsingTablePerHierarchyShouldWorkWhenNotIgnoringGeneratedPrimaryKeys()
        {
            using (var db = new UnitTestContext())
            {
                var warnings = new[]
                {
                    new LogWarning
                    {
                        Timestamp = DateTime.Now,
                        Message = "my warning message 1",
                        Recommendation = "my recommendation 1",
                        Id = 0,
                    },
                    new LogWarning
                    {
                        Timestamp = DateTime.Now,
                        Message = "my warning message 2",
                        Id = 0,
                    },
                    new LogWarning
                    {
                        Timestamp = DateTime.Now,
                        Message = "my warning message 3",
                        Id = 0,
                    }
                };

                var request1 = new BulkInsertRequest<LogWarning>
                {
                    AllowNotNullSelfReferences = AllowNotNullSelfReferences.No,
                    EnableRecursiveInsert = EnableRecursiveInsert.NoButRetrieveGeneratedPrimaryKeys,
                    Entities = warnings
                };

                db.BulkInsertAll(request1);

                var dbWarnings = db.LogWarnings
                    .OrderBy(item => item.Message)
                    .ToArray();
                Assert.AreEqual(3, dbWarnings.Length);
                Assert.AreEqual("my warning message 1", dbWarnings[0].Message);
                Assert.AreEqual("my warning message 2", dbWarnings[1].Message);
                Assert.AreEqual("my warning message 3", dbWarnings[2].Message);

                var errors = new[]
                {
                    new LogError
                    {
                        Timestamp = DateTime.Now,
                        Message = "my error message 1",
                        Severity = 1,
                        Id = 0,
                    },
                    new LogError
                    {
                        Timestamp = DateTime.Now,
                        Message = "my error message 2",
                        Severity = 7,
                        Id = 0,
                    },
                    new LogError
                    {
                        Timestamp = DateTime.Now,
                        Message = "my error message 3",
                        Id = 0,
                    }
                };

                var request2 = new BulkInsertRequest<LogError>
                {
                    AllowNotNullSelfReferences = AllowNotNullSelfReferences.No,
                    EnableRecursiveInsert = EnableRecursiveInsert.NoButRetrieveGeneratedPrimaryKeys,
                    Entities = errors
                };
                db.BulkInsertAll(request2);

                var dbErrors = db.LogErrors
                    .OrderBy(item => item.Message)
                    .ToArray();
                Assert.AreEqual(3, dbErrors.Length);
                Assert.AreEqual("my error message 1", dbErrors[0].Message);
                Assert.AreEqual("my error message 2", dbErrors[1].Message);
                Assert.AreEqual("my error message 3", dbErrors[2].Message);
            }
        }

    }
}