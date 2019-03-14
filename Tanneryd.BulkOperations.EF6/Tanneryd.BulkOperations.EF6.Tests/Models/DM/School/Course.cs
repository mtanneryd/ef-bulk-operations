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