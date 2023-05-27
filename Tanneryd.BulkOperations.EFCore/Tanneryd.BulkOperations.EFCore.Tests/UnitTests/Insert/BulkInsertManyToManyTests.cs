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
using Tanneryd.BulkOperations.EFCore.Model;

namespace Tanneryd.BulkOperations.EFCore.Tests.UnitTests.Insert
{
    [TestClass]
    public class BulkInsertManyToManyTests : BulkOperationTestBase
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
        public void JoinTablesWithGuidKeysShouldBeProperlyInserted()
        {
            using (var db = new UnitTestContext())
            {
                var blog = new Blog {Name = "My Blog"};
                var firstPost = new Post
                {
                    Blog = blog,
                    Text = "My first blogpost.",
                };
                var visitor = new Visitor
                {
                    Name = "Visitor1"
                };
                firstPost.Visitors.Add(visitor);
                var req = new BulkInsertRequest<Post>
                {
                    Entities = new[] {firstPost}.ToList(),
                    AllowNotNullSelfReferences = AllowNotNullSelfReferences.No,
                    SortUsingClusteredIndex = true,
                    EnableRecursiveInsert = EnableRecursiveInsert.Yes
                };
                var response = db.BulkInsertAll(req);
                var posts = db.Posts
                    .Include(p => p.Blog)
                    .ToArray();
                Assert.AreEqual(1, posts.Count());
            }
        }


        [TestMethod]
        public void StackOverflowTest()
        {
            using (var db = new UnitTestContext())
            {
                var i1 = new Instructor
                {
                    FirstName = "Mickey",
                    LastName = "Mouse",
                    HireDate = new DateTime(1928, 5, 15),
                    OfficeAssignment = new OfficeAssignment {Location = "Room 1A"}
                };
                db.Instructors.Add(i1);
                db.SaveChanges();

                var d1 = new Department
                {
                    Name = "Computer Science",
                    Budget = 10000000,
                };

                var c1 = new Course
                {
                    Credits = 4,
                    Title = "Foundations of Data Science",
                    Department = d1
                };

                db.Courses.Add(c1);
                db.SaveChanges();

                i1.Courses.Add(c1);
                db.SaveChanges();

                var courses = db.Instructors
                    .Include(i => i.CourseInstructors)
                    .ThenInclude(ci => ci.Course)
                    .Distinct()
                    .ToArray();

                Assert.AreEqual(1, courses.Count());

                var instructor = db.Instructors
                    .Include(i => i.CourseInstructors)
                    .ThenInclude(ci => ci.Course)
                    .Include(i => i.OfficeAssignment)
                    .Single();

                instructor.InstructorId = 0;
                instructor.OfficeAssignment.InstructorId = 0;

                var request = new BulkInsertRequest<Instructor>
                {
                    Entities = new[] {instructor}.ToList(),
                    EnableRecursiveInsert = EnableRecursiveInsert.Yes,
                    AllowNotNullSelfReferences = AllowNotNullSelfReferences.No
                };
                db.BulkInsertAll(request);

                courses = db.Instructors
                    .Include(i => i.CourseInstructors)
                    .ThenInclude(ci => ci.Course)
                    .ToArray();
                Assert.AreEqual(2, courses.Count());
            }
        }

