/*
* Copyright ©  2017-2019 Tånneryd IT AB
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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.EF6.Model;
using Tanneryd.BulkOperations.EF6.NETCore.Tests.Models.DM.People;
using Tanneryd.BulkOperations.EF6.NETCore.Tests.Models.EF;

namespace Tanneryd.BulkOperations.EF6.NETCore.Tests.Tests.Delete
{
    [TestClass]
    public class BulkDeleteTests : BulkOperationTestBase
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

            using (var db = new UnitTestContext())
            {
                db.People.AddRange(new[] { p0, p1, p2, p3, p4, p5 });
                db.SaveChanges();

                var people = db.People.OrderBy(p => p.FirstName).ToArray();
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
                    new[] { new SqlCondition("MotherId", p0.Id) },
                    new[] { "FirstName", "LastName" })
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

        [TestMethod]
        public void DeleteNotExistingEntities2()
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
                EmployeeNumber = 0,
                Mother = p0
            };
            var p2 = new Person
            {
                FirstName = "Viktor",
                LastName = "Tånneryd",
                BirthDate = DateTime.Now,
                EmployeeNumber = 0,
                Mother = p0
            };

            using (var db = new UnitTestContext())
            {
                db.People.AddRange(new[] { p0, p1, p2 });
                db.SaveChanges();

                var people = db.People.OrderBy(p => p.FirstName).ToArray();
                Assert.AreEqual(3, people.Length);
                Assert.AreEqual(p0.Id, people[0].Id);
                Assert.AreEqual(p1.Id, people[1].Id);
                Assert.AreEqual(p2.Id, people[2].Id);

                // Delete all children of p0 in the database 
                // that are not in the Items list. Make sure
                // that we can differentiate between null and
                // column default values.
                // Setting the EmployeeNumber values to null
                // should make those two objects NOT match
                // with the records in the database and thus
                // only the mother record should be left after
                // this operation.
                p1.EmployeeNumber = null;
                p2.EmployeeNumber = null;
                db.BulkDeleteNotExisting<Person, Person>(new BulkDeleteRequest<Person>(
                    new[] { new SqlCondition("MotherId", p0.Id) },
                    new[] { "FirstName", "EmployeeNumber", "LastName" })
                {
                    Items = new[] { p1, p2 }.ToList()
                });

                people = db.People.OrderBy(p => p.FirstName).ToArray();
                Assert.AreEqual(1, people.Length);
                Assert.AreEqual(p0.Id, people[0].Id);
            }
        }

        [TestMethod]
        public void DeleteAllChildrenOfMother()
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
                EmployeeNumber = 0,
                Mother = p0
            };
            var p2 = new Person
            {
                FirstName = "Viktor",
                LastName = "Tånneryd",
                BirthDate = DateTime.Now,
                EmployeeNumber = 0,
                Mother = p0
            };

            using (var db = new UnitTestContext())
            {
                db.People.AddRange(new[] { p0, p1, p2 });
                db.SaveChanges();

                var people = db.People.OrderBy(p => p.FirstName).ToArray();
                Assert.AreEqual(3, people.Length);
                Assert.AreEqual((object)p0.Id, people[0].Id);
                Assert.AreEqual((object)p1.Id, people[1].Id);
                Assert.AreEqual((object)p2.Id, people[2].Id);

                // Delete all children of p0 in the database 
                // by providing an empty list of existing entities.

                p1.EmployeeNumber = null;
                p2.EmployeeNumber = null;
                db.BulkDeleteNotExisting<Person, Person>(new BulkDeleteRequest<Person>(
                    new[] { new SqlCondition("MotherId", p0.Id) },
                    new[] { "FirstName", "EmployeeNumber", "LastName" })
                {
                    Items = Array.Empty<Person>().ToList()
                });

                people = db.People.OrderBy(p => p.FirstName).ToArray();
                Assert.AreEqual(1, people.Length);
                Assert.AreEqual((object)p0.Id, people[0].Id);
            }
        }

        [TestMethod]
        public void DeleteAllChildrenOfOneMotherButNotTheOther()
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
                EmployeeNumber = 0,
                Mother = p0
            };
            var p2 = new Person
            {
                FirstName = "Viktor",
                LastName = "Tånneryd",
                BirthDate = DateTime.Now,
                EmployeeNumber = 0,
                Mother = p0
            };
            var p3 = new Person
            {
                FirstName = "Inga-Lill",
                LastName = "Tånneryd",
                BirthDate = DateTime.Now
            };
            var p4 = new Person
            {
                FirstName = "Måns",
                LastName = "Tånneryd",
                BirthDate = DateTime.Now,
                EmployeeNumber = 0,
                Mother = p3
            };
            using (var db = new UnitTestContext())
            {
                db.People.AddRange(new[] { p0, p1, p2, p3, p4 });
                db.SaveChanges();

                var people = db.People.OrderBy(p => p.FirstName).ToArray();
                Assert.AreEqual(5, people.Length);
                Assert.AreEqual((object)p0.Id, people[0].Id);
                Assert.AreEqual((object)p1.Id, people[1].Id);
                Assert.AreEqual((object)p3.Id, people[2].Id);
                Assert.AreEqual((object)p4.Id, people[3].Id);
                Assert.AreEqual((object)p2.Id, people[4].Id);

                // Delete all children of p0 in the database 
                // by providing an empty list of existing entities.

                p1.EmployeeNumber = null;
                p2.EmployeeNumber = null;
                db.BulkDeleteNotExisting<Person, Person>(new BulkDeleteRequest<Person>(
                    new[] { new SqlCondition("MotherId", p0.Id) },
                    new[] { "FirstName", "EmployeeNumber", "LastName" })
                {
                    Items = Array.Empty<Person>().ToList()
                });

                people = db.People.OrderBy(p => p.FirstName).ToArray();
                Assert.AreEqual(3, people.Length);
                Assert.AreEqual((object)p0.Id, people[0].Id);
                Assert.AreEqual((object)p3.Id, people[1].Id);
                Assert.AreEqual((object)p4.Id, people[2].Id);
            }
        }
    }
}