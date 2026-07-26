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

namespace Tanneryd.BulkOperations.EFCore.Tests.Models.EF.ClientOnAdd
{
    /// <summary>
    /// Non-PK ValueGeneratedOnAdd without a store default — client-supplied
    /// values must still be mapped for bulk insert.
    /// </summary>
    public class ClientOnAddProbe
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public DateTime? CreatedAt { get; set; }
    }

    public class ClientOnAddContext : DbContext
    {
        public const string ConnectionString =
            @"data source=(localdb)\MSSQLLocalDB;initial catalog=Tanneryd.BulkOperations.EFCore.Tests.ClientOnAdd;persist security info=True;Integrated Security=SSPI;MultipleActiveResultSets=True;TrustServerCertificate=true";

        public ClientOnAddContext(DbContextOptions<ClientOnAddContext> options)
            : base(options)
        {
        }

        public DbSet<ClientOnAddProbe> Probes { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ClientOnAddProbe>(e =>
            {
                e.ToTable("ClientOnAddProbe");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).UseIdentityColumn();
                e.Property(x => x.Name).HasMaxLength(100).IsRequired();
                // Bare OnAdd: no HasDefaultValueSql — value comes from the client.
                e.Property(x => x.CreatedAt).ValueGeneratedOnAdd();
            });
        }
    }
}
