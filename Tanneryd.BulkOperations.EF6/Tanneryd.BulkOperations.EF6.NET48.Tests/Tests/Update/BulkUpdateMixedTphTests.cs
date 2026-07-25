/*
 * Copyright ©  2017-2026 Tånneryd IT AB
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
using System.Data.Entity;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.EF6.Model;
using Tanneryd.BulkOperations.EF6.NET48.Tests.Models.DM.Logs;
using Tanneryd.BulkOperations.EF6.NET48.Tests.Models.EF;

namespace Tanneryd.BulkOperations.EF6.NET48.Tests.Tests.Update
{
    /// <summary>
    /// Mixed TPH BulkUpdate batches must not key column mappings off entities[0].
    /// Derived types expose different columns (Recommendation vs Severity); using
    /// one type's mapping for the whole batch throws or writes the wrong SET list.
    /// </summary>
    [TestClass]
    public class BulkUpdateMixedTphTests : BulkOperationTestBase
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
        public void BulkUpdate_MixedTphBatch_ShouldUpdateEachConcreteType_WhenWarningComesFirst()
        {
            AssertMixedBatchUpdatesCorrectly(warningFirst: true);
        }

        [TestMethod]
        public void BulkUpdate_MixedTphBatch_ShouldUpdateEachConcreteType_WhenErrorComesFirst()
        {
            AssertMixedBatchUpdatesCorrectly(warningFirst: false);
        }

        private void AssertMixedBatchUpdatesCorrectly(bool warningFirst)
        {
            using (var db = new UnitTestContext())
            {
                db.BulkInsertAll(new BulkInsertRequest<LogWarning>
                {
                    Entities = new[]
                    {
                        new LogWarning
                        {
                            Message = "warning-before",
                            Recommendation = "rec-before",
                            Timestamp = new DateTime(2026, 1, 1),
                        },
                    },
                    EnableRecursiveInsert = EnableRecursiveInsert.NoAndIgnoreGeneratedPrimaryKeys,
                });
                db.BulkInsertAll(new BulkInsertRequest<LogError>
                {
                    Entities = new[]
                    {
                        new LogError
                        {
                            Message = "error-before",
                            Severity = 1,
                            Timestamp = new DateTime(2026, 1, 2),
                        },
                    },
                    EnableRecursiveInsert = EnableRecursiveInsert.NoAndIgnoreGeneratedPrimaryKeys,
                });

                var warning = db.LogWarnings.Single();
                var error = db.LogErrors.Single();
                warning.Message = "warning-after";
                warning.Recommendation = "rec-after";
                warning.Timestamp = new DateTime(2026, 2, 1);
                error.Message = "error-after";
                error.Severity = 9;
                error.Timestamp = new DateTime(2026, 2, 2);

                var batch = warningFirst
                    ? new LogItem[] { warning, error }
                    : new LogItem[] { error, warning };

                db.BulkUpdateAll(new BulkUpdateRequest
                {
                    Entities = batch,
                });
            }

            using (var db = new UnitTestContext())
            {
                var updatedWarning = db.LogWarnings.AsNoTracking().Single();
                var updatedError = db.LogErrors.AsNoTracking().Single();

                Assert.AreEqual("warning-after", updatedWarning.Message);
                Assert.AreEqual("rec-after", updatedWarning.Recommendation);
                Assert.AreEqual(new DateTime(2026, 2, 1), updatedWarning.Timestamp);
                Assert.AreEqual("error-after", updatedError.Message);
                Assert.AreEqual(9, updatedError.Severity);
                Assert.AreEqual(new DateTime(2026, 2, 2), updatedError.Timestamp);
            }
        }

        /// <summary>
        /// InsertIfNew on a TPH derived type must stage and INSERT the discriminator.
        /// Without it, SQL Server rejects the insert (NOT NULL LogType) or the row
        /// is not visible on the typed DbSet.
        /// </summary>
        [TestMethod]
        public void BulkUpdate_InsertIfNew_ShouldStampTphDiscriminator_ForLogWarning()
        {
            using (var db = new UnitTestContext())
            {
                db.BulkInsertAll(new BulkInsertRequest<LogWarning>
                {
                    Entities = new[]
                    {
                        new LogWarning
                        {
                            Message = "warning-before",
                            Recommendation = "rec-before",
                            Timestamp = new DateTime(2026, 1, 1),
                        },
                    },
                    EnableRecursiveInsert = EnableRecursiveInsert.NoAndIgnoreGeneratedPrimaryKeys,
                });

                var existing = db.LogWarnings.Single();
                existing.Message = "warning-after";

                db.BulkUpdateAll(new BulkUpdateRequest
                {
                    Entities = new object[]
                    {
                        existing,
                        new LogWarning
                        {
                            Message = "warning-new",
                            Recommendation = "rec-new",
                            Timestamp = new DateTime(2026, 3, 1),
                        },
                    },
                    UpdatedPropertyNames = new[] { nameof(LogWarning.Message) },
                    InsertIfNew = true,
                });
            }

            using (var db = new UnitTestContext())
            {
                var warnings = db.LogWarnings.AsNoTracking().OrderBy(w => w.Id).ToArray();
                Assert.AreEqual(2, warnings.Length,
                    "InsertIfNew must insert the new LogWarning with the correct TPH discriminator.");
                Assert.AreEqual("warning-after", warnings[0].Message);
                Assert.AreEqual("rec-before", warnings[0].Recommendation,
                    "Partial UpdatedPropertyNames must not overwrite columns omitted from the SET list.");
                Assert.AreEqual("warning-new", warnings[1].Message);
                Assert.AreEqual("rec-new", warnings[1].Recommendation);
                Assert.AreEqual(new DateTime(2026, 3, 1), warnings[1].Timestamp);
                Assert.AreEqual(0, db.LogErrors.Count());
            }
        }

        /// <summary>
        /// Same InsertIfNew discriminator coverage for the sibling TPH type.
        /// </summary>
        [TestMethod]
        public void BulkUpdate_InsertIfNew_ShouldStampTphDiscriminator_ForLogError()
        {
            using (var db = new UnitTestContext())
            {
                db.BulkInsertAll(new BulkInsertRequest<LogError>
                {
                    Entities = new[]
                    {
                        new LogError
                        {
                            Message = "error-before",
                            Severity = 1,
                            Timestamp = new DateTime(2026, 1, 2),
                        },
                    },
                    EnableRecursiveInsert = EnableRecursiveInsert.NoAndIgnoreGeneratedPrimaryKeys,
                });

                var existing = db.LogErrors.Single();
                existing.Message = "error-after";

                db.BulkUpdateAll(new BulkUpdateRequest
                {
                    Entities = new object[]
                    {
                        existing,
                        new LogError
                        {
                            Message = "error-new",
                            Severity = 7,
                            Timestamp = new DateTime(2026, 3, 2),
                        },
                    },
                    UpdatedPropertyNames = new[] { nameof(LogError.Message) },
                    InsertIfNew = true,
                });
            }

            using (var db = new UnitTestContext())
            {
                var errors = db.LogErrors.AsNoTracking().OrderBy(e => e.Id).ToArray();
                Assert.AreEqual(2, errors.Length,
                    "InsertIfNew must insert the new LogError with the correct TPH discriminator.");
                Assert.AreEqual("error-after", errors[0].Message);
                Assert.AreEqual(1, errors[0].Severity);
                Assert.AreEqual("error-new", errors[1].Message);
                Assert.AreEqual(7, errors[1].Severity);
                Assert.AreEqual(0, db.LogWarnings.Count());
            }
        }
    }
}
