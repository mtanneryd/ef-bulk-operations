namespace Tanneryd.BulkOperations.EFCore.Tests
{
    public class Level1
    {
        public int Id { get; set; }
        public string Level1Name { get; set; }
        public Level2 Level2 { get; set; }
    }
}
