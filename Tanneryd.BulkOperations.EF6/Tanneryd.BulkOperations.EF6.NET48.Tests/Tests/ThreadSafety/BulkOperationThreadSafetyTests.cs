/*
* Copyright ©  2017-2025 Tånneryd IT AB
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.EF6;
using Tanneryd.BulkOperations.EF6.Model;
using Tanneryd.BulkOperations.EF6.NET48.Tests.Models.DM.Miscellaneous;
using Tanneryd.BulkOperations.EF6.NET48.Tests.Models.DM.People;
using Tanneryd.BulkOperations.EF6.NET48.Tests.Models.DM.Prices;
using Tanneryd.BulkOperations.EF6.NET48.Tests.Models.EF;

namespace Tanneryd.BulkOperations.EF6.NET48.Tests.Tests.ThreadSafety
{
    [TestClass]
    public class BulkOperationThreadSafetyTests : BulkOperationTestBase
    {
        private const int ThreadCount = 16;
        private const int PerThread = 25;
        private const int MaxTransientAttempts = 5;

        [TestInitialize]
        public void Initialize()
        {
            InitializeUnitTestContext();
        }

        [TestCleanup]
        public void CleanUp()
        {
            CleanupUnitTestContext();
        }

        [TestMethod]
        public void ConcurrentMappingExtractorAccessShouldSucceed()
        {
            Person[] seededPeople;
            using (var db = new UnitTestContext())
            {
                seededPeople = Enumerable.Range(0, ThreadCount)
                    .Select(i => new Person
                    {
                        FirstName = $"Map{i:D4}",
                        LastName = "Extractor",
                        BirthDate = new DateTime(1970, 1, 1)
                    })
                    .ToArray();
                db.BulkInsertAll(new BulkInsertRequest<Person>
                {
                    Entities = seededPeople,
                    EnableRecursiveInsert = EnableRecursiveInsert.NoButRetrieveGeneratedPrimaryKeys,
                    AllowNotNullSelfReferences = AllowNotNullSelfReferences.No
                });
            }
            var errors = new ConcurrentQueue<Exception>();
            var selectedCount = 0;
            using (var barrier = new Barrier(ThreadCount))
            {
                Parallel.For(0, ThreadCount, threadIndex =>
                {
                    try
                    {
                        barrier.SignalAndWait();
                        using (var db = new UnitTestContext())
                        {
                            var probe = new Person { Id = seededPeople[threadIndex].Id };
                            var existing = db.BulkSelectExisting<Person, Person>(new BulkSelectRequest<Person>
                            {
                                Items = new[] { probe },
                                KeyPropertyMappings = new[]
                                {
                                    new KeyPropertyMapping
                                    {
                                        ItemPropertyName = "Id",
                                        EntityPropertyName = "Id"
                                    }
                                }
                            });
                            Interlocked.Add(ref selectedCount, existing.Count);
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Enqueue(ex);
                    }
                });
            }
            AssertNoErrors(errors);
            Assert.AreEqual(ThreadCount, selectedCount);
        }

        [TestMethod]
        public void ParallelBulkInsertFromSeparateContextsShouldSucceed()
        {
            var errors = new ConcurrentQueue<Exception>();
            using (var barrier = new Barrier(ThreadCount))
            {
                Parallel.For(0, ThreadCount, threadIndex =>
                {
                    try
                    {
                        barrier.SignalAndWait();
                        RunWithTransientSqlRetry(() =>
                        {
                            using (var db = new UnitTestContext())
                            {
                                db.BulkInsertAll(new BulkInsertRequest<Person>
                                {
                                    Entities = CreatePeople(threadIndex, PerThread),
                                    EnableRecursiveInsert = EnableRecursiveInsert.NoAndIgnoreGeneratedPrimaryKeys,
                                    AllowNotNullSelfReferences = AllowNotNullSelfReferences.No
                                });
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        errors.Enqueue(ex);
                    }
                });
            }
            AssertNoErrors(errors);
            using (var db = new UnitTestContext())
            {
                Assert.AreEqual(ThreadCount * PerThread, db.People.Count());
            }
        }

        [TestMethod]
        public void ParallelBulkInsertWithIdentityPrimaryKeyShouldSucceed()
        {
            var errors = new ConcurrentQueue<Exception>();
            var generatedIds = new ConcurrentBag<int>();
            using (var barrier = new Barrier(ThreadCount))
            {
                Parallel.For(0, ThreadCount, threadIndex =>
                {
                    try
                    {
                        barrier.SignalAndWait();
                        var entities = new List<EmptyTable>(PerThread);
                        for (var i = 0; i < PerThread; i++)
                            entities.Add(new EmptyTable());
                        RunWithTransientSqlRetry(() =>
                        {
                            using (var db = new UnitTestContext())
                            {
                                db.BulkInsertAll(new BulkInsertRequest<EmptyTable>
                                {
                                    Entities = entities,
                                    EnableRecursiveInsert = EnableRecursiveInsert.NoButRetrieveGeneratedPrimaryKeys,
                                    AllowNotNullSelfReferences = AllowNotNullSelfReferences.No
                                });
                            }
                        });
                        foreach (var entity in entities)
                            generatedIds.Add(entity.Id);
                    }
                    catch (Exception ex)
                    {
                        errors.Enqueue(ex);
                    }
                });
            }
            AssertNoErrors(errors);
            Assert.AreEqual(ThreadCount * PerThread, generatedIds.Count);
            Assert.AreEqual(ThreadCount * PerThread, generatedIds.Distinct().Count());
            using (var db = new UnitTestContext())
            {
                Assert.AreEqual(ThreadCount * PerThread, db.EmptyTables.Count());
            }
        }

        [TestMethod]
        public void ParallelMixedBulkOperationsShouldSucceed()
        {
            var seedDate = new DateTime(2024, 6, 1);
            Person[] seededPeople;
            Price[] seededPrices;
            using (var db = new UnitTestContext())
            {
                seededPeople = Enumerable.Range(0, ThreadCount)
                    .Select(i => new Person
                    {
                        FirstName = $"Seed{i:D4}",
                        LastName = "ThreadSafety",
                        BirthDate = new DateTime(1970, 1, 1)
                    })
                    .ToArray();
                db.BulkInsertAll(new BulkInsertRequest<Person>
                {
                    Entities = seededPeople,
                    EnableRecursiveInsert = EnableRecursiveInsert.NoButRetrieveGeneratedPrimaryKeys,
                    AllowNotNullSelfReferences = AllowNotNullSelfReferences.No
                });
                seededPrices = Enumerable.Range(0, ThreadCount)
                    .Select(i => new Price
                    {
                        Date = seedDate,
                        Name = $"Seed{i:D4}",
                        Value = i
                    })
                    .ToArray();
                db.Prices.AddRange(seededPrices);
                db.SaveChanges();
            }
            var errors = new ConcurrentQueue<Exception>();
            var selectedCount = 0;
            using (var barrier = new Barrier(ThreadCount))
            {
                Parallel.For(0, ThreadCount, threadIndex =>
                {
                    try
                    {
                        barrier.SignalAndWait();
                        using (var db = new UnitTestContext())
                        {
                            switch (threadIndex % 3)
                            {
                                case 0:
                                    RunWithTransientSqlRetry(() =>
                                    {
                                        db.BulkInsertAll(new BulkInsertRequest<Person>
                                        {
                                            Entities = CreatePeople(1000 + threadIndex, PerThread),
                                            EnableRecursiveInsert = EnableRecursiveInsert.NoAndIgnoreGeneratedPrimaryKeys,
                                            AllowNotNullSelfReferences = AllowNotNullSelfReferences.No
                                        });
                                    });
                                    break;
                                case 1:
                                    var probe = new Person { Id = seededPeople[threadIndex].Id };
                                    var existing = db.BulkSelectExisting<Person, Person>(new BulkSelectRequest<Person>
                                    {
                                        Items = new[] { probe },
                                        KeyPropertyMappings = new[]
                                        {
                                            new KeyPropertyMapping
                                            {
                                                ItemPropertyName = "Id",
                                                EntityPropertyName = "Id"
                                            }
                                        }
                                    });
                                    Interlocked.Add(ref selectedCount, existing.Count);
                                    break;
                                default:
                                    RunWithTransientSqlRetry(() =>
                                    {
                                        db.BulkUpdateAll(new BulkUpdateRequest
                                        {
                                            Entities = new[]
                                            {
                                                new Price
                                                {
                                                    Date = seedDate,
                                                    Name = seededPrices[threadIndex].Name,
                                                    Value = threadIndex + 1000m
                                                }
                                            },
                                            KeyPropertyNames = new[] { "Date", "Name" },
                                            UpdatedPropertyNames = new[] { "Value" }
                                        });
                                    });
                                    break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Enqueue(ex);
                    }
                });
            }
            AssertNoErrors(errors);
            var expectedSelects = Enumerable.Range(0, ThreadCount).Count(i => i % 3 == 1);
            Assert.AreEqual(expectedSelects, selectedCount);
            using (var db = new UnitTestContext())
            {
                var expectedInserts = Enumerable.Range(0, ThreadCount).Count(i => i % 3 == 0);
                Assert.AreEqual(ThreadCount + expectedInserts * PerThread, db.People.Count());
                var updateThreads = Enumerable.Range(0, ThreadCount).Where(i => i % 3 == 2).ToArray();
                Assert.AreEqual(1000m + updateThreads.Max(), db.Prices.Max(p => p.Value));
            }
        }

        private static List<Person> CreatePeople(int threadIndex, int count)
        {
            var people = new List<Person>(count);
            for (var i = 0; i < count; i++)
            {
                people.Add(new Person
                {
                    FirstName = $"T{threadIndex:D4}",
                    LastName = $"N{i:D4}",
                    BirthDate = new DateTime(1980, 1, 1)
                });
            }
            return people;
        }

        private static void RunWithTransientSqlRetry(Action action)
        {
            for (var attempt = 1; attempt <= MaxTransientAttempts; attempt++)
            {
                try
                {
                    action();
                    return;
                }
                catch (Exception ex) when (IsTransientSqlError(ex) && attempt < MaxTransientAttempts)
                {
                    Thread.Sleep(25 * attempt);
                }
            }
        }

        private static bool IsTransientSqlError(Exception ex)
        {
            var sqlEx = ex as SqlException ?? ex.InnerException as SqlException;
            return sqlEx != null && (sqlEx.Number == 1205 || sqlEx.Number == 4891);
        }

        private static void AssertNoErrors(ConcurrentQueue<Exception> errors)
        {
            if (errors.IsEmpty)
                return;
            Assert.Fail(string.Join(Environment.NewLine, errors.Select(e => e.ToString())));
        }
    }
}
