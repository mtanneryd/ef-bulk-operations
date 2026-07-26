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

using System;
using Microsoft.EntityFrameworkCore;

namespace Tanneryd.BulkOperations.EFCore.Tests.Models.EF.ComplexAssignedPk
{
    public class AssignedPkLocation
    {
        public string City { get; set; }
    }

    /// <summary>
    /// Client-assigned Guid PK plus a complex property — forces the
    /// select-not-existing insert path after Expando flatten.
    /// </summary>
    public class ComplexAssignedPkProbe
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public AssignedPkLocation Location { get; set; }
    }

    public class ComplexAssignedPkContext : DbContext
    {
        public const string ConnectionString =
            @"data source=(localdb)\MSSQLLocalDB;initial catalog=Tanneryd.BulkOperations.EFCore.Tests.ComplexAssignedPk;persist security info=True;Integrated Security=SSPI;MultipleActiveResultSets=True;TrustServerCertificate=true";

        public ComplexAssignedPkContext(DbContextOptions<ComplexAssignedPkContext> options)
            : base(options)
        {
        }

        public DbSet<ComplexAssignedPkProbe> Probes { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ComplexAssignedPkProbe>(e =>
            {
                e.ToTable("ComplexAssignedPkProbe");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).ValueGeneratedNever();
                e.Property(x => x.Name).HasMaxLength(100).IsRequired();
                e.ComplexProperty(x => x.Location, location =>
                {
                    location.IsRequired();
                    location.Property(l => l.City).HasColumnName("City").HasMaxLength(100).IsRequired();
                });
            });
        }
    }
}
