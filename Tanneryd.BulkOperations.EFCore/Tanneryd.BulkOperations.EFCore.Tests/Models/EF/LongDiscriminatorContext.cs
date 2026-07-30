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

using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace Tanneryd.BulkOperations.EFCore.Tests.Models.EF.LongDiscriminator
{
    /// <summary>
    /// TPH hierarchy whose custom string discriminator values exceed 128
    /// characters. The temp-table staging column used to be hard-coded to
    /// nvarchar(128), silently truncating such values and breaking MERGE
    /// matching. The column type must come from the model's store type.
    /// </summary>
    public abstract class LongTagBase
    {
        public int Id { get; set; }
    }

    public class LongRedTag : LongTagBase
    {
    }

    public class LongBlueTag : LongTagBase
    {
    }

    public class LongDiscriminatorContext : DbContext
    {
        // 150 chars each - longer than the old nvarchar(128) staging column.
        public static readonly string RedDiscriminator =
            "Red-" + string.Concat(Enumerable.Repeat("R", 146));

        public static readonly string BlueDiscriminator =
            "Blue-" + string.Concat(Enumerable.Repeat("B", 145));

        public LongDiscriminatorContext(DbContextOptions<LongDiscriminatorContext> options)
            : base(options)
        {
        }

        public DbSet<LongTagBase> Tags { get; set; }
        public DbSet<LongRedTag> RedTags { get; set; }
        public DbSet<LongBlueTag> BlueTags { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<LongTagBase>(e =>
            {
                e.ToTable("LongDiscriminatorTag");
                e.HasKey(t => t.Id);
                e.Property(t => t.Id).UseIdentityColumn();
                e.HasDiscriminator<string>("TagType")
                    .HasValue<LongRedTag>(RedDiscriminator)
                    .HasValue<LongBlueTag>(BlueDiscriminator);
            });
        }
    }
}
