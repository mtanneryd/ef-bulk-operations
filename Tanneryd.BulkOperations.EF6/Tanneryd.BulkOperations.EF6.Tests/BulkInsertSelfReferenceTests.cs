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
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.EF6;
using Tanneryd.BulkOperations.EF6.Model;
using Tanneryd.BulkOperations.EF6.Tests.DM;
using Tanneryd.BulkOperations.EF6.Tests.DM.Companies;
using Tanneryd.BulkOperations.EF6.Tests.DM.People;
using Tanneryd.BulkOperations.EF6.Tests.EF;

namespace Tanneryd.BulkOperations.EF6.Tests
{
    /// <summary>
    /// These tests primarily assert that we can insert entities with NOT NULL
    /// self references and that we get the expected exceptions when we do not
    /// follow the rules of entity engagement.
    /// </summary>
    [TestClass]
    public class BulkInsertSelfReferenceTests : BulkOperationTestBase
    {
        [TestInitialize]
        public void Initialize()
        {
            InitializeCompanyContext();
            InitializePeopleContext();
        }

        [TestCleanup]
        public void CleanUp()
        {
            CleanupCompanyContext();
            CleanupPeopleContext();
        }

        [TestMethod]
        [ExpectedException(typeof(System.Data.SqlClient.SqlException))]
        public void AddingEmployeeToCompanyWithoutParentCompanySet()
        {
            try
            {
                var employer = new Company
                {
                    Name = "World Inc",
                };
                var john = new Employee
                {
                    Name = "John",
                    Employer = employer
                };
                var adam = new Employee
                {
                    Name = "Adam",
                    Employer = employer
                };
                using (var db = new CompanyContext())
                {
                    var request = new BulkInsertRequest<Employee>
                    {
                        Entities = new List<Employee> { john, adam },
                        Recursive = true,
                        AllowNotNullSelfReferences = true
                    };
                    db.BulkInsertAll(request);
                }
            }
            catch (System.Data.SqlClient.SqlException e)
            {
                Assert.AreEqual(@"The ALTER TABLE statement conflicted with the FOREIGN KEY SAME TABLE constraint ""FK_dbo.Company_dbo.Company_ParentCompanyId"". The conflict occurred in database ""Tanneryd.BulkOperations.EF6.Tests.EF.CompanyContext"", table ""dbo.Company"", column 'Id'.", e.Message);
                throw;
            }
        }

        [TestMethod]
        public void AddingEmployeeToSubsidiary()
        {
            var corporateGroup = new Company
            {
                Name = "Global Corporation Inc",
            };
            corporateGroup.ParentCompany = corporateGroup;
            var employer = new Company
            {
                Name = "Subsidiary Corporation Inc",
            };
            employer.ParentCompany = corporateGroup;

            var john = new Employee
            {
                Name = "John",
                Employer = employer
            };
            var adam = new Employee
            {
                Name = "Adam",
                Employer = employer
            };
            using (var db = new CompanyContext())
            {
                var request = new BulkInsertRequest<Employee>
                {
                    Entities = new List<Employee> { john, adam },
                    Recursive = true,
                    AllowNotNullSelfReferences = true
                };
                db.BulkInsertAll(request);

                var actual = db.Employees
                    .Include(e => e.Employer.ParentCompany)
                    .OrderBy(e => e.Name).ToArray();
                Assert.AreEqual("Adam", actual[0].Name);
                Assert.AreEqual("Subsidiary Corporation Inc", actual[0].Employer.Name);
                Assert.AreSame(actual[0].Employer, actual[1].Employer);

                Assert.AreEqual("John", actual[1].Name);
                Assert.AreEqual("Subsidiary Corporation Inc", actual[1].Employer.Name);

                Assert.AreEqual("Global Corporation Inc", actual[0].Employer.ParentCompany.Name);
                Assert.AreEqual("Global Corporation Inc", actual[1].Employer.ParentCompany.Name);
                Assert.AreSame(actual[0].Employer.ParentCompany, actual[1].Employer.ParentCompany);
            }
        }

