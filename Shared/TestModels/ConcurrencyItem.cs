namespace Tanneryd.BulkOperations.TestModels
{
    /// <summary>
    /// Entity with a SQL Server rowversion concurrency token (GitHub issue #34).
    /// Shared by EF6 and EF Core test projects.
    /// </summary>
    public class ConcurrencyItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public byte[] RowVersion { get; set; }
    }
}
