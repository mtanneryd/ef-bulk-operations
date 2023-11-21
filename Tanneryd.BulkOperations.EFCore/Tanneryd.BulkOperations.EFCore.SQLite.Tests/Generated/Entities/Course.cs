// <auto-generated>

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Tanneryd.BulkOperations.EFCore.SQLite.Tests
{
    public class Course
    {
        public long CourseId { get; set; }
        public string Title { get; set; }
        public long Credits { get; set; }
        public long DepartmentId { get; set; }

        // Reverse navigation
        public ICollection<CourseInstructor> CourseInstructors { get; set; }

        public Department Department { get; set; }

        public Course()
        {
            CourseInstructors = new List<CourseInstructor>();
        }
    }

}
// </auto-generated>
