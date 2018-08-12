namespace Tanneryd.BulkOperations.EF6.Tests.DM.Miscellaneous
{
    public class Point
    {
        public int XCoordinateId { get; set; }
        public Coordinate XCoordinate { get; set; }
        public int YCoordinateId { get; set; }
        public Coordinate YCoordinate { get; set; }

        public double Value { get; set; }

    }
}