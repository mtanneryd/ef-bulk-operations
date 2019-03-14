using System.Collections.Generic;

namespace Tanneryd.BulkOperations.EF6.Tests.DM.Miscellaneous
{
    public class Coordinate
    {
        public int Id { get; set; }
        public int Value { get; set; }

        public ICollection<Point> XCoordinatePoints { get; set; }
        public ICollection<Point> YCoordinatePoints { get; set; }
    }
}