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
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Tanneryd.BulkOperations.EFCore.Tests.Models.EF.VariantModel
{
    /// <summary>
    /// Same context CLR type can map <see cref="Widget"/> to different tables
    /// when <see cref="TableName"/> differs (custom model cache key).
    /// </summary>
    public class Widget
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class VariantTableContext : DbContext
    {
        public VariantTableContext(DbContextOptions<VariantTableContext> options, string tableName)
            : base(options)
        {
            TableName = tableName;
        }

        public string TableName { get; }

        public DbSet<Widget> Widgets { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Widget>(e =>
            {
                e.ToTable(TableName);
                e.HasKey(w => w.Id);
                e.Property(w => w.Id).UseIdentityColumn();
            });
        }
    }

    /// <summary>
    /// EF Core defaults to caching the model by context CLR type only; include
    /// <see cref="VariantTableContext.TableName"/> so each variant gets its own model.
    /// </summary>
    public class VariantTableModelCacheKeyFactory : IModelCacheKeyFactory
    {
        public object Create(DbContext context, bool designTime)
        {
            var variant = (VariantTableContext)context;
            return (context.GetType(), variant.TableName, designTime);
        }
    }
}
