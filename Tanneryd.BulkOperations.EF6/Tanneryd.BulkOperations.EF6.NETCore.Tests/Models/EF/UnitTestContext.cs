using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity;
using Tanneryd.BulkOperations.EF6.NETCore.Tests.Models.DM.Blog;
using Tanneryd.BulkOperations.EF6.NETCore.Tests.Models.DM.Companies;
using Tanneryd.BulkOperations.EF6.NETCore.Tests.Models.DM.Invoice;
using Tanneryd.BulkOperations.EF6.NETCore.Tests.Models.DM.Levels;
using Tanneryd.BulkOperations.EF6.NETCore.Tests.Models.DM.Miscellaneous;
using Tanneryd.BulkOperations.EF6.NETCore.Tests.Models.DM.Numbers;
using Tanneryd.BulkOperations.EF6.NETCore.Tests.Models.DM.People;
using Tanneryd.BulkOperations.EF6.NETCore.Tests.Models.DM.Prices;
using Tanneryd.BulkOperations.EF6.NETCore.Tests.Models.DM.Report;
using Tanneryd.BulkOperations.EF6.NETCore.Tests.Models.DM.School;
using Tanneryd.BulkOperations.EF6.NETCore.Tests.Models.DM.Logs;


namespace Tanneryd.BulkOperations.EF6.NETCore.Tests.Models.EF
{
    public class UnitTestContext : DbContext
    {
        #region Blog

        public DbSet<Blog> Blogs { get; set; }
        public DbSet<Post> Posts { get; set; }
        public DbSet<Keyword> Keywords { get; set; }
        public DbSet<Visitor> Visitors { get; set; }

        #endregion

        #region Company

        public DbSet<Company> Companies { get; set; }
        public DbSet<Employee> Employees { get; set; }

        #endregion

        #region Invoice

        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<Journal> Journals { get; set; }
        public DbSet<BatchInvoice> BatchInvoices { get; set; }
        public DbSet<InvoiceItem> InvoiceItems { get; set; }
        public DbSet<BatchInvoiceItem> BatchInvoiceItems { get; set; }

        #endregion

        #region Levels

        public DbSet<Level1> Levels { get; set; }

        #endregion

        #region Miscellaneous

        public DbSet<ReservedSqlKeyword> ReservedSqlKeywords { get; set; }
        public DbSet<Coordinate> Coordinates { get; set; }
        public DbSet<Point> Points { get; set; }
        public DbSet<EmptyTable> EmptyTables { get; set; }

        #endregion

        #region Number

        public DbSet<Parity> Parities { get; set; }
        public DbSet<Number> Numbers { get; set; }
        public DbSet<Prime> Primes { get; set; }
        public DbSet<Composite> Composites { get; set; }

        #endregion

        #region People

        public DbSet<Person> People { get; set; }

        #endregion

        #region Price

        public DbSet<Price> Prices { get; set; }

        #endregion

        #region Report

        public DbSet<Period> Periods { get; set; }
        public DbSet<SummaryReportFROMTableASExtent> SummaryReports { get; set; }
        public DbSet<DetailReportWithFROM> DetailReports { get; set; }

        #endregion

        #region School

