namespace Tanneryd.BulkOperations.EF6.Tests.DM.Companies
{
    public class Employee
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public long EmployerId { get; set; }
        public Company Employer { get; set; }
    }
}