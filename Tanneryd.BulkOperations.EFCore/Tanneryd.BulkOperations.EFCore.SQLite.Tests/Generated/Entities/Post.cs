// <auto-generated>

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Tanneryd.BulkOperations.EFCore.SQLite.Tests
{
    public class Post
    {
        public string Id { get; set; }
        public string BlogId { get; set; }
        public string Text { get; set; }

        // Reverse navigation
        public ICollection<Keyword> Keywords { get; set; }

        public Blog Blog { get; set; }

        public Post()
        {
            Keywords = new List<Keyword>();
        }
    }

}
// </auto-generated>