        public DbSet<Course> Courses { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<Instructor> Instructors { get; set; }
        public DbSet<OfficeAssignment> OfficeAssignments { get; set; }

        #endregion

        #region Team

        public DbSet<DM.Teams.UsingDbGeneratedGuidKeys.TeamWithDbGeneratedGuidKey> TeamsWithDbGeneratedGuids { get; set; }
        public DbSet<DM.Teams.UsingDbGeneratedGuidKeys.PlayerWithDbGeneratedGuidKey> PlayersWithDbGeneratedGuids { get; set; }
        public DbSet<DM.Teams.UsingDbGeneratedGuidKeys.CoachWithDbGeneratedGuidKey> CoachesWithDbGeneratedGuids { get; set; }

        public DbSet<DM.Teams.UsingUserGeneratedGuidKeys.TeamWithUserGeneratedGuidKey> TeamsWithUserGeneratedGuids { get; set; }
        public DbSet<DM.Teams.UsingUserGeneratedGuidKeys.PlayerWithUserGeneratedGuidKey> PlayersWithUserGeneratedGuids  { get; set; }
        public DbSet<DM.Teams.UsingUserGeneratedGuidKeys.CoachWithUserGeneratedGuidKey> CoachesWithUserGeneratedGuids  { get; set; }

        #endregion

        public DbSet<LogItem> LogItems { get; set; }
        public DbSet<LogWarning> LogWarnings { get; set; }
        public DbSet<LogError> LogErrors { get; set; }

        public UnitTestContext()
            :base("data source =(localdb)\\MSSQLLocalDB; initial catalog = Tanneryd.BulkOperations.EF6.NETCore.Tests.Models.EF.UnitTestContext; persist security info=True;Integrated Security = SSPI; MultipleActiveResultSets=True")
            //:base("data source =.\\SQLEXPRESS; initial catalog = Tanneryd.BulkOperations.EF6.NETCore.Tests.Models.EF.UnitTestContext; persist security info=True;Integrated Security = SSPI; MultipleActiveResultSets=True")
        {

        }
        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {            
            #region Blog

            modelBuilder.Entity<Blog>()
                .ToTable("Blog")
                .HasKey(p => p.Id);
            modelBuilder.Entity<Blog>()
                .Property(p => p.Id)
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            modelBuilder.Entity<Blog>()
                .Property(p => p.Name)
                .IsRequired();
            modelBuilder.Entity<Blog>()
                .HasMany(b => b.BlogPosts)
                .WithRequired(p => p.Blog)
                .HasForeignKey(p => p.BlogId);

            modelBuilder.Entity<Post>()
                .ToTable("Post")
                .HasKey(p => p.Id);
            modelBuilder.Entity<Post>()
                .Property(p => p.Id)
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            modelBuilder.Entity<Post>()
                .Property(p => p.Text)
                .IsRequired();
            modelBuilder.Entity<Post>()
                .HasMany(p => p.PostKeywords)
                .WithRequired(k => k.Post)
                .HasForeignKey(k => k.PostId);

            modelBuilder.Entity<Keyword>()
                .ToTable("Keyword")
                .HasKey(p => p.Id);
            modelBuilder.Entity<Keyword>()
                .Property(p => p.Id)
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            modelBuilder.Entity<Keyword>()
                .Property(p => p.Text)
                .IsRequired();

            modelBuilder.Entity<Visitor>()
                .ToTable("Visitor")
                .HasKey(p => p.Id);
            modelBuilder.Entity<Visitor>()
                .Property(p => p.Id)
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            modelBuilder.Entity<Visitor>()
                .Property(p => p.Name)
                .IsRequired();
            modelBuilder.Entity<Visitor>()
                .HasMany(a => a.Posts)
                .WithMany(a => a.Visitors)
                .Map(x =>
                {
                    x.MapLeftKey("VisitorId");
                    x.MapRightKey("PostId");
                    x.ToTable("VisitorPosts");
                });

            #endregion

            #region Company

            modelBuilder.Entity<Company>()
                .ToTable("Company")
                .HasKey(p => p.Id);
            modelBuilder.Entity<Company>()
                .Property(p => p.Id)
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            modelBuilder.Entity<Company>()
                .Property(p => p.Name)
                .IsRequired();
            modelBuilder.Entity<Company>()
                .HasMany(p => p.Employees)
                .WithRequired(p => p.Employer)
                .HasForeignKey(p => p.EmployerId);
            modelBuilder.Entity<Company>()
                .HasMany(p => p.Subsidiaries)
                .WithRequired(p => p.ParentCompany)
                .HasForeignKey(p => p.ParentCompanyId);

            modelBuilder.Entity<Employee>()
                .ToTable("Employee")
                .HasKey(p => p.Id);
            modelBuilder.Entity<Employee>()
                .Property(p => p.Id)
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            modelBuilder.Entity<Employee>()
                .Property(p => p.Name)
                .IsRequired();

            #endregion

            #region Invoice

            modelBuilder.Entity<BatchInvoiceItem>()
                .ToTable("BatchInvoiceItem")
                .HasKey(p => p.Id);
            modelBuilder.Entity<BatchInvoiceItem>()
                .Property(p => p.Id)
                .HasColumnName("PrimaryKey");

            modelBuilder.Entity<InvoiceItem>()
                .ToTable("InvoiceItem")
                .HasKey(p => p.Id);
            modelBuilder.Entity<InvoiceItem>()
                .Property(p => p.Id)
                .HasColumnName("PrimaryKey");
            modelBuilder.Entity<InvoiceItem>()
                .Property(p => p.Id)
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);

            modelBuilder.Entity<Invoice>()
                .ToTable("Invoice")
                .HasKey(p => p.Id);
            modelBuilder.Entity<Invoice>()
                .Property(p => p.Id)
                .HasColumnName("PrimaryKey");
            modelBuilder.Entity<Invoice>()
                .HasMany(b => b.BatchInvoices)
                .WithRequired(p => p.Invoice)
                .HasForeignKey(p => p.InvoiceId);
            modelBuilder.Entity<Invoice>()
                .HasMany(b => b.Journals)
                .WithRequired(p => p.Invoice)
                .HasForeignKey(p => p.InvoiceId);
            modelBuilder.Entity<Invoice>()
                .Property(b => b.Tax)
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Computed);

