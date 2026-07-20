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

using Microsoft.EntityFrameworkCore;

namespace Tanneryd.BulkOperations.EFCore.Tests.Models.EF.DiscriminatorOnly
{
    /// <summary>
    /// TPH hierarchy with only an identity PK plus discriminator column.
    /// Used to hit the MERGE branch that selects discriminator+rowno but
    /// previously aliased only rowno.
    /// </summary>
    public abstract class TagBase
    {
        public int Id { get; set; }
    }

    public class RedTag : TagBase
    {
    }

    public class BlueTag : TagBase
    {
    }

    public class DiscriminatorOnlyContext : DbContext
    {
        public DiscriminatorOnlyContext(DbContextOptions<DiscriminatorOnlyContext> options)
            : base(options)
        {
        }

        public DbSet<TagBase> Tags { get; set; }
        public DbSet<RedTag> RedTags { get; set; }
        public DbSet<BlueTag> BlueTags { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TagBase>(e =>
            {
                e.ToTable("DiscriminatorOnlyTag");
                e.HasKey(t => t.Id);
                e.Property(t => t.Id).UseIdentityColumn();
                e.HasDiscriminator<string>("TagType")
                    .HasValue<RedTag>("Red")
                    .HasValue<BlueTag>("Blue");
            });
        }
    }
}
