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
using Tanneryd.BulkOperations.EF6.NETCore.Tests.Models.DM.Miscellaneous;

namespace Tanneryd.BulkOperations.EF6.NETCore.Tests.Models.EF
{
    public class MiscellaneousContext : DbContext
    {
        public DbSet<ReservedSqlKeyword> ReservedSqlKeywords { get; set; }
        public DbSet<Coordinate> Coordinates { get; set; }
        public DbSet<Point> Points { get; set; }
        public DbSet<EmptyTable> EmptyTables { get; set; }


        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<EmptyTable>()
                .ToTable("EmptyTable")
                .HasKey(p => p.Id);
            modelBuilder.Entity<EmptyTable>()
                .Property(p => p.Id)
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);

            modelBuilder.Entity<ReservedSqlKeyword>()
                .ToTable("ReservedSqlKeyword")
                .HasKey(p => p.Id);
            modelBuilder.Entity<ReservedSqlKeyword>()
                .Property(p => p.Id)
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            modelBuilder.Entity<ReservedSqlKeyword>()
                .Property(p => p.Key)
                .IsOptional();
            modelBuilder.Entity<ReservedSqlKeyword>()
                .Property(p => p.Select)
                .IsOptional();
            modelBuilder.Entity<ReservedSqlKeyword>()
                .Property(p => p.Identity)
                .IsOptional();

            modelBuilder.Entity<Coordinate>()
                .ToTable("Coordinate")
                .HasKey(p => p.Id);
            modelBuilder.Entity<Coordinate>()
                .Property(p => p.Id)
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            modelBuilder.Entity<Coordinate>()
                .Property(p => p.Value)
                .IsRequired();
            modelBuilder.Entity<Coordinate>()
                .HasMany(e => e.XCoordinatePoints)
                .WithRequired(e => e.XCoordinate)
                .HasForeignKey(e => e.XCoordinateId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<Coordinate>()
                .HasMany(e => e.YCoordinatePoints)
                .WithRequired(e => e.YCoordinate)
                .HasForeignKey(e => e.YCoordinateId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Point>()
                .ToTable("Point")
                .HasKey(p => new { p.XCoordinateId, p.YCoordinateId});
            modelBuilder.Entity<Point>()
                .Property(p => p.XCoordinateId)
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.None)
                .HasColumnOrder(0);
            modelBuilder.Entity<Point>()
                .Property(p => p.YCoordinateId)
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.None)
                .HasColumnOrder(1);
            modelBuilder.Entity<Point>()
                .Property(e => e.Value)
                .IsRequired();
        }
    }
}