            modelBuilder.Entity<BatchInvoice>()
                .ToTable("BatchInvoice")
                .HasKey(p => p.Id);
            modelBuilder.Entity<BatchInvoice>()
                .Property(p => p.Id)
                .HasColumnName("PrimaryKey");
            modelBuilder.Entity<BatchInvoice>()
                .HasMany(p => p.Invoices)
                .WithRequired(k => k.BatchInvoice)
                .HasForeignKey(k => k.BatchInvoiceId);

            modelBuilder.Entity<Journal>()
                .ToTable("Journal")
                .HasKey(p => p.Id);
            modelBuilder.Entity<Journal>()
                .Property(p => p.Id)
                .HasColumnName("PrimaryKey");
            modelBuilder.Entity<Journal>()
                .HasMany(p => p.Invoices)
                .WithRequired(k => k.Journal)
                .HasForeignKey(k => k.JournalId);

            #endregion

            #region Levels

            modelBuilder.Entity<Level1>()
                .ToTable("Level1")
                .HasKey(u => u.Id);
            modelBuilder.Entity<Level1>()
                .Property(u => u.Id)
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            modelBuilder.Entity<Level1>()
                .Property(p => p.Level1Name)
                .HasColumnName("Level1Name");

            modelBuilder.ComplexType<Level2>()
                .Property(p => p.Level2Name)
                .HasColumnName("Level2Name");

            modelBuilder.ComplexType<Level3>()
                .Property(p => p.Level3Name)
                .HasColumnName("Level3Name");

            #endregion

            #region Miscellaneous

            modelBuilder.Entity<EmptyTable>()
                .ToTable("EmptyTable")
                .HasKey(p => p.Id);
            modelBuilder.Entity<EmptyTable>()
                .Property(p => p.Id)
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);

            modelBuilder.Entity<ReservedSqlKeyword>()
                .ToTable("ReservedSqlKeyword")
                .HasKey(p => p.Id);
            modelBuilder.Entity<ReservedSqlKeyword>()
                .Property(p => p.Id)
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            modelBuilder.Entity<ReservedSqlKeyword>()
                .Property(p => p.Key)
                .IsOptional();
            modelBuilder.Entity<ReservedSqlKeyword>()
                .Property(p => p.Select)
                .IsOptional();
            modelBuilder.Entity<ReservedSqlKeyword>()
                .Property(p => p.Identity)
                .IsOptional();

