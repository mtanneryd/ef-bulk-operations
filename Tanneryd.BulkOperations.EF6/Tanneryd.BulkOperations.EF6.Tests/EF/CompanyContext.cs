/*
* Copyright ©  2017 Tånneryd IT AB
* 
* This file is part of the tutorial application BulkInsert.App.
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
using Tanneryd.BulkOperations.EF6.Tests.DM;
using Tanneryd.BulkOperations.EF6.Tests.DM.Companies;

namespace Tanneryd.BulkOperations.EF6.Tests.EF
{
    public class CompanyContext : DbContext
    {
        public DbSet<Company> Companies { get; set; }
        public DbSet<Employee> Employees { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Company>()
                .ToTable("Company")
                .HasKey(p => p.Id);
            modelBuilder.Entity<Company>()
                .Property(p => p.Id)
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            modelBuilder.Entity<Company>()
                .Property(p => p.Name)
                .IsRequired();
            modelBuilder.Entity<Company>()
                .HasMany(p => p.Employees)
                .WithRequired(p => p.Employer)
                .HasForeignKey(p => p.EmployerId);
            modelBuilder.Entity<Company>()
                .HasMany(p => p.Subsidiaries)
                .WithRequired(p => p.ParentCompany)
                .HasForeignKey(p => p.ParentCompanyId);

            modelBuilder.Entity<Employee>()
                .ToTable("Employee")
                .HasKey(p => p.Id);
            modelBuilder.Entity<Employee>()
                .Property(p => p.Id)
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            modelBuilder.Entity<Employee>()
                .Property(p => p.Name)
                .IsRequired();
        }
    }
}