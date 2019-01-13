/*
* Copyright ©  2017-2018 Tånneryd IT AB
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
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity;
using System.Data.Entity.Migrations;
using Tanneryd.BulkOperations.EF6.Tests.DM.School;

namespace Tanneryd.BulkOperations.EF6.Tests.EF
{
   // https://msdn.microsoft.com/en-us/library/jj591620%28v=vs.113%29.aspx?f=255&MSPPError=-2147217396
    public class SchoolContext : DbContext
    {
        public DbSet<Course> Courses { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<Instructor> Instructors { get; set; }
        public DbSet<OfficeAssignment> OfficeAssignments { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            #region Department

            modelBuilder.Entity<Department>()
                .ToTable("Department")
                .HasKey(p => p.DepartmentID);
            modelBuilder.Entity<Department>()
                .Property(e => e.Name);
            modelBuilder.Entity<Department>()
                .Property(e => e.Budget);
            modelBuilder.Entity<Department>()
                .Property(e => e.Administrator)
                .IsOptional();
            modelBuilder.Entity<Department>()
                .HasMany(e => e.Courses)
                .WithRequired(e => e.Department)
                .HasForeignKey(e => e.DepartmentID);

            #endregion

            #region Course

            modelBuilder.Entity<Course>()
                .ToTable("Course")
                .HasKey(p => p.CourseID);
            modelBuilder.Entity<Course>()
                .Property(e => e.Title);
            modelBuilder.Entity<Course>()
                .Property(e => e.Credits);
            modelBuilder.Entity<Course>()
                .HasMany(t => t.Instructors)
                .WithMany(t => t.Courses)
                .Map(m =>
                {
                    m.ToTable("CourseInstructor");
                    m.MapLeftKey("CourseID");
                    m.MapRightKey("InstructorID");
                });

            #endregion

            #region Instructor

            modelBuilder.Entity<Instructor>()
                .ToTable("Instructor")
                .HasKey(p => p.InstructorID);
            modelBuilder.Entity<Instructor>()
                .Property(e => e.FirstName);
            modelBuilder.Entity<Instructor>()
                .Property(e => e.LastName);
            modelBuilder.Entity<Instructor>()
                .Property(e => e.FullName)
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Computed);
            modelBuilder.Entity<Instructor>()
                .Property(e => e.HireDate);
            modelBuilder.Entity<Instructor>()
                .HasRequired(t => t.OfficeAssignment)
                .WithRequiredPrincipal(t => t.Instructor);

            #endregion

            #region OfficeAssignment

            modelBuilder.Entity<OfficeAssignment>()
                .ToTable("OfficeAssignment")
                .HasKey(p => p.InstructorID);
            modelBuilder.Entity<OfficeAssignment>()
                .Property(e => e.Location);
            modelBuilder.Entity<OfficeAssignment>()
                .Property(e => e.Timestamp)
                .IsConcurrencyToken()
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Computed);

            #endregion
        }
    }
}