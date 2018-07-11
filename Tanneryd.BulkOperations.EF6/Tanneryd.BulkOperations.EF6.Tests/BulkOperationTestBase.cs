using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.EF6.Tests.DM.Numbers;
using Tanneryd.BulkOperations.EF6.Tests.EF;

namespace Tanneryd.BulkOperations.EF6.Tests
{
    [TestClass]
    public class BulkOperationTestBase

    {
        #region School

        protected void InitializeSchoolContext()
        {
            Database.SetInitializer(new CreateDatabaseIfNotExists<SchoolContext>());
        }

        protected void CleanupSchoolContext()
        {
            var db = new SchoolContext();
            db.OfficeAssignments.RemoveRange(db.OfficeAssignments.ToArray());
            db.Instructors.RemoveRange(db.Instructors.ToArray());
            db.Courses.RemoveRange(db.Courses.ToArray());
            db.Departments.RemoveRange(db.Departments.ToArray());
            db.SaveChanges();
        }

        #endregion

        #region Blog

        protected void InitializeBlogContext()
        {
            Database.SetInitializer(new CreateDatabaseIfNotExists<BlogContext>());
        }

        protected void CleanupBlogContext()
        {
            var db = new BlogContext();
            db.Blogs.RemoveRange(db.Blogs.ToArray());
            db.Posts.RemoveRange(db.Posts.ToArray());
            db.Keywords.RemoveRange(db.Keywords.ToArray());
            db.SaveChanges();
        }

        #endregion

        #region People

        protected void InitializePeopleContext()
        {
            Database.SetInitializer(new CreateDatabaseIfNotExists<PeopleContext>());
        }

        protected void CleanupPeopleContext()
        {
            var db = new PeopleContext();
            db.People.RemoveRange(db.People.ToArray());
            db.SaveChanges();
        }


        #endregion
        
        #region Company

        protected void InitializeCompanyContext()
        {
            Database.SetInitializer(new CreateDatabaseIfNotExists<CompanyContext>());
        }

        protected void CleanupCompanyContext()
        {
            var db = new CompanyContext();
            db.Employees.RemoveRange(db.Employees.ToArray());
            db.Companies.RemoveRange(db.Companies.ToArray());
            db.SaveChanges();
        }

        #endregion


        protected void InitializeNumberContext()
        {
            Database.SetInitializer(new CreateDatabaseIfNotExists<NumberContext>());
            CleanupNumberContext();
        }

        protected void CleanupNumberContext()
        {
            var db = new NumberContext();
            db.Composites.RemoveRange(db.Composites.ToArray());
            db.Primes.RemoveRange(db.Primes.ToArray());
            db.Numbers.RemoveRange(db.Numbers.ToArray());
            db.Parities.RemoveRange(db.Parities.ToArray());
            db.Levels.RemoveRange(db.Levels.ToArray());
            db.SaveChanges();
        }
               

        protected static IEnumerable<Number> GenerateNumbers(int start, int count, DateTime now)
        {
            Parity even = new Parity { Name = "Even", UpdatedAt = now, UpdatedBy = "Måns" };
            Parity odd = new Parity { Name = "Odd", UpdatedAt = now, UpdatedBy = "Måns" };
            return GenerateNumbers(start, count, even, odd, now);
        }

        protected static IEnumerable<Number> GenerateNumbers(int start, int count, Parity even, Parity odd, DateTime now)
        {
            for (int i = start; i < count + start; i++)
            {
                var parity = i % 2 == 0 ? even : odd;
                var n = new Number
                {
                    Value = i,
                    ParityId = parity.Id,
                    Parity = parity,
                    UpdatedAt = now,
                    UpdatedBy = "Måns"
                };

                yield return n;
            }
        }

        protected static Prime[] GeneratePrimeNumbers(int count, Number[] numbers, DateTime now)
        {
            var primes = new List<int>();
            for (int i = 1; i <= count; i++)
            {
                var factors = Factorize(i).ToArray();
                if (factors.Length == 1 &&
                    factors[0] == i)
                    primes.Add(i);
            }

            return primes.Select(p => new Prime
            {
                NumberId = numbers.Single(n => n.Value == p).Id,
                Number = numbers.Single(n => n.Value == p),
                UpdatedAt = now,
                UpdatedBy = "Måns"
            }).ToArray();
        }

        protected static IEnumerable<long> Factorize(long composite)
        {
            for (long i = 2; i <= Math.Sqrt(composite); i++)
            {
                while (composite % i == 0)
                {
                    if (i == 1) continue;
                    yield return i;
                    composite /= i;
                }
            }
            yield return composite;
        }

    }

    internal class Num
    {
        public long Val { get; set; }
    }

}