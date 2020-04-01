using System.Data.Entity.Migrations;

namespace Tanneryd.BulkOperations.EF6.NETCore.Tests.Migrations.InvoiceContext
{
    internal sealed class Configuration : DbMigrationsConfiguration<Models.EF.InvoiceContext>
    {
        public Configuration()
        {
            AutomaticMigrationsEnabled = false;
            MigrationsDirectory = @"Migrations\InvoiceContext";
            ContextKey = @"Tanneryd.BulkOperations.EF6.NETCore.Tests.Models.EF.InvoiceContext";
        }

        protected override void Seed(Models.EF.InvoiceContext context)
        {
            //  This method will be called after migrating to the latest version.

            //  You can use the DbSet<T>.AddOrUpdate() helper extension method
            //  to avoid creating duplicate seed data.
        }
    }
}
