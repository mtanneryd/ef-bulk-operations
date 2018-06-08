using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tanneryd.BulkOperations.EF6.Tests.DM.School
{
    public class Department
    {
        public Department()
        {
            this.Courses = new HashSet<Course>();
        }
        // Primary key 
        public int DepartmentID { get; set; }
        public string Name { get; set; }
        public decimal Budget { get; set; }
        public int? Administrator { get; set; }

        public ICollection<Course> Courses { get; private set; }
    }
}
