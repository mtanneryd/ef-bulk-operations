using System;

namespace Tanneryd.DM
{
    public class Level1
    {
        public int Id { get; set; }
        public Level2 Level2 { get; set; }
    }

    public class Level2
    {
        public string Level2Name { get; set; }
        public Level3 Level3 { get; set; }
    }

    public class Level3
    {
        public string Level3Name { get; set; }
        public DateTime Updated { get; set; }
    }
}