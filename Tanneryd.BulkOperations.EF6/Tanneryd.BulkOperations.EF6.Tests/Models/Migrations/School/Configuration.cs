namespace Tanneryd.BulkOperations.EF6.Tests.Migrations.School
{
    using System;
    using System.Data.Entity;
    using System.Data.Entity.Migrations;
    using System.Linq;

    internal sealed class Configuration : DbMigrationsConfiguration<Tanneryd.BulkOperations.EF6.Tests.EF.SchoolContext>
    {
        public Configuration()
        {
            AutomaticMigrationsEnabled = false;
            ContextKey = "Tanneryd.BulkOperations.EF6.Tests.EF.SchoolContext";
        }

        protected override void Seed(Tanneryd.BulkOperations.EF6.Tests.EF.SchoolContext context)
        {
            //  This method will be called after migrating to the latest version.

            //  You can use the DbSet<T>.AddOrUpdate() helper extension method 
            //  to avoid creating duplicate seed data.
        }
    }
}
