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
using Tanneryd.BulkOperations.EF6.NET47.Tests.Models.DM.Numbers;
using Tanneryd.BulkOperations.EF6.NET47.Tests.Models.EF;

namespace Tanneryd.BulkOperations.EF6.NET47.Tests.Tests
{
    [TestClass]
    public class BulkOperationTestBase

    {

        protected void InitializeUnitTestContext()
        {
            Database.SetInitializer(new DropCreateDatabaseIfModelChanges<UnitTestContext>());
            CleanupUnitTestContext();
        }

        protected void CleanupUnitTestContext()
        {
            var db = new UnitTestContext();
            db.BatchInvoiceItems.RemoveRange(db.BatchInvoiceItems.ToArray());
            db.InvoiceItems.RemoveRange(db.InvoiceItems.ToArray());
            db.Invoices.RemoveRange(db.Invoices.ToArray());
            db.Journals.RemoveRange(db.Journals.ToArray());
            db.BatchInvoices.RemoveRange(db.BatchInvoices.ToArray());
            db.SaveChanges();

            db.Database.ExecuteSqlCommand(@"DELETE FROM [dbo].[PlayerWithDbGeneratedGuid]");
            db.Database.ExecuteSqlCommand(@"DELETE FROM [dbo].[CoachWithDbGeneratedGuid]");
            db.Database.ExecuteSqlCommand(@"DELETE FROM [dbo].[TeamWithDbGeneratedGuid]");
            db.Database.ExecuteSqlCommand(@"DELETE FROM [dbo].[CoachTeamsWithDbGeneratedGuid]");

            db.Database.ExecuteSqlCommand(@"DELETE FROM [dbo].[PlayerWithUserGeneratedGuid]");
            db.Database.ExecuteSqlCommand(@"DELETE FROM [dbo].[CoachWithUserGeneratedGuid]");
            db.Database.ExecuteSqlCommand(@"DELETE FROM [dbo].[TeamWithUserGeneratedGuid]");
            db.Database.ExecuteSqlCommand(@"DELETE FROM [dbo].[CoachTeamsWithUserGeneratedGuid]");

            db.OfficeAssignments.RemoveRange(db.OfficeAssignments.ToArray());
            foreach (var i in db.Instructors)
            {
                i.Courses.Clear();
            }
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
            db.Companies.RemoveRange(db.Companies.ToArray());
            db.SaveChanges();

            db.Composites.RemoveRange(db.Composites.ToArray());
            db.Primes.RemoveRange(db.Primes.ToArray());
            db.Numbers.RemoveRange(db.Numbers.ToArray());
            db.Parities.RemoveRange(db.Parities.ToArray());
            db.SaveChanges();

            db.Levels.RemoveRange(db.Levels.ToArray());
            db.SaveChanges();

            db.Prices.RemoveRange(db.Prices.ToArray());
            db.SaveChanges();
        }

        //#region Invoice

        //protected void InitializeInvoiceContext()
        //{
        //    Database.SetInitializer(new DropCreateDatabaseAlways<InvoiceContext>());
        //}

        //protected void CleanupInvoiceContext()
        //{
        //    var db = new InvoiceContext();
        //    db.BatchInvoiceItems.RemoveRange(db.BatchInvoiceItems.ToArray());
        //    db.InvoiceItems.RemoveRange(db.InvoiceItems.ToArray());
        //    db.Invoices.RemoveRange(db.Invoices.ToArray());
        //    db.Journals.RemoveRange(db.Journals.ToArray());
        //    db.BatchInvoices.RemoveRange(db.BatchInvoices.ToArray());
        //    db.SaveChanges();
        //}

        //#endregion

        //#region Teams

        //protected void InitializeTeamContext()
        //{
        //    Database.SetInitializer(new DropCreateDatabaseIfModelChanges<UserGeneratedTeamContext>());
        //    Database.SetInitializer(new DropCreateDatabaseIfModelChanges<DbGeneratedTeamContext>());
        //}

        //protected void CleanupTeamContext()
        //{
        //    var db1 = new UserGeneratedTeamContext();
        //    db1.Database.ExecuteSqlCommand(@"DELETE FROM [dbo].[Player]");
        //    db1.Database.ExecuteSqlCommand(@"DELETE FROM [dbo].[Coach]");
        //    db1.Database.ExecuteSqlCommand(@"DELETE FROM [dbo].[Team]");
        //    db1.Database.ExecuteSqlCommand(@"DELETE FROM [dbo].[CoachTeams]");

        //    var db2 = new DbGeneratedTeamContext();
        //    db2.Database.ExecuteSqlCommand(@"DELETE FROM [dbo].[Player]");
        //    db2.Database.ExecuteSqlCommand(@"DELETE FROM [dbo].[Coach]");
        //    db2.Database.ExecuteSqlCommand(@"DELETE FROM [dbo].[Team]");
        //    db2.Database.ExecuteSqlCommand(@"DELETE FROM [dbo].[CoachTeams]");
        //}

        //#endregion

        //#region School

        //protected void InitializeSchoolContext()
        //{
        //    Database.SetInitializer(new CreateDatabaseIfNotExists<SchoolContext>());
        //}

        //protected void CleanupSchoolContext()
        //{
        //    var db = new SchoolContext();
        //    db.OfficeAssignments.RemoveRange(db.OfficeAssignments.ToArray());
        //    foreach (var i in db.Instructors)
        //    {
        //        i.Courses.Clear();
        //    }
        //    db.Instructors.RemoveRange(db.Instructors.ToArray());
        //    db.Courses.RemoveRange(db.Courses.ToArray());
        //    db.Departments.RemoveRange(db.Departments.ToArray());
        //    db.SaveChanges();
        //}

        //#endregion

        //#region Miscellaneous

        //protected void InitializeMiscellaneousContext()
        //{
        //    Database.SetInitializer(new DropCreateDatabaseIfModelChanges<MiscellaneousContext>());
        //}

        //protected void CleanupMiscellaneousContext()
        //{
        //    var db = new MiscellaneousContext();
        //    db.ReservedSqlKeywords.RemoveRange(db.ReservedSqlKeywords.ToArray());
        //    db.Coordinates.RemoveRange(db.Coordinates.ToArray());
        //    db.Points.RemoveRange(db.Points.ToArray());
        //    db.SaveChanges();
        //}

        //#endregion

        //#region Blog

        //protected void InitializeBlogContext()
        //{
        //    Database.SetInitializer(new DropCreateDatabaseIfModelChanges<BlogContext>());
        //}

        //protected void CleanupBlogContext()
        //{
        //    var db = new BlogContext();
        //    db.Blogs.RemoveRange(db.Blogs.ToArray());
        //    db.Posts.RemoveRange(db.Posts.ToArray());
        //    db.Keywords.RemoveRange(db.Keywords.ToArray());
        //    db.Visitors.RemoveRange(db.Visitors.ToArray());
        //    db.SaveChanges();
        //}

        //#endregion

        //#region People

        //protected void InitializePeopleContext()
        //{
        //    Database.SetInitializer(new DropCreateDatabaseIfModelChanges<PeopleContext>());
        //}

        //protected void CleanupPeopleContext()
        //{
        //    var db = new PeopleContext();
        //    db.People.RemoveRange(db.People.ToArray());
        //    db.SaveChanges();
        //}

        //#endregion

        //#region Company

        //protected void InitializeCompanyContext()
        //{
        //    Database.SetInitializer(new CreateDatabaseIfNotExists<CompanyContext>());
        //}

        //protected void CleanupCompanyContext()
        //{
        //    var db = new CompanyContext();
        //    db.Employees.RemoveRange(db.Employees.ToArray());
        //    db.Companies.RemoveRange(db.Companies.ToArray());
        //    db.SaveChanges();
        //}

        //#endregion

        //#region Numbers

        //protected void InitializeNumberContext()
        //{
        //    Database.SetInitializer(new DropCreateDatabaseIfModelChanges<NumberContext>());
        //    CleanupNumberContext();
        //}

        //protected void CleanupNumberContext()
        //{
        //    var db = new NumberContext();
        //    db.Composites.RemoveRange(db.Composites.ToArray());
        //    db.Primes.RemoveRange(db.Primes.ToArray());
        //    db.Numbers.RemoveRange(db.Numbers.ToArray());
        //    db.Parities.RemoveRange(db.Parities.ToArray());
        //    db.SaveChanges();
        //}

        //#endregion

        //#region Levels

        //protected void InitializeLevelContext()
        //{
        //    Database.SetInitializer(new DropCreateDatabaseIfModelChanges<LevelContext>());
        //    CleanupLevelContext();
        //}

        //protected void CleanupLevelContext()
        //{
        //    var db = new LevelContext();
        //    db.Levels.RemoveRange(db.Levels.ToArray());
        //    db.SaveChanges();
        //}

        //#endregion

        //#region Prices

        //protected void InitializePriceContext()
        //{
        //    Database.SetInitializer(new DropCreateDatabaseIfModelChanges<PriceContext>());
        //    CleanupPriceContext();
        //}

        //protected void CleanupPriceContext()
        //{
        //    var db = new PriceContext();
        //    db.Prices.RemoveRange(db.Prices.ToArray());
        //    db.SaveChanges();
        //}

        //#endregion

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