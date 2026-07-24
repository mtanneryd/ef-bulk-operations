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
    }
}
