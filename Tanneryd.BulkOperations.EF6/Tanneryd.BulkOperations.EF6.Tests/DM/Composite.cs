using System.Collections.Generic;

namespace Tanneryd.DM
{
    public class CompositePrime
    {
        public long CompositeId { get; set; }
        public long PrimeId { get; set; }
    }

    public class Composite
    {
        public Composite()
        {
            Primes = new HashSet<Prime>();
        }

        public long NumberId { get; set; }
        public Number Number { get; set; }

        public Updated Updated { get; set; }

        public ICollection<Prime> Primes { get; set; }

    }
}