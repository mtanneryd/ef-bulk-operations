using System.Collections.Generic;

namespace Tanneryd.BulkOperations.EF6.Tests.DM.School
{
    public class Instructor
    {
        public Instructor()
        {
            this.Courses = new List<Course>();
        }

        // Primary key 
        public int InstructorID { get; set; }
        public string LastName { get; set; }
        public string FirstName { get; set; }
        public System.DateTime HireDate { get; set; }

        public OfficeAssignment OfficeAssignment { get; set; }

        public ICollection<Course> Courses { get; private set; }
    }
}