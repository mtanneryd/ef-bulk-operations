/*
 * Copyright ©  2017-2026 Tånneryd IT AB
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
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity;
using System.Data.Entity.ModelConfiguration;

namespace Tanneryd.BulkOperations.EF6.NET48.Tests.Models.EF.IndependentAssociation
{
    public class IaDepartment
    {
        public IaDepartment()
        {
            Courses = new HashSet<IaCourse>();
        }

        public int Id { get; set; }
        public string Name { get; set; }
        public ICollection<IaCourse> Courses { get; set; }
    }

    /// <summary>
    /// Dependent with navigation only — no CLR FK property. MapKey makes this
    /// an independent association (AssociationType.Constraint is null).
    /// </summary>
    public class IaCourse
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public IaDepartment Department { get; set; }
    }

    public class IndependentAssociationContext : DbContext
    {
        public const string ConnectionString =
            @"data source=(localdb)\MSSQLLocalDB;initial catalog=Tanneryd.BulkOperations.EF6.NET48.Tests.IndependentAssociation;persist security info=True;Integrated Security=SSPI;MultipleActiveResultSets=True;TrustServerCertificate=true";

        public IndependentAssociationContext()
            : base(ConnectionString)
        {
        }

        public DbSet<IaDepartment> Departments { get; set; }
        public DbSet<IaCourse> Courses { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Configurations.Add(new IaDepartmentConfiguration());
            modelBuilder.Configurations.Add(new IaCourseConfiguration());
            base.OnModelCreating(modelBuilder);
        }
    }

    public class IaDepartmentConfiguration : EntityTypeConfiguration<IaDepartment>
    {
        public IaDepartmentConfiguration()
        {
            ToTable("IaDepartment");
            HasKey(e => e.Id);
            Property(e => e.Id).HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            Property(e => e.Name).IsOptional();
        }
    }

    public class IaCourseConfiguration : EntityTypeConfiguration<IaCourse>
    {
        public IaCourseConfiguration()
        {
            ToTable("IaCourse");
            HasKey(e => e.Id);
            Property(e => e.Id).HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            Property(e => e.Title).IsOptional();

            // Independent association: FK column in DB, not on CLR type.
            HasRequired(e => e.Department)
                .WithMany(d => d.Courses)
                .Map(m => m.MapKey("DepartmentId"));
        }
    }
}
