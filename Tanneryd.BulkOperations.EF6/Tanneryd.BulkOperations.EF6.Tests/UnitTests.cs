using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations;
using Tanneryd.BulkOperations.EF6;
using Tanneryd.DM;
using Tanneryd.EF;

namespace Tanneryd.BulkInsert.Tests
{
    [TestClass]
    public class UnitTests
    {
        [TestInitialize]
        public void Initialize()
        {
            Database.SetInitializer(new CreateDatabaseIfNotExists<NumberContext>());
        }

        [TestCleanup]
        public void Cleanup()
        {
            var db = new NumberContext();
            db.Database.Delete();
        }

        [TestMethod]
        public void ComplexTypesShouldBeInserted()
        {
            using (var db = new NumberContext())
            {
                // The level entities are used to test EF complex types.
                var expectedLevels = new[]
                {
                    new Level1
                    {
                        Level2 = new Level2
                        {
                            Level2Name = "L2",
                            Level3 = new Level3
                            {
                                Level3Name = "L3",
                                Updated = new DateTime(2018,1,1)
                            }
                        }
                    }
                };
                db.BulkInsertAll(expectedLevels);

                var actualLevels = db.Levels.ToArray();
                Assert.AreEqual(expectedLevels.Length, actualLevels.Length);
                Assert.AreEqual(expectedLevels[0].Id, actualLevels[0].Id);
                Assert.AreEqual(expectedLevels[0].Level2.Level2Name, actualLevels[0].Level2.Level2Name);
                Assert.AreEqual(expectedLevels[0].Level2.Level3.Level3Name, actualLevels[0].Level2.Level3.Level3Name);
                Assert.AreEqual(expectedLevels[0].Level2.Level3.Updated.Ticks, actualLevels[0].Level2.Level3.Updated.Ticks);
            }
        }

        [TestMethod]
        public void PrimaryKeyColumnMappedToPropertyWithDifferentNameShouldBeAllowed()
        {
            using (var db = new NumberContext())
            {
                var now = DateTime.Now;

                // The Parity table is defined with a pk column named Key but
                // it is mapped to the property Id. There was a user reporting that
                // this did not work properly so we want to test it.
                var parities = new[]
                {
                    new Parity {Name = "Even", UpdatedAt = now, UpdatedBy = "Måns"},
                    new Parity {Name = "Odd", UpdatedAt = now, UpdatedBy = "Måns"},
                };
                db.BulkInsertAll(parities);

                Assert.AreEqual(1, parities[0].Id);
                Assert.AreEqual(2, parities[1].Id);
            }
        }

        /// <summary>
        /// We use parity to test the one-to-many relationship. Each number
        /// has a foreign key relation to one of the two parity entries.
        /// </summary>
        [TestMethod]
        public void OneToMany()
        {
            using (var db = new NumberContext())
            {
                var now = DateTime.Now;

                var parities = new[]
                {
                    new Parity {Name = "Even", UpdatedAt = now, UpdatedBy = "Måns"},
                    new Parity {Name = "Odd", UpdatedAt = now, UpdatedBy = "Måns"},
                };
                db.BulkInsertAll(parities);

                var numbers = GenerateNumbers(100, parities[0], parities[1], now).ToArray();
                db.BulkInsertAll(numbers);

                Assert.AreEqual(100, db.Numbers.Count());

                var dbNumbers = db.Numbers.Include(n => n.Parity).ToArray();
                foreach (var number in dbNumbers.Where(n => n.Value % 2 == 0))
                {
                    Assert.AreEqual("Even", number.Parity.Name);
                    Assert.AreEqual(now.ToString("yyyyMMddHHmmss"), number.Updated.UpdatedAt.ToString("yyyyMMddHHmmss"));
                }

                foreach (var number in dbNumbers.Where(n => n.Value % 2 != 0))
                {
                    Assert.AreEqual("Odd", number.Parity.Name);
                    Assert.AreEqual(now.ToString("yyyyMMddHHmmss"), number.Updated.UpdatedAt.ToString("yyyyMMddHHmmss"));
                }
            }
        }

        private static IEnumerable<Number> GenerateNumbers(int count, Parity even, Parity odd, DateTime now)
        {
            for (int i = 1; i <= count; i++)
            {
                var n = new Number
                {
                    Value = i,
                    ParityId = i % 2 == 0 ? even.Id : odd.Id,
                    Updated = new Updated { UpdatedAt = now, UpdatedBy = "Måns" }
                };

                yield return n;
            }
        }

    }
}
