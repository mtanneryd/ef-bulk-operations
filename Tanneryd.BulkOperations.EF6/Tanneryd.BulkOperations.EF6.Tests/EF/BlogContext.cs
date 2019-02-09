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

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity;
using Tanneryd.BulkOperations.EF6.Tests.DM.Blog;

namespace Tanneryd.BulkOperations.EF6.Tests.EF
{
    

    public class BlogContext : DbContext
    {
        public DbSet<Blog> Blogs { get; set; }
        public DbSet<Post> Posts { get; set; }
        public DbSet<Keyword> Keywords { get; set; }
        public DbSet<Visitor> Visitors { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Blog>()
                .ToTable("Blog")
                .HasKey(p => p.Id);
            modelBuilder.Entity<Blog>()
                .Property(p => p.Id)
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            modelBuilder.Entity<Blog>()
                .Property(p => p.Name)
                .IsRequired();
            modelBuilder.Entity<Blog>()
                .HasMany(b => b.BlogPosts)
                .WithRequired(p => p.Blog)
                .HasForeignKey(p => p.BlogId);

            modelBuilder.Entity<Post>()
                .ToTable("Post")
                .HasKey(p => p.Id);
            modelBuilder.Entity<Post>()
                .Property(p => p.Id)
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            modelBuilder.Entity<Post>()
                .Property(p => p.Text)
                .IsRequired();
            modelBuilder.Entity<Post>()
                .HasMany(p => p.PostKeywords)
                .WithRequired(k => k.Post)
                .HasForeignKey(k => k.PostId);

            modelBuilder.Entity<Keyword>()
                .ToTable("Keyword")
                .HasKey(p => p.Id);
            modelBuilder.Entity<Keyword>()
                .Property(p => p.Id)
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            modelBuilder.Entity<Keyword>()
                .Property(p => p.Text)
                .IsRequired();            

            modelBuilder.Entity<Visitor>()
                .ToTable("Visitor")
                .HasKey(p => p.Id);
            modelBuilder.Entity<Visitor>()
                .Property(p => p.Id)
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            modelBuilder.Entity<Visitor>()
                .Property(p => p.Name)
                .IsRequired();  
            modelBuilder.Entity<Visitor>()
                .HasMany(a => a.Posts)
                .WithMany(a=>a.Visitors)
                .Map(x =>
                {
                    x.MapLeftKey("VisitorId");
                    x.MapRightKey("PostId");
                    x.ToTable("VisitorPosts");
                });
        }
    }
}