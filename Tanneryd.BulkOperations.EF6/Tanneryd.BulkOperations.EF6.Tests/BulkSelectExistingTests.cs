using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.EF6.Model;
using Tanneryd.BulkOperations.EF6.Tests.DM.Numbers;
using Tanneryd.BulkOperations.EF6.Tests.EF;

namespace Tanneryd.BulkOperations.EF6.Tests
{
    [TestClass]
    public class BulkSelectExistingTests : BulkOperationTestBase
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
        public void PrimitiveTypeValuesMatchingExistingEntitiesShouldBeSelected()
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


                var nums = GenerateNumbers(50, 100, now)
                    .Select(n =>  n.Value )
                    .ToList();
                var existingNumbers = db.BulkSelectExisting<long, Number>(new BulkSelectRequest<long>
                {
                    Items = nums,
                    KeyPropertyMappings = new[]
                    {
                        new KeyPropertyMapping
                        {
                            ItemPropertyName = null,
                            EntityPropertyName = "Value"
                        },
                    }
                });

                for (int i = 0; i < 100; i++)
                {
                    Assert.AreEqual(nums[i], existingNumbers[i]);
                }
            }
        }

        [TestMethod]
        public void EntitiesOfDifferentTypeMatchingExistingEntitiesShouldBeSelected()
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


                var nums = GenerateNumbers(50, 100, now)
                    .Select(n => new Num { Val = n.Value })
                    .ToList();
                var existingNumbers = db.BulkSelectExisting<Num, Number>(new BulkSelectRequest<Num>
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

                for (int i = 0; i < 100; i++)
                {
                    Assert.AreSame(nums[i], existingNumbers[i]);
                }
            }
        }

        [TestMethod]
        public void EntitiesOfSameTypeMatchingExistingEntitiesShouldBeSelected()
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


                numbers = GenerateNumbers(50, 100, now).ToArray();
                var existingNumbers = db.BulkSelectExisting<Number, Number>(new BulkSelectRequest<Number>
                {
                    Items = numbers,
                    KeyPropertyMappings = new[]
                    {
                        new KeyPropertyMapping
                        {
                            ItemPropertyName = "Value",
                            EntityPropertyName = "Value"
                        },
                    }
                });

                for (int i = 0; i < 100; i++)
                {
                    Assert.AreSame(numbers[i], existingNumbers[i]);
                }
            }
        }

        [TestMethod]
        public void ExistingEntitiesShouldBeSelectedUsingRuntimeTypes()
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


                numbers = GenerateNumbers(50, 100, now).ToArray();

                var request = typeof(BulkSelectRequest<>).MakeGenericType(typeof(Number));
                var r = Activator.CreateInstance(request, new [] { "Value" }, numbers.ToList(), null);

                Type ex = typeof(DbContextExtensions);
                MethodInfo mi = ex.GetMethod("BulkSelectExisting");
                MethodInfo miGeneric = mi.MakeGenericMethod(new[] {typeof(Number),typeof(Number)});
                object[] args = {db, r};
                var existingNumbers = (List<Number>) miGeneric.Invoke(null, args);

                for (int i = 0; i < 100; i++)
                {
                    Assert.AreSame(numbers[i], existingNumbers[i]);
                }
            }
        }
    }
}