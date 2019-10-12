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
using Tanneryd.BulkOperations.EF6.Tests.DM.Levels;

namespace Tanneryd.BulkOperations.EF6.Tests.EF
{
    public class LevelContext : DbContext
    {
        public DbSet<Level1> Levels { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Level1>()
                .ToTable("Level1")
                .HasKey(u => u.Id);
            modelBuilder.Entity<Level1>()
                .Property(u => u.Id)
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            modelBuilder.Entity<Level1>()
                .Property(p => p.Level1Name)
                .HasColumnName("Level1Name");

            modelBuilder.ComplexType<Level2>()
                .Property(p => p.Level2Name)
                .HasColumnName("Level2Name");

            modelBuilder.ComplexType<Level3>()
                .Property(p => p.Level3Name)
                .HasColumnName("Level3Name");
        }
    }
}