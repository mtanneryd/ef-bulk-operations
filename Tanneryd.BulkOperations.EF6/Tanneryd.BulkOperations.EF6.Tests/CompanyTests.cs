using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.EF6;
using Tanneryd.BulkOperations.EF6.Model;
using Tanneryd.BulkOperations.EF6.Tests.DM;
using Tanneryd.BulkOperations.EF6.Tests.DM.Companies;
using Tanneryd.BulkOperations.EF6.Tests.EF;

namespace Tanneryd.BulkOperations.EF6.Tests
{
    [TestClass]
    public class CompanyTests
    {
        [TestInitialize]
        public void Initialize()
        {
            Database.SetInitializer(new CreateDatabaseIfNotExists<CompanyContext>());
        }

        [TestCleanup]
        public void Cleanup()
        {
            var db = new CompanyContext();
            db.Employees.RemoveRange(db.Employees.ToArray());
            db.Companies.RemoveRange(db.Companies.ToArray());
            db.SaveChanges();
        }

        [TestMethod]
        public void AddingEmployeeWithCompany()
        {
            var employer = new Company
            {
                Name = "World Inc",
            };
            var employee = new Employee
            {
                Name = "John",
                Employer = employer
            };
            using (var db = new CompanyContext())
            {
                var request = new BulkInsertRequest<Employee>
                {
                    Entities = new List<Employee> {employee},
                    Recursive = true,
                };
                db.BulkInsertAll(request);

                var actual = db.Employees.Include(e => e.Employer).Single();
                Assert.AreEqual("John", actual.Name);
                Assert.AreEqual("World Inc", actual.Employer.Name);

                Cleanup();

                request.AllowNotNullSelfReferences = true;
                employee.Id = 0;
                employee.EmployerId = 0;
                employer.Id = 0;
                db.BulkInsertAll(request);

                actual = db.Employees.Include(e => e.Employer).Single();
                Assert.AreEqual("John", actual.Name);
                Assert.AreEqual("World Inc", actual.Employer.Name);
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
            employer.Employees.Add(employee);

            using (var db = new CompanyContext())
            {
                var request = new BulkInsertRequest<Company>
                {
                    Entities = new List<Company> { employer },
                    Recursive = true,
                };
                db.BulkInsertAll(request);

                var actual = db.Companies.Include(e => e.Employees).Single();
                Assert.AreEqual("World Inc", actual.Name);
                Assert.AreEqual("John", actual.Employees.Single().Name);

                Cleanup();

                request.AllowNotNullSelfReferences = true;
                employee.Id = 0;
                employee.EmployerId = 0;
                employer.Id = 0;
                db.BulkInsertAll(request);
                actual = db.Companies.Include(e => e.Employees).Single();
                Assert.AreEqual("World Inc", actual.Name);
                Assert.AreEqual("John", actual.Employees.Single().Name);
            }
        }

    }
}