            modelBuilder.Entity<Coordinate>()
                .ToTable("Coordinate")
                .HasKey(p => p.Id);
            modelBuilder.Entity<Coordinate>()
                .Property(p => p.Id)
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            modelBuilder.Entity<Coordinate>()
                .Property(p => p.Value)
                .IsRequired();
            modelBuilder.Entity<Coordinate>()
                .HasMany(e => e.XCoordinatePoints)
                .WithRequired(e => e.XCoordinate)
                .HasForeignKey(e => e.XCoordinateId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<Coordinate>()
                .HasMany(e => e.YCoordinatePoints)
                .WithRequired(e => e.YCoordinate)
                .HasForeignKey(e => e.YCoordinateId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Point>()
                .ToTable("Point")
                .HasKey(p => new { p.XCoordinateId, p.YCoordinateId });
            modelBuilder.Entity<Point>()
                .Property(p => p.XCoordinateId)
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.None)
                .HasColumnOrder(0);
            modelBuilder.Entity<Point>()
                .Property(p => p.YCoordinateId)
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.None)
                .HasColumnOrder(1);
            modelBuilder.Entity<Point>()
                .Property(e => e.Value)
                .IsRequired();

            #endregion

            #region Number

            modelBuilder.Entity<Parity>()
                    .ToTable("Parity")
                    .HasKey(u => u.Id);
            modelBuilder.Entity<Parity>()
                .Property(u => u.Id)
                .HasColumnName("Key")
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            modelBuilder.Entity<Parity>()
                .Property(u => u.Name)
                .IsRequired();
            modelBuilder.Entity<Parity>()
                .Property(p => p.UpdatedAt)
                .HasColumnName("UpdatedAt");
            modelBuilder.Entity<Parity>()
                .Property(p => p.UpdatedBy)
                .HasColumnName("UpdatedBy");
            modelBuilder.Entity<Parity>()
                .HasMany(p => p.Numbers)
                .WithRequired(n => n.Parity)
                .HasForeignKey(n => n.ParityId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Number>()
                .ToTable("Number")
                .HasKey(u => u.Id);
            modelBuilder.Entity<Number>()
                .Property(u => u.Id)
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            modelBuilder.Entity<Number>()
                .Property(u => u.Value);
            modelBuilder.Entity<Number>()
                .Property(p => p.UpdatedAt)
                .HasColumnName("UpdatedAt");
            modelBuilder.Entity<Number>()
                .Property(p => p.UpdatedBy)
                .HasColumnName("UpdatedBy");

            modelBuilder.Entity<Prime>()
                .ToTable("Prime")
                .HasKey(u => u.NumberId);
            modelBuilder.Entity<Prime>()
                .HasRequired(t => t.Number)
                .WithOptional(t => t.Prime);
            modelBuilder.Entity<Prime>()
                .Property(p => p.UpdatedAt)
                .HasColumnName("UpdatedAt");
            modelBuilder.Entity<Prime>()
                .Property(p => p.UpdatedBy)
                .HasColumnName("UpdatedBy");

            modelBuilder.Entity<Composite>()
                .ToTable("Composite")
                .HasKey(u => u.NumberId);
            modelBuilder.Entity<Composite>()
                .Property(p => p.UpdatedAt)
                .HasColumnName("UpdatedAt");
            modelBuilder.Entity<Composite>()
                .Property(p => p.UpdatedBy)
                .HasColumnName("UpdatedBy");
            modelBuilder.Entity<Composite>()
                .HasRequired(t => t.Number)
                .WithOptional(t => t.Composite);
            modelBuilder.Entity<Composite>()
                .HasMany(e => e.Primes)
                .WithMany(e => e.Composites)
                .Map(m => m.ToTable("CompositePrime").MapLeftKey("CompositeId").MapRightKey("PrimeId"));

            #endregion

            #region People

            modelBuilder.Entity<Person>()
                .ToTable("Person")
                .HasKey<long>(p => p.Id);
            modelBuilder.Entity<Person>()
                .Property(p => p.Id)
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            modelBuilder.Entity<Person>()
                .Property(p => p.FirstName)
                .IsRequired();
            modelBuilder.Entity<Person>()
                .Property(p => p.EmployeeNumber)
                .IsOptional();
            modelBuilder.Entity<Person>()
                .Property(p => p.LastName)
                .IsRequired();
            modelBuilder.Entity<Person>()
                .Property(p => p.BirthDate)
                .IsRequired();

            modelBuilder.Entity<Person>()
                .HasMany(p => p.Children)
                .WithOptional(p => p.Mother)
                .HasForeignKey(p => p.MotherId);

            #endregion

            #region Price

            modelBuilder.Entity<Price>()
                .ToTable("Price")
                .HasKey(u => u.Id);
            modelBuilder.Entity<Price>()
                .Property(u => u.Id)
                .HasColumnName("Id")
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            modelBuilder.Entity<Price>()
                .Property(u => u.Date)
                .IsRequired();
            modelBuilder.Entity<Price>()
                .Property(u => u.Name)
                .IsRequired();
            modelBuilder.Entity<Price>()
                .Property(u => u.Value)
                .IsOptional();

            #endregion

            #region Report

            #region Period

            modelBuilder.Entity<Period>()
                .ToTable("Period", "Some.Complex_Schema Name")
                .HasKey(p => p.PeriodID);
            modelBuilder.Entity<Period>()
                .Property(e => e.Name);
            modelBuilder.Entity<Period>()
                .HasMany(e => e.SummaryReports)
                .WithRequired(e => e.Period)
                .HasForeignKey(e => e.PeriodID);

            #endregion

            #region SummaryReport

            modelBuilder.Entity<SummaryReportFROMTableASExtent>()
                .ToTable("SummaryReportFROMTableASExtent")
                .HasKey(p => p.ReportID);
            modelBuilder.Entity<SummaryReportFROMTableASExtent>()
                .Property(e => e.Title);
            modelBuilder.Entity<SummaryReportFROMTableASExtent>()
                .Property(e => e.Entry);
            modelBuilder.Entity<SummaryReportFROMTableASExtent>()
                .HasRequired(t => t.Period)
                .WithMany(t => t.SummaryReports)
                .HasForeignKey(e => e.PeriodID);

            #endregion

            #region DetailReport

            modelBuilder.Entity<DetailReportWithFROM>()
                .ToTable("SELECT WORSE FROM NAMES AS Extent1", "In # Some.Complex_Schema @Name")
                .HasKey(p => p.ReportID);
            modelBuilder.Entity<DetailReportWithFROM>()
                .Property(e => e.Title);
            modelBuilder.Entity<DetailReportWithFROM>()
                .Property(e => e.Entry);
            modelBuilder.Entity<DetailReportWithFROM>()
                .Property(e => e.BadNamedColumn1).HasColumnName("I am Bad Column Name 1 ");
            modelBuilder.Entity<DetailReportWithFROM>()
                .Property(e => e.BadNamedColumn2).HasColumnName("A #_@ Abc 123 Абв$");
            modelBuilder.Entity<DetailReportWithFROM>()
                .Property(e => e.BadNamedColumn3).HasColumnName("SELECT WORSE FROM NAMES AS Extent ");
            modelBuilder.Entity<DetailReportWithFROM>()
                .HasRequired(t => t.Period)
                .WithMany(t => t.DetailReports)
                .HasForeignKey(e => e.PeriodID);
            modelBuilder.Entity<DetailReportWithFROM>()
                .HasRequired(t => t.SummaryReport)
                .WithMany()
                .HasForeignKey(e => e.SummaryReportID)
                .WillCascadeOnDelete(false);

            #endregion

            #endregion

            #region School

            #region Department

            modelBuilder.Entity<Department>()
                .ToTable("Department")
                .HasKey(p => p.DepartmentID);
            modelBuilder.Entity<Department>()
                .Property(e => e.Name);
            modelBuilder.Entity<Department>()
                .Property(e => e.Budget);
            modelBuilder.Entity<Department>()
                .Property(e => e.Administrator)
                .IsOptional();
            modelBuilder.Entity<Department>()
                .HasMany(e => e.Courses)
                .WithRequired(e => e.Department)
                .HasForeignKey(e => e.DepartmentID);

            #endregion

            #region Course

            modelBuilder.Entity<Course>()
                .ToTable("Course")
                .HasKey(p => p.CourseID);
            modelBuilder.Entity<Course>()
                .Property(e => e.Title);
            modelBuilder.Entity<Course>()
                .Property(e => e.Credits);
            modelBuilder.Entity<Course>()
                .HasMany(t => t.Instructors)
                .WithMany(t => t.Courses)
                .Map(m =>
                {
                    m.ToTable("CourseInstructor");
                    m.MapLeftKey("CourseID");
                    m.MapRightKey("InstructorID");
                });

            #endregion

            #region Instructor

            modelBuilder.Entity<Instructor>()
                .ToTable("Instructor")
                .HasKey(p => p.InstructorID);
            modelBuilder.Entity<Instructor>()
                .Property(e => e.FirstName);
            modelBuilder.Entity<Instructor>()
                .Property(e => e.LastName);
            modelBuilder.Entity<Instructor>()
                .Property(e => e.FullName)
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Computed);
            modelBuilder.Entity<Instructor>()
                .Property(e => e.HireDate);
            modelBuilder.Entity<Instructor>()
                .HasRequired(t => t.OfficeAssignment)
                .WithRequiredPrincipal(t => t.Instructor);

