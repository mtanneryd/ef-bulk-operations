namespace Tanneryd.BulkOperations.EF6.Tests.Migrations.Invoice
{
    using System;
    using System.Data.Entity;
    using System.Data.Entity.Migrations;
    using System.Linq;

    internal sealed class Configuration : DbMigrationsConfiguration<Tanneryd.BulkOperations.EF6.Tests.EF.InvoiceContext>
    {
        public Configuration()
        {
            AutomaticMigrationsEnabled = false;
            ContextKey = "Tanneryd.BulkOperations.EF6.Tests.EF.InvoiceContext";
        }

        protected override void Seed(Tanneryd.BulkOperations.EF6.Tests.EF.InvoiceContext context)
        {
            //  This method will be called after migrating to the latest version.

            //  You can use the DbSet<T>.AddOrUpdate() helper extension method
            //  to avoid creating duplicate seed data.
        }
    }
}