        [TestMethod]
        public void AddingCompanyWithEmployee()
        {
            var employee = new Employee
            {
                Name = "John",
            };
            var employer = new Company
            {
                Name = "World Inc",
            };
            employer.ParentCompany = employer;
            employer.Employees.Add(employee);

            using (var db = new CompanyContext())
            {
                var request = new BulkInsertRequest<Company>
                {
                    Entities = new List<Company> { employer },
                    Recursive = true,
                    AllowNotNullSelfReferences = true
                };
                db.BulkInsertAll(request);

                var actual = db.Companies.Include(e => e.Employees).Single();
                Assert.AreEqual("World Inc", actual.Name);
                Assert.AreSame(actual, actual.ParentCompany);
                Assert.AreEqual("John", actual.Employees.Single().Name);
            }
        }

        [TestMethod]
        public void AddingEmployeeWithCompany()

        {
            var employee = new Employee
            {
                Name = "John",
            };
            var employer = new Company
            {
                Name = "World Inc",
            };
            employer.ParentCompany = employer;
            employee.Employer = employer;

            using (var db = new CompanyContext())
            {
                var request = new BulkInsertRequest<Employee>
                {
                    Entities = new List<Employee> { employee },
                    Recursive = true,
                    AllowNotNullSelfReferences = true
                };
                db.BulkInsertAll(request);

                var actual = db.Companies.Include(e => e.Employees).Single();
                Assert.AreEqual("World Inc", actual.Name);
                Assert.AreSame(actual, actual.ParentCompany);
                Assert.AreEqual("John", actual.Employees.Single().Name);
            }
        }

        //[TestMethod]
        //public void AddingUser()
        //{
        //    var user = new User
        //    {
        //        UserName = "anna",
        //        Password = "hemligt",
        //        FirstName = "Anna",
        //        LastName = "Andersson",
        //        BirthDate = new DateTime(1980, 1, 1),
        //    };
            
        //    using (var db = new PeopleContext())
        //    {
        //        var request = new BulkInsertRequest<User>
        //        {
        //            Entities = new List<User> { user },
        //            Recursive = true,
        //        };
        //        db.BulkInsertAll(request);

        //        var people = db.People.ToArray();
        //        Assert.AreEqual(1, people.Length);
        //    }
        //}

        [TestMethod]
        public void AddingChildWithMother()
        {
            var mother = new Person
            {
                FirstName = "Anna",
                LastName = "Andersson",
                BirthDate = new DateTime(1980, 1, 1),
            };
            var child = new Person
            {
                FirstName = "Arvid",
                LastName = "Andersson",
                BirthDate = new DateTime(2018, 1, 1),
                Mother = mother,
            };
            using (var db = new PeopleContext())
            {
                var request = new BulkInsertRequest<Person>
                {
                    Entities = new List<Person> { child },
                    Recursive = true,
                };
                db.BulkInsertAll(request);

                var people = db.People.ToArray();
                Assert.AreEqual(2, people.Length);
            }
        }

        [TestMethod]
        public void AddingMotherWithChild()
        {
            var child = new Person
            {
                FirstName = "Arvid",
                LastName = "Andersson",
                BirthDate = new DateTime(2018, 1, 1),
            };
            var mother = new Person
            {
                FirstName = "Anna",
                LastName = "Andersson",
                BirthDate = new DateTime(1980, 1, 1),
            };
            mother.Children.Add(child);

            using (var db = new PeopleContext())
            {
                var request = new BulkInsertRequest<Person>
                {
                    Entities = new List<Person> { mother },
                    Recursive = true,
                };
                db.BulkInsertAll(request);

                var people = db.People.ToArray();
                Assert.AreEqual(2, people.Length);
            }
        }
    }
}