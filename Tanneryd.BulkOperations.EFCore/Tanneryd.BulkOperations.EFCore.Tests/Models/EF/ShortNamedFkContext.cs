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
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace Tanneryd.BulkOperations.EFCore.Tests.Models.EF.ShortNamedFk
{
    /// <summary>
    /// Minimal one-to-many model registered as shared-type entity types with short names
    /// (e.g. "Author") while ClrType.ToString() remains namespace-qualified.
    /// That mismatch is what MappingsExtractor H1 compares inconsistently.
    /// </summary>
    public class Author
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; }
        public ICollection<Book> Books { get; set; } = new List<Book>();
    }

    public class Book
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Title { get; set; }
        public Guid AuthorId { get; set; }
        public Author Author { get; set; }
    }

    public class ShortNamedFkContext : DbContext
    {
        public const string AuthorEntityName = "Author";
        public const string BookEntityName = "Book";

        public ShortNamedFkContext(DbContextOptions<ShortNamedFkContext> options)
            : base(options)
        {
        }

        public DbSet<Author> Authors => Set<Author>(AuthorEntityName);
        public DbSet<Book> Books => Set<Book>(BookEntityName);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Shared-type short Names differ from ClrType.ToString(). Register both
            // types first, then wire the relationship by entity-type name.
            modelBuilder.SharedTypeEntity<Book>(BookEntityName, e =>
            {
                e.ToTable("H1Book");
                e.HasKey(x => x.Id);
                e.Property(x => x.Title).IsRequired().HasMaxLength(200);
                e.Property(x => x.AuthorId);
            });

            modelBuilder.SharedTypeEntity<Author>(AuthorEntityName, e =>
            {
                e.ToTable("H1Author");
                e.HasKey(x => x.Id);
                e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            });

            modelBuilder.Entity(AuthorEntityName)
                .HasMany(BookEntityName, nameof(Author.Books))
                .WithOne(nameof(Book.Author))
                .HasForeignKey(nameof(Book.AuthorId))
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