            #endregion

            #region OfficeAssignment

            modelBuilder.Entity<OfficeAssignment>()
                .ToTable("OfficeAssignment")
                .HasKey(p => p.InstructorID);
            modelBuilder.Entity<OfficeAssignment>()
                .Property(e => e.Location);
            modelBuilder.Entity<OfficeAssignment>()
                .Property(e => e.Timestamp)
                .IsConcurrencyToken()
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Computed);

            #endregion

            #endregion

            #region Team

            #region db generated guids

            modelBuilder.Entity<DM.Teams.UsingDbGeneratedGuidKeys.TeamWithDbGeneratedGuidKey>()
                .ToTable("TeamWithDbGeneratedGuid")
                .HasKey(p => p.Id);
            modelBuilder.Entity<DM.Teams.UsingDbGeneratedGuidKeys.TeamWithDbGeneratedGuidKey>()
                .Property(p => p.Id)
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            modelBuilder.Entity<DM.Teams.UsingDbGeneratedGuidKeys.TeamWithDbGeneratedGuidKey>()
                .Property(p => p.Name)
                .IsRequired();
            modelBuilder.Entity<DM.Teams.UsingDbGeneratedGuidKeys.TeamWithDbGeneratedGuidKey>()
                .HasMany(b => b.Players)
                .WithRequired(p => p.Team)
                .HasForeignKey(p => p.TeamId);

            modelBuilder.Entity<DM.Teams.UsingDbGeneratedGuidKeys.PlayerWithDbGeneratedGuidKey>()
                .ToTable("PlayerWithDbGeneratedGuid")
                .HasKey(p => p.Id);
            modelBuilder.Entity<DM.Teams.UsingDbGeneratedGuidKeys.PlayerWithDbGeneratedGuidKey>()
                .Property(p => p.Id)
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            modelBuilder.Entity<DM.Teams.UsingDbGeneratedGuidKeys.PlayerWithDbGeneratedGuidKey>()
                .Property(p => p.Firstname)
                .IsRequired();
            modelBuilder.Entity<DM.Teams.UsingDbGeneratedGuidKeys.PlayerWithDbGeneratedGuidKey>()
                .Property(p => p.Lastname)
                .IsRequired();


            modelBuilder.Entity<DM.Teams.UsingDbGeneratedGuidKeys.CoachWithDbGeneratedGuidKey>()
                .ToTable("CoachWithDbGeneratedGuid")
                .HasKey(p => p.Id);
            modelBuilder.Entity<DM.Teams.UsingDbGeneratedGuidKeys.CoachWithDbGeneratedGuidKey>()
                .Property(p => p.Id)
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            modelBuilder.Entity<DM.Teams.UsingDbGeneratedGuidKeys.CoachWithDbGeneratedGuidKey>()
                .Property(p => p.Firstname)
                .IsRequired();
            modelBuilder.Entity<DM.Teams.UsingDbGeneratedGuidKeys.CoachWithDbGeneratedGuidKey>()
                .Property(p => p.Lastname)
                .IsRequired();

            modelBuilder.Entity<DM.Teams.UsingDbGeneratedGuidKeys.CoachWithDbGeneratedGuidKey>()
                .HasMany(a => a.Teams)
                .WithMany()
                .Map(x =>
                {
                    x.MapLeftKey("CoachId");
                    x.MapRightKey("TeamId");
                    x.ToTable("CoachTeamsWithDbGeneratedGuid");
                });

            #endregion

            #region user generated guids

            modelBuilder.Entity<DM.Teams.UsingUserGeneratedGuidKeys.TeamWithUserGeneratedGuidKey>()
                .ToTable("TeamWithUserGeneratedGuid")
                .HasKey(p => p.Id);
            modelBuilder.Entity<DM.Teams.UsingUserGeneratedGuidKeys.TeamWithUserGeneratedGuidKey>()
                .Property(p => p.Id)
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.None);
            modelBuilder.Entity<DM.Teams.UsingUserGeneratedGuidKeys.TeamWithUserGeneratedGuidKey>()
                .Property(p => p.Name)
                .IsRequired();
            modelBuilder.Entity<DM.Teams.UsingUserGeneratedGuidKeys.TeamWithUserGeneratedGuidKey>()
                .HasMany(b => b.Players)
                .WithRequired(p => p.Team)
                .HasForeignKey(p => p.TeamId);

            modelBuilder.Entity<DM.Teams.UsingUserGeneratedGuidKeys.PlayerWithUserGeneratedGuidKey>()
                .ToTable("PlayerWithUserGeneratedGuid")
                .HasKey(p => p.Id);
            modelBuilder.Entity<DM.Teams.UsingUserGeneratedGuidKeys.PlayerWithUserGeneratedGuidKey>()
                .Property(p => p.Id)
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.None);
            modelBuilder.Entity<DM.Teams.UsingUserGeneratedGuidKeys.PlayerWithUserGeneratedGuidKey>()
                .Property(p => p.Firstname)
                .IsRequired();
            modelBuilder.Entity<DM.Teams.UsingUserGeneratedGuidKeys.PlayerWithUserGeneratedGuidKey>()
                .Property(p => p.Lastname)
                .IsRequired();


            modelBuilder.Entity<DM.Teams.UsingUserGeneratedGuidKeys.CoachWithUserGeneratedGuidKey>()
                .ToTable("CoachWithUserGeneratedGuid")
                .HasKey(p => p.Id);
            modelBuilder.Entity<DM.Teams.UsingUserGeneratedGuidKeys.CoachWithUserGeneratedGuidKey>()
                .Property(p => p.Id)
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.None);
            modelBuilder.Entity<DM.Teams.UsingUserGeneratedGuidKeys.CoachWithUserGeneratedGuidKey>()
                .Property(p => p.Firstname)
                .IsRequired();
            modelBuilder.Entity<DM.Teams.UsingUserGeneratedGuidKeys.CoachWithUserGeneratedGuidKey>()
                .Property(p => p.Lastname)
                .IsRequired();

            modelBuilder.Entity<DM.Teams.UsingUserGeneratedGuidKeys.CoachWithUserGeneratedGuidKey>()
                .HasMany(a => a.Teams)
                .WithMany()
                .Map(x =>
                {
                    x.MapLeftKey("CoachId");
                    x.MapRightKey("TeamId");
                    x.ToTable("CoachTeamsWithUserGeneratedGuid");
                });

            #endregion

            #endregion

            #region Logs

            modelBuilder.Entity<LogItem>()
                .ToTable("LogItem")
                .HasKey(u => u.Id);
            modelBuilder.Entity<LogItem>()
                .Property(u => u.Id)
                .HasColumnName("Id")
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            modelBuilder.Entity<LogItem>()
                .Property(u => u.Message);
            modelBuilder.Entity<LogItem>()
                .Property(u => u.Timestamp);
            modelBuilder.Entity<LogWarning>()
                .Property(u => u.Recommendation);
            modelBuilder.Entity<LogError>()
                .Property(u => u.Severity);
            modelBuilder.Entity<LogItem>()
                .Map<LogWarning>(m => m.Requires("LogType").HasValue("Warning"))
                .Map<LogError>(m => m.Requires("LogType").HasValue("Error"));

            #endregion
        }
    }
}