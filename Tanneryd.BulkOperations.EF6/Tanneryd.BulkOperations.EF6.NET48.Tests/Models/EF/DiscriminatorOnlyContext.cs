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

using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity;
using System.Data.Entity.ModelConfiguration;

namespace Tanneryd.BulkOperations.EF6.NET48.Tests.Models.EF.DiscriminatorOnly
{
    /// <summary>
    /// TPH hierarchy with only an identity PK plus discriminator column.
    /// Used to hit the MERGE branch that selects discriminator+rowno but
    /// aliases only rowno (review finding H3).
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
        public const string ConnectionString =
            @"data source=(localdb)\MSSQLLocalDB;initial catalog=Tanneryd.BulkOperations.EF6.NET48.Tests.DiscriminatorOnly;persist security info=True;Integrated Security=SSPI;MultipleActiveResultSets=True;TrustServerCertificate=true";

        public DiscriminatorOnlyContext()
            : base(ConnectionString)
        {
        }

        public DbSet<TagBase> Tags { get; set; }
        public DbSet<RedTag> RedTags { get; set; }
        public DbSet<BlueTag> BlueTags { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Configurations.Add(new TagBaseConfiguration());
            base.OnModelCreating(modelBuilder);
        }
    }

    public class TagBaseConfiguration : EntityTypeConfiguration<TagBase>
    {
        public TagBaseConfiguration()
        {
            ToTable("H3Tag");
            HasKey(t => t.Id);
            Property(t => t.Id)
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);

            Map<RedTag>(m => m.Requires("TagType").HasValue("Red"));
            Map<BlueTag>(m => m.Requires("TagType").HasValue("Blue"));
        }
    }
}
