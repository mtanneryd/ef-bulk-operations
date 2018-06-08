using System;

namespace Tanneryd.BulkOperations.EF6.Tests.DM.School
{
    public class OfficeAssignment
    {
        // Specifying InstructorID as a primary 
        public Int32 InstructorID { get; set; }

        public string Location { get; set; }

        public Byte[] Timestamp { get; set; }

        // Navigation property 
        public virtual Instructor Instructor { get; set; }
    }
}