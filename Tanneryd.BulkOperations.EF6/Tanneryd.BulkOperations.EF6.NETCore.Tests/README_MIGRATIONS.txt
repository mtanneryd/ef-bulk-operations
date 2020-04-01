Enable-Migrations -MigrationsDirectory .\Migrations\InvoiceContext -ContextTypeName Tanneryd.BulkOperations.EF6.NETCore.Tests.Models.EF.InvoiceContext -ProjectName Tanneryd.BulkOperations.EF6.NETCore.Tests
Add-Migration -configuration Tanneryd.BulkOperations.EF6.NETCore.Tests.Migrations.InvoiceContext.Configuration -ProjectName Tanneryd.BulkOperations.EF6.NETCore.Tests InitialMigration

Enable-Migrations -MigrationsDirectory .\Migrations\SchoolContext -ContextTypeName Tanneryd.BulkOperations.EF6.NETCore.Tests.Models.EF.SchoolContext -ProjectName Tanneryd.BulkOperations.EF6.NETCore.Tests
Add-Migration -configuration Tanneryd.BulkOperations.EF6.NETCore.Tests.Migrations.SchoolContext.Configuration -ProjectName Tanneryd.BulkOperations.EF6.NETCore.Tests InitialMigration

Enable-Migrations -MigrationsDirectory .\Migrations\InvoiceContext -ContextTypeName Tanneryd.BulkOperations.EF6.NET47.Tests.Models.EF.InvoiceContext -ProjectName Tanneryd.BulkOperations.EF6.NET47.Tests
Add-Migration -configuration Tanneryd.BulkOperations.EF6.NET47.Tests.Migrations.InvoiceContext.Configuration -ProjectName Tanneryd.BulkOperations.EF6.NET47.Tests InitialMigration

Enable-Migrations -MigrationsDirectory .\Migrations\SchoolContext -ContextTypeName Tanneryd.BulkOperations.EF6.NET47.Tests.Models.EF.SchoolContext -ProjectName Tanneryd.BulkOperations.EF6.NET47.Tests
Add-Migration -configuration Tanneryd.BulkOperations.EF6.NET47.Tests.Migrations.SchoolContext.Configuration -ProjectName Tanneryd.BulkOperations.EF6.NET47.Tests InitialMigration