using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.EF6.Model;
using Tanneryd.BulkOperations.EF6.Tests.DM.People;
using Tanneryd.BulkOperations.EF6.Tests.EF;

namespace Tanneryd.BulkOperations.EF6.Tests.Tests.Delete
{
    [TestClass]
    public class BulkDeleteTests : BulkOperationTestBase
    {
        [TestInitialize]
        public void Initialize()
        {
            CleanupPeopleContext();
            InitializePeopleContext();
        }

        [TestCleanup]
        public void CleanUp()
        {
            CleanupPeopleContext();
        }

        [TestMethod]
        public void DeleteNotExistingEntities()
        {
            var p0 = new Person
            {
                FirstName = "Angelica",
                LastName = "Tånneryd",
                BirthDate = DateTime.Now
            };
            var p1 = new Person
            {
                FirstName = "Arvid",
                LastName = "Tånneryd",
                BirthDate = DateTime.Now,
                Mother = p0
            };
            var p2 = new Person
            {
                FirstName = "Inga-Lill",
                LastName = "Tånneryd",
                BirthDate = DateTime.Now
            };
            var p3 = new Person
            {
                FirstName = "Linus",
                LastName = "Tånneryd",
                BirthDate = DateTime.Now,
                Mother = p0
            };
            var p4 = new Person
            {
                FirstName = "Måns",
                LastName = "Tånneryd",
                BirthDate = DateTime.Now,
                Mother = p2
            };
            var p5 = new Person
            {
                FirstName = "Viktor",
                LastName = "Tånneryd",
                BirthDate = DateTime.Now,
                Mother = p0
            };

            using (var db = new PeopleContext())
            {
                db.People.AddRange(new[] {p0, p1, p2, p3, p4, p5});
                db.SaveChanges();

                var people = db.People.OrderBy(p=>p.FirstName).ToArray();
                Assert.AreEqual(6, people.Length);
                Assert.AreEqual(p0.Id, people[0].Id);
                Assert.AreEqual(p1.Id, people[1].Id);
                Assert.AreEqual(p2.Id, people[2].Id);
                Assert.AreEqual(p3.Id, people[3].Id);
                Assert.AreEqual(p4.Id, people[4].Id);
                Assert.AreEqual(p5.Id, people[5].Id);

                // Delete all children of p0 in the database 
                // that are not in the Items list.
                db.BulkDeleteNotExisting<Person, Person>(new BulkDeleteRequest<Person>(
                    new [] { new SqlCondition{ ColumnName = "MotherId", ColumnValue = p0.Id} }, 
                    new [] { "FirstName", "LastName"})
                {
                    Items = new[] { p1, p3 }.ToList()
                });

                people = db.People.OrderBy(p => p.FirstName).ToArray();
                Assert.AreEqual(5, people.Length);
                Assert.AreEqual(p0.Id, people[0].Id);
                Assert.AreEqual(p1.Id, people[1].Id);
                Assert.AreEqual(p2.Id, people[2].Id);
                Assert.AreEqual(p3.Id, people[3].Id);
                Assert.AreEqual(p4.Id, people[4].Id);
            }
        }
    }
}