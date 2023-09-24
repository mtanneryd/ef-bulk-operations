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
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tanneryd.BulkOperations.EFCore.Tests.UnitTests
{
    [TestClass]
    public class BulkOperationTestBase
    {
        static BulkOperationTestBase()
        {
            Factory = new MSSQLContextFactory();
        }

        public static readonly MSSQLContextFactory Factory;


        protected void InitializeUnitTestContext()
        {
            //using (var ctx = Factory.CreateDbContext())
            using(var ctx = Factory.CreateDbContext())
            {
                ctx.Database.Migrate();
            }
            CleanupUnitTestContext();
        }

        protected void CleanupUnitTestContext()
        {
            var db = Factory.CreateDbContext();
            db.BatchInvoiceItems.RemoveRange(db.BatchInvoiceItems.ToArray());
            db.InvoiceItems.RemoveRange(db.InvoiceItems.ToArray());
            db.Invoices.RemoveRange(db.Invoices.ToArray());
            db.Journals.RemoveRange(db.Journals.ToArray());
            db.BatchInvoices.RemoveRange(db.BatchInvoices.ToArray());
            db.SaveChanges();

            db.Database.ExecuteSqlRaw(@"DELETE FROM [dbo].[PlayerWithDbGeneratedGuid]");
            db.Database.ExecuteSqlRaw(@"DELETE FROM [dbo].[CoachWithDbGeneratedGuid]");
            db.Database.ExecuteSqlRaw(@"DELETE FROM [dbo].[TeamWithDbGeneratedGuid]");
            db.Database.ExecuteSqlRaw(@"DELETE FROM [dbo].[CoachTeamsWithDbGeneratedGuid]");

            db.Database.ExecuteSqlRaw(@"DELETE FROM [dbo].[PlayerWithUserGeneratedGuid]");
            db.Database.ExecuteSqlRaw(@"DELETE FROM [dbo].[CoachWithUserGeneratedGuid]");
            db.Database.ExecuteSqlRaw(@"DELETE FROM [dbo].[TeamWithUserGeneratedGuid]");
            db.Database.ExecuteSqlRaw(@"DELETE FROM [dbo].[CoachTeamsWithUserGeneratedGuid]");

            db.OfficeAssignments.RemoveRange(db.OfficeAssignments.ToArray());
            foreach (var i in db.Instructors)
            {
                i.CourseInstructors.Clear();
            }
            foreach (var i in db.Courses)
            {
                i.CourseInstructors.Clear();
            }
            db.CourseInstructors.RemoveRange(db.CourseInstructors.ToArray());
            db.Instructors.RemoveRange(db.Instructors.ToArray());
            db.Courses.RemoveRange(db.Courses.ToArray());
            db.Departments.RemoveRange(db.Departments.ToArray());
            db.SaveChanges();

            db.ReservedSqlKeywords.RemoveRange(db.ReservedSqlKeywords.ToArray());
            db.Coordinates.RemoveRange(db.Coordinates.ToArray());
            db.Points.RemoveRange(db.Points.ToArray());
            db.SaveChanges();

            db.Blogs.RemoveRange(db.Blogs.ToArray());
            db.Posts.RemoveRange(db.Posts.ToArray());
            db.Keywords.RemoveRange(db.Keywords.ToArray());
            db.Visitors.RemoveRange(db.Visitors.ToArray());
            db.SaveChanges();

            db.People.RemoveRange(db.People.ToArray());
            db.SaveChanges();

            db.Employees.RemoveRange(db.Employees.ToArray());
            foreach (var c in db.Companies)
            {
                c.ParentCompanyId = 0;
                c.ParentCompany = null;
            }
            db.Companies.RemoveRange(db.Companies.ToArray());
            db.SaveChanges();

            db.Composites.RemoveRange(db.Composites.ToArray());
            db.Primes.RemoveRange(db.Primes.ToArray());
            db.Numbers.RemoveRange(db.Numbers.ToArray());
            db.Parities.RemoveRange(db.Parities.ToArray());
            db.SaveChanges();

            db.Prices.RemoveRange(db.Prices.ToArray());
            db.SaveChanges();

            db.LogItems.RemoveRange(db.LogItems.ToArray());
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