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

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tanneryd.BulkOperations.EFCore.SQLite.Tests.UnitTests.Insert
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
            InitializeUnitTestContext();
            CleanUp();
        }

        [TestCleanup]
        public void CleanUp()
        {
            CleanupUnitTestContext();
        }

        [TestMethod]
        [ExpectedException(typeof(SqlException))]
        public void AddingEmployeeToCompanyWithoutParentCompanySet()
        {
            try
            {
                var Company = new Company
                {
                    Name = "World Inc",
                };
                var john = new Employee
                {
                    Name = "John",
                    Company = Company
                };
                var adam = new Employee
                {
                    Name = "Adam",
                    Company = Company
                };
                using (var db = Factory.CreateDbContext())
                {
                    var request = new BulkInsertRequest<Employee>
                    {
                        Entities = new List<Employee> { john, adam },
                        EnableRecursiveInsert = EnableRecursiveInsert.Yes,
                        AllowNotNullSelfReferences = AllowNotNullSelfReferences.Yes
                    };
                    db.BulkInsertAll(request);
                }
            }
            catch (SqlException e)
            {
                var expectedMessage =
                    @"The ALTER TABLE statement conflicted with the FOREIGN KEY SAME TABLE constraint ""FK_dbo.Company_dbo.Company_ParentCompanyId"". The conflict occurred in database ""Tanneryd.BulkOperations.EFCore.Tests.Models.EF.UnitTestContext"", table ""dbo.Company"", column 'Id'.";
                Assert.AreEqual(expectedMessage, e.Message);
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
            var Company = new Company
            {
                Name = "Subsidiary Corporation Inc",
            };
            Company.ParentCompany = corporateGroup;

            var john = new Employee
            {
                Name = "John",
                Company = Company
            };
            var adam = new Employee
            {
                Name = "Adam",
                Company = Company
            };
            using (var db = Factory.CreateDbContext())
            {
                var request = new BulkInsertRequest<Employee>
                {
                    Entities = new List<Employee> { john, adam },
                    EnableRecursiveInsert = EnableRecursiveInsert.Yes,
                    AllowNotNullSelfReferences = AllowNotNullSelfReferences.Yes
                };
                db.BulkInsertAll(request);

                var actual = db.Employees
                    .Include(e => e.Company.ParentCompany)
                    .OrderBy(e => e.Name).ToArray();
                Assert.AreEqual("Adam", actual[0].Name);
                Assert.AreEqual("Subsidiary Corporation Inc", actual[0].Company.Name);
                Assert.AreSame(actual[0].Company, actual[1].Company);

                Assert.AreEqual("John", actual[1].Name);
                Assert.AreEqual("Subsidiary Corporation Inc", actual[1].Company.Name);

                Assert.AreEqual("Global Corporation Inc", actual[0].Company.ParentCompany.Name);
                Assert.AreEqual("Global Corporation Inc", actual[1].Company.ParentCompany.Name);
                Assert.AreSame(actual[0].Company.ParentCompany, actual[1].Company.ParentCompany);
            }
        }

        [TestMethod]
        public void AddingCompanyWithEmployee()
        {
            var employee = new Employee
            {
                Name = "John",
            };
            var Company = new Company
            {
                Name = "World Inc",
            };
            Company.ParentCompany = Company;
            Company.Employees.Add(employee);

            using (var db = Factory.CreateDbContext())
            {
                var request = new BulkInsertRequest<Company>
                {
                    Entities = new List<Company> { Company },
                    EnableRecursiveInsert = EnableRecursiveInsert.Yes,
                    AllowNotNullSelfReferences = AllowNotNullSelfReferences.Yes
                };
                db.BulkInsertAll(request);

                var actual = db.Companies
                    .Include(e => e.Employees)
                    .Single();
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
            var Company = new Company
            {
                Name = "World Inc",
            };
            Company.ParentCompany = Company;
            employee.Company = Company;

            using (var db = Factory.CreateDbContext())
            {
                var request = new BulkInsertRequest<Employee>
                {
                    Entities = new List<Employee> { employee },
                    EnableRecursiveInsert = EnableRecursiveInsert.Yes,
                    AllowNotNullSelfReferences = AllowNotNullSelfReferences.Yes
                };
                db.BulkInsertAll(request);

                var actual = db.Companies.Include(e => e.Employees).Single();
                Assert.AreEqual("World Inc", actual.Name);
                Assert.AreSame(actual, actual.ParentCompany);
                Assert.AreEqual("John", actual.Employees.Single().Name);
            }
        }

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
            using (var db = Factory.CreateDbContext())
            {
                var request = new BulkInsertRequest<Person>
                {
                    Entities = new List<Person> { child },
                    EnableRecursiveInsert = EnableRecursiveInsert.Yes,
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
            mother.People.Add(child);

            using (var db = Factory.CreateDbContext())
            {
                var request = new BulkInsertRequest<Person>
                {
                    Entities = new List<Person> { mother },
                    EnableRecursiveInsert = EnableRecursiveInsert.Yes,
                };
                db.BulkInsertAll(request);

                var people = db.People.ToArray();
                Assert.AreEqual(2, people.Length);
            }
        }
    }
}