        /// <summary>
        /// Test that two instructors with multiple but disjoint courses
        /// can be bulk inserted correctly.
        /// </summary>
        [TestMethod]
        public void InstructorsWithMulitpleCoursesShouldBeBulkInserted()
        {
            using (var db = new UnitTestContext())
            {
                var instructors = GetInstructors().ToArray();
                var courses = GetCourses().ToArray();
                instructors[0].CourseInstructors.Add(new CourseInstructor {Course = courses[0]});
                instructors[0].CourseInstructors.Add(new CourseInstructor {Course = courses[1]});
                instructors[0].CourseInstructors.Add(new CourseInstructor {Course = courses[2]});
                instructors[1].CourseInstructors.Add(new CourseInstructor {Course = courses[3]});
                instructors[1].CourseInstructors.Add(new CourseInstructor {Course = courses[4]});

                var request = new BulkInsertRequest<Instructor>
                {
                    Entities = instructors.ToList(),
                    EnableRecursiveInsert = EnableRecursiveInsert.Yes,
                    AllowNotNullSelfReferences = AllowNotNullSelfReferences.No
                };
                db.BulkInsertAll(request);
                var dbInstructors = db.Instructors
                    .Include(i => i.CourseInstructors)
                    .ThenInclude(ci => ci.Course)
                    .ToArray();
                var dbCourses = db.Courses.ToArray();
                Assert.AreEqual(2, dbInstructors.Length);
                Assert.AreEqual(5, dbInstructors.SelectMany(i => i.Courses).Count());
                Assert.AreEqual(3, dbInstructors[0].Courses.Count);
                Assert.AreEqual(2, dbInstructors[1].Courses.Count);
                Assert.AreSame(dbCourses[0].Instructors.Single(), dbCourses[1].Instructors.Single());
                Assert.AreSame(dbCourses[0].Instructors.Single(), dbCourses[2].Instructors.Single());
                Assert.AreSame(dbCourses[3].Instructors.Single(), dbCourses[4].Instructors.Single());
            }
        }

        /// <summary>
        /// Test that five courses, each with a single instructor, can
        /// be bulk inserted. There are only two instructors. Three courses
        /// share the first of them and two courses share the second. 
        /// </summary>
        [TestMethod]
        public void CoursesWithSingleInstructorShouldBeBulkInserted()
        {
            using (var db = new UnitTestContext())
            {
                var instructors = GetInstructors().ToArray();
                var courses = GetCourses().ToArray();
                courses[0].CourseInstructors.Add(new CourseInstructor {Instructor = instructors[0]});
                courses[1].CourseInstructors.Add(new CourseInstructor {Instructor = instructors[0]});
                courses[2].CourseInstructors.Add(new CourseInstructor {Instructor = instructors[0]});
                courses[3].CourseInstructors.Add(new CourseInstructor {Instructor = instructors[1]});
                courses[4].CourseInstructors.Add(new CourseInstructor {Instructor = instructors[1]});
                
                var request = new BulkInsertRequest<Course>
                {
                    Entities = courses.ToList(),
                    EnableRecursiveInsert = EnableRecursiveInsert.Yes,
                    AllowNotNullSelfReferences = AllowNotNullSelfReferences.No
                };
                db.BulkInsertAll(request);

                var dbInstructors = db.Instructors
                    .Include(i => i.CourseInstructors)
                    .ThenInclude(ci => ci.Course)
                    .ToArray();
                var dbCourses = db.Courses.ToArray();
                Assert.AreEqual(2, dbInstructors.Length);
                Assert.AreEqual(5, dbInstructors.SelectMany(i => i.Courses).Count());
                Assert.AreEqual(3, dbInstructors[0].Courses.Count);
                Assert.AreEqual(2, dbInstructors[1].Courses.Count);
                Assert.AreSame(dbCourses[0].Instructors.Single(), dbCourses[1].Instructors.Single());
                Assert.AreSame(dbCourses[0].Instructors.Single(), dbCourses[2].Instructors.Single());
                Assert.AreSame(dbCourses[3].Instructors.Single(), dbCourses[4].Instructors.Single());
            }
        }


        /// <summary>
        /// Create two instructor entities.
        /// </summary>
        /// <returns></returns>
        private IEnumerable<Instructor> GetInstructors()
        {
            yield return new Instructor
            {
                FirstName = "Mickey",
                LastName = "Mouse",
                HireDate = new DateTime(1928, 5, 15),
                OfficeAssignment = new OfficeAssignment {Location = "Room 1A"}
            };
            yield return new Instructor
            {
                FirstName = "Donald",
                LastName = "Duck",
                HireDate = new DateTime(1934, 6, 9),
                OfficeAssignment = new OfficeAssignment {Location = "Room 1B"}
            };
        }

        /// <summary>
        /// Create five course entities belonging two the same department.
        /// </summary>
        /// <returns></returns>
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