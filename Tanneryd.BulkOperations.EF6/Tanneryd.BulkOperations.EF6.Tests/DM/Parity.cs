using System;
using System.Collections.Generic;

namespace Tanneryd.DM
{
    public class Parity
    {
        public Parity()
        {
            Numbers = new HashSet<Number>();   
        }
        public int Id { get; set; }
        public string Name { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string UpdatedBy { get; set; }
        public ICollection<Number> Numbers { get; set; }
    }
}