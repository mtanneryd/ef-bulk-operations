/*
* Copyright ©  2017-2018 Tånneryd IT AB
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

namespace Tanneryd.BulkOperations.EF6.Tests.DM.Blog
{
    public class Blog
    {
        public Blog()
        {
            BlogPosts = new HashSet<Post>();
        }
        public Guid Id { get; set; }
        public string Name { get; set; }
        public virtual ICollection<Post> BlogPosts { get; set; }
    }

    public class Post
    {
        public Post()
        {
            Visitors = new HashSet<Visitor>();
        }

        public Guid Id { get; set; }
        public Guid BlogId { get; set; }
        public Blog Blog { get; set; }
        public string Text { get; set; }
        public ICollection<Keyword> PostKeywords { get; set; }
        public ICollection<Visitor> Visitors { get; set; }
    }

    public class Keyword
    {
        public Guid Id { get; set; }
        public Guid PostId { get; set; }
        public Post Post { get; set; }
        public string Text { get; set; }
    }

    public class Visitor
    {
        public Visitor()
        {
            Posts = new HashSet<Post>();
        }
        public Guid Id { get; set; }
        public string Name { get; set; }
        public ICollection<Post> Posts { get; set; }
    }
}
