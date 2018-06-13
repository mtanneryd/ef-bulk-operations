using System;
using System.Globalization;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.EF6.Model;
using Tanneryd.BulkOperations.EF6.Tests.DM.Numbers;
using Tanneryd.BulkOperations.EF6.Tests.EF;

namespace Tanneryd.BulkOperations.EF6.Tests
{
    [TestClass]
    public class BulkSelectTests : BulkOperationTestBase
    {
        [TestInitialize]
        public void Initialize()
        {
            InitializeNumberContext();
        }

        [TestCleanup]
        public void CleanUp()
        {
            CleanupNumberContext();
        }

        [TestMethod]
        public void ExistingEntitiesShouldBeSelectedOnSingleKeyUsingSimpleType()
        {
            using (var db = new NumberContext())
            {
                var now = DateTime.Now;

                var numbers = GenerateNumbers(1, 200, now).ToArray();
                db.BulkInsertAll(new BulkInsertRequest<Number>
                {
                    Entities = numbers,
                    Recursive = true
                });

                var nums = GenerateNumbers(50, 100, now).ToList();
                var existingNumbers = db.BulkSelect<Number, Number>(new BulkSelectRequest<Number>
                {
                    Items = nums.ToArray(),
                    KeyPropertyMappings = new[]
                    {
                        new KeyPropertyMapping
                        {
                            ItemPropertyName = "Value",
                            EntityPropertyName = "Value"
                        },
                    }
                });

                var expectedNumbers = numbers.Skip(49).Take(100).ToArray();
                for (int i = 0; i < 100; i++)
                {
                    Assert.AreEqual(expectedNumbers[i].Id, existingNumbers[i].Id);
                    Assert.AreEqual(expectedNumbers[i].ParityId, existingNumbers[i].ParityId);
                    Assert.AreEqual(expectedNumbers[i].UpdatedAt.ToString(CultureInfo.InvariantCulture), existingNumbers[i].UpdatedAt.ToString(CultureInfo.InvariantCulture));
                    Assert.AreEqual(expectedNumbers[i].UpdatedBy, existingNumbers[i].UpdatedBy);
                    Assert.AreEqual(expectedNumbers[i].Value, existingNumbers[i].Value);
                }
            }
        }

        [TestMethod]
        public void ExistingEntitiesShouldBeSelectedOnSingleKey()
        {
            using (var db = new NumberContext())
            {
                var now = DateTime.Now;

                var numbers = GenerateNumbers(1, 200, now).ToArray();
                db.BulkInsertAll(new BulkInsertRequest<Number>
                {
                    Entities = numbers,
                    Recursive = true
                });
                foreach (var number in numbers)
                {
                    Console.WriteLine($"{number.Id};{number.Value}");
                }

                var nums = GenerateNumbers(50, 100, now)
                    .Select(n => new Num { Val = n.Value })
                    .ToList();
                var existingNumbers = db.BulkSelect<Num, Number>(new BulkSelectRequest<Num>
                {
                    Items = nums,
                    KeyPropertyMappings = new[]
                    {
                        new KeyPropertyMapping
                        {
                            ItemPropertyName = "Val",
                            EntityPropertyName = "Value"
                        },
                    }
                });

                //var expectedNumbers = numbers.Skip(49).Take(100).ToArray();
                //for (int i = 0; i < 100; i++)
                //{
                //    Assert.AreEqual(expectedNumbers[i].Id, existingNumbers[i].Id);
                //    Assert.AreEqual(expectedNumbers[i].ParityId, existingNumbers[i].ParityId);
                //    Assert.AreEqual(expectedNumbers[i].UpdatedAt.ToString(CultureInfo.InvariantCulture), existingNumbers[i].UpdatedAt.ToString(CultureInfo.InvariantCulture));
                //    Assert.AreEqual(expectedNumbers[i].UpdatedBy, existingNumbers[i].UpdatedBy);
                //    Assert.AreEqual(expectedNumbers[i].Value, existingNumbers[i].Value);
                //}
            }
        }

    }
}