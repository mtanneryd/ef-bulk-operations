using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.EF6.Model;
using Tanneryd.BulkOperations.EF6.Tests.DM.People;
using Tanneryd.BulkOperations.EF6.Tests.EF;

namespace Tanneryd.BulkOperations.EF6.Tests
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
            var p1 = new Person
            {
                FirstName = "Måns",
                LastName = "Tånneryd",
                BirthDate = DateTime.Now
            };
            var p2 = new Person
            {
                FirstName = "Angelica",
                LastName = "Tånneryd",
                BirthDate = DateTime.Now
            };
            var p3 = new Person
            {
                FirstName = "Linus",
                LastName = "Tånneryd"
                ,
                BirthDate = DateTime.Now
            };
            var p4 = new Person
            {
                FirstName = "Arvid",
                LastName = "Tånneryd",
                BirthDate = DateTime.Now
            };
            var p5 = new Person
            {
                FirstName = "Viktor",
                LastName = "Tånneryd",
                BirthDate = DateTime.Now
            };

            using (var db = new PeopleContext())
            {
                db.People.AddRange(new[] {p1, p2, p3, p4, p5});
                db.SaveChanges();

                var people = db.People.ToArray();
                Assert.AreEqual(5, people.Length);
                Assert.AreEqual(p1.Id, people[0].Id);
                Assert.AreEqual(p2.Id, people[1].Id);
                Assert.AreEqual(p3.Id, people[2].Id);
                Assert.AreEqual(p4.Id, people[3].Id);
                Assert.AreEqual(p5.Id, people[4].Id);

                db.BulkDeleteNotExisting<Person, Person>(new BulkDeleteRequest<Person>(new [] { "FirstName", "LastName"})
                {
                    Items = new[] { p1, p2, p3 }.ToList()
                });

                people = db.People.ToArray();
                Assert.AreEqual(3, people.Length);
                Assert.AreEqual(p1.Id, people[0].Id);
                Assert.AreEqual(p2.Id, people[1].Id);
                Assert.AreEqual(p3.Id, people[2].Id);
            }
        }
    }
}