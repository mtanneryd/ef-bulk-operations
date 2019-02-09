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

using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity;
using Tanneryd.BulkOperations.EF6.Tests.DM;
using Tanneryd.BulkOperations.EF6.Tests.DM.People;

namespace Tanneryd.BulkOperations.EF6.Tests.EF
{
    public class PeopleContext : DbContext
    {
        public DbSet<Person> People { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            #region Person

            modelBuilder.Entity<Person>()
                .ToTable("Person")
                .HasKey(p => p.Id);
            modelBuilder.Entity<Person>()
                .Property(p => p.Id)
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            modelBuilder.Entity<Person>()
                .Property(p => p.FirstName)
                .IsRequired();
            modelBuilder.Entity<Person>()
                .Property(p => p.LastName)
                .IsRequired();
            modelBuilder.Entity<Person>()
                .Property(p => p.BirthDate)
                .IsRequired();

            modelBuilder.Entity<Person>()
                .HasMany(p => p.Children)
                .WithOptional(p => p.Mother)
                .HasForeignKey(p => p.MotherId);

            #endregion
        }
    }
 }