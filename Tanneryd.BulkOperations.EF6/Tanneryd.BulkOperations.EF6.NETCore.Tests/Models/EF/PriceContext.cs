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
using Tanneryd.BulkOperations.EF6.NETCore.Tests.Models.DM.Prices;

namespace Tanneryd.BulkOperations.EF6.NETCore.Tests.Models.EF
{
    public class PriceContext : DbContext
    {
        public DbSet<Price> Prices { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Price>()
                .ToTable("Price")
                .HasKey(u => u.Id);
            modelBuilder.Entity<Price>()
                .Property(u => u.Id)
                .HasColumnName("Id")
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            modelBuilder.Entity<Price>()
                .Property(u => u.Date)
                .IsRequired();
            modelBuilder.Entity<Price>()
                .Property(u => u.Name)
                .IsRequired();
            modelBuilder.Entity<Price>()
                .Property(u => u.Value)
                .IsOptional();
        }
    }
}