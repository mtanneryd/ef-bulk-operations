namespace Tanneryd.BulkOperations.EF6.NET47.Tests.Migrations.SchoolContext
{
    using System;
    using System.Data.Entity;
    using System.Data.Entity.Migrations;
    using System.Linq;

    internal sealed class Configuration : DbMigrationsConfiguration<Tanneryd.BulkOperations.EF6.NET47.Tests.Models.EF.SchoolContext>
    {
        public Configuration()
        {
            AutomaticMigrationsEnabled = false;
            MigrationsDirectory = @".\Migrations\SchoolContext";
        }

        protected override void Seed(Tanneryd.BulkOperations.EF6.NET47.Tests.Models.EF.SchoolContext context)
        {
            //  This method will be called after migrating to the latest version.

            //  You can use the DbSet<T>.AddOrUpdate() helper extension method
            //  to avoid creating duplicate seed data.
        }
    }
}
