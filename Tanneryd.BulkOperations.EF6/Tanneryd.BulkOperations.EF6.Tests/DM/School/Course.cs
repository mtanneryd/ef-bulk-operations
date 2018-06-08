using System.Collections.Generic;

namespace Tanneryd.BulkOperations.EF6.Tests.DM.School
{
    public class Course
    {
        public Course()
        {
            this.Instructors = new HashSet<Instructor>();
        }
        // Primary key 
        public int CourseID { get; set; }

        public string Title { get; set; }
        public int Credits { get; set; }

        // Foreign key 
        public int DepartmentID { get; set; }
        public Department Department { get; set; }

        public ICollection<Instructor> Instructors { get; private set; }
    }
}