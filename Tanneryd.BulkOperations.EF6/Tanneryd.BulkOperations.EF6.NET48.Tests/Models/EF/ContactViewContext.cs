/*
* Copyright ©  2017-2025 Tånneryd IT AB
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

using System.Data.Entity;
using Tanneryd.BulkOperations.EF6.NET48.Tests.Models.DM.People;

namespace Tanneryd.BulkOperations.EF6.NET48.Tests.Models.EF
{
    /// <summary>
    /// Maps only the Contact view. Uses the same database as UnitTestContext but is kept
    /// separate so adding the view mapping does not require a new EF6 code migration.
    /// </summary>
    public class ContactViewContext : DbContext
    {
        static ContactViewContext()
        {
            Database.SetInitializer<ContactViewContext>(null);
        }

        public ContactViewContext()
            : base("name=UnitTestContext")
        {
        }

        public DbSet<Contact> Contacts { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Contact>()
                .ToTable("Contact", "dbo")
                .HasKey(c => new { c.FirstName, c.LastName });
            modelBuilder.Entity<Contact>()
                .Property(c => c.FirstName)
                .IsRequired();
            modelBuilder.Entity<Contact>()
                .Property(c => c.LastName)
                .IsRequired();
        }
    }
}
