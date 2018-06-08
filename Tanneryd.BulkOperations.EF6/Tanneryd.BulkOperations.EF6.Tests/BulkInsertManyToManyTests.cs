using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.EF6.Model;
using Tanneryd.BulkOperations.EF6.Tests.DM.School;
using Tanneryd.BulkOperations.EF6.Tests.EF;

namespace Tanneryd.BulkOperations.EF6.Tests
{
    [TestClass]
    public class BulkInsertManyToManyTests : BulkOperationTestBase
    {
        [TestInitialize]
        public void Initialize()
        {
            InitializeSchoolContext();
        }

        [TestCleanup]
        public void CleanUp()
        {
            CleanupSchoolContext();
        }

        [TestMethod]
        public void InstructorsWithMulitpleCoursesShouldBeBulkInserted()
        {
            using (var db = new SchoolContext())
            {
                var instructors = GetInstructors().ToArray();
                var courses = GetCourses().ToArray();
                instructors[0].Courses.Add(courses[0]);
                instructors[0].Courses.Add(courses[1]);
                instructors[0].Courses.Add(courses[2]);
                instructors[1].Courses.Add(courses[3]);
                instructors[1].Courses.Add(courses[4]);

                var request = new BulkInsertRequest<Instructor>
                {
                    Entities = instructors.ToList(),
                    Recursive = true,
                    AllowNotNullSelfReferences = false
                };
                db.BulkInsertAll(request);
                var dbInstructors = db.Instructors
                    .Include(i=>i.Courses)
                    .ToArray();
                Assert.AreEqual(2, dbInstructors.Length);
                Assert.AreEqual(5, dbInstructors.SelectMany(i=>i.Courses).Count());
            }
        }

        [TestMethod]
        public void CoursesWithSingleInstructorShouldBeBulkInserted()
        {
            using (var db = new SchoolContext())
            {
                var instructors = GetInstructors().ToArray();
                var courses = GetCourses().ToArray();
                courses[0].Instructors.Add(instructors[0]);
                courses[1].Instructors.Add(instructors[0]);
                courses[2].Instructors.Add(instructors[0]);
                courses[3].Instructors.Add(instructors[1]);
                courses[4].Instructors.Add(instructors[1]);
                var request = new BulkInsertRequest<Course>
                {
                    Entities = courses.ToList(),
                    Recursive = true,
                    AllowNotNullSelfReferences = false
                };
                db.BulkInsertAll(request);

                var dbInstructors = db.Instructors
                    .Include(i => i.Courses)
                    .ToArray();
                Assert.AreEqual(2, dbInstructors.Length);
                Assert.AreEqual(5, dbInstructors.SelectMany(i => i.Courses).Count());
            }
        }


        private IEnumerable<Instructor> GetInstructors()
        {
            yield return new Instructor
            {
                FirstName = "Mickey",
                LastName = "Mouse",
                HireDate = new DateTime(1928, 5, 15),
                OfficeAssignment = new OfficeAssignment { Location = "Room 1A"}
            };
            yield return new Instructor
            {
                FirstName = "Donald",
                LastName = "Duck",
                HireDate = new DateTime(1934, 6, 9),
                OfficeAssignment = new OfficeAssignment { Location = "Room 1B" }
            };
        }

        private IEnumerable<Course> GetCourses()
        {
            var department = new Department
            {
                Name = "Computer Science",
                Budget = 10000000,
            };

            yield return new Course
            {
                Credits = 4,
                Title = "Foundations of Data Science",
                Department = department
            };
            yield return new Course
            {
                Credits = 3,
                Title = "Introduction to Computational Thinking with Data",
                Department = department
            };
            yield return new Course
            {
                Credits = 2,
                Title = "Matlab for Programmers",
                Department = department
            };
            yield return new Course
            {
                Credits = 2,
                Title = "C for Programmers",
                Department = department
            };
            yield return new Course
            {
                Credits = 2,
                Title = "Scheme and Functional Programming for Programmers",
                Department = department
            };
        }
    }
}