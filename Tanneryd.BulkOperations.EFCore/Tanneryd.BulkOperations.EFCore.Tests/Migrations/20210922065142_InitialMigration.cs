using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Tanneryd.BulkOperations.EFCore.Tests.Migrations
{
    public partial class InitialMigration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.CreateTable(
                name: "BatchInvoice",
                schema: "dbo",
                columns: table => new
                {
                    PrimaryKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dbo.BatchInvoice", x => x.PrimaryKey)
                        .Annotation("SqlServer:Clustered", true);
                });

            migrationBuilder.CreateTable(
                name: "Blog",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dbo.Blog", x => x.Id)
                        .Annotation("SqlServer:Clustered", true);
                });

            migrationBuilder.CreateTable(
                name: "CoachWithDbGeneratedGuid",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Firstname = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Lastname = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dbo.CoachWithDbGeneratedGuid", x => x.Id)
                        .Annotation("SqlServer:Clustered", true);
                });

            migrationBuilder.CreateTable(
                name: "CoachWithUserGeneratedGuid",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Firstname = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Lastname = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dbo.CoachWithUserGeneratedGuid", x => x.Id)
                        .Annotation("SqlServer:Clustered", true);
                });

            migrationBuilder.CreateTable(
                name: "Company",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ParentCompanyId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dbo.Company", x => x.Id)
                        .Annotation("SqlServer:Clustered", true);
                    table.ForeignKey(
                        name: "FK_dbo.Company_dbo.Company_ParentCompanyId",
                        column: x => x.ParentCompanyId,
                        principalSchema: "dbo",
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Coordinate",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Value = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dbo.Coordinate", x => x.Id)
                        .Annotation("SqlServer:Clustered", true);
                });

            migrationBuilder.CreateTable(
                name: "Department",
                schema: "dbo",
                columns: table => new
                {
                    DepartmentID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Budget = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Administrator = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dbo.Department", x => x.DepartmentID)
                        .Annotation("SqlServer:Clustered", true);
                });

            migrationBuilder.CreateTable(
                name: "EmptyTable",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dbo.EmptyTable", x => x.Id)
                        .Annotation("SqlServer:Clustered", true);
                });

            migrationBuilder.CreateTable(
                name: "Instructor",
                schema: "dbo",
                columns: table => new
                {
                    InstructorID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LastName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FirstName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HireDate = table.Column<DateTime>(type: "datetime", nullable: false),
                    //FullName = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dbo.Instructor", x => x.InstructorID)
                        .Annotation("SqlServer:Clustered", true);
                });
            // This replaces the out commented line for FullName in the create table above.
            migrationBuilder.Sql("ALTER TABLE dbo.Instructor ADD FullName AS (FirstName + ' ' + LastName) PERSISTED NOT NULL");

            migrationBuilder.CreateTable(
                name: "Invoice",
                schema: "dbo",
                columns: table => new
                {
                    PrimaryKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Net = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Gross = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    //Tax = table.Column<decimal>(type: "decimal(19,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dbo.Invoice", x => x.PrimaryKey)
                        .Annotation("SqlServer:Clustered", true);
                });
            // This replaces the out commented line for Tax in the create table above.
            migrationBuilder.Sql("ALTER TABLE dbo.Invoice ADD Tax AS (Gross - Net) PERSISTED NOT NULL");

            migrationBuilder.CreateTable(
                name: "Journal",
                schema: "dbo",
                columns: table => new
                {
                    PrimaryKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dbo.Journal", x => x.PrimaryKey)
                        .Annotation("SqlServer:Clustered", true);
                });

            migrationBuilder.CreateTable(
                name: "LogItem",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "datetime", nullable: false),
                    Severity = table.Column<int>(type: "int", nullable: true),
                    Recommendation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LogType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dbo.LogItem", x => x.Id)
                        .Annotation("SqlServer:Clustered", true);
                });

            migrationBuilder.CreateTable(
                name: "Parity",
                schema: "dbo",
                columns: table => new
                {
                    Key = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dbo.Parity", x => x.Key)
                        .Annotation("SqlServer:Clustered", true);
                });

            migrationBuilder.CreateTable(
                name: "Person",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FirstName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BirthDate = table.Column<DateTime>(type: "datetime", nullable: false),
                    MotherId = table.Column<long>(type: "bigint", nullable: true),
                    EmployeeNumber = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dbo.Person", x => x.Id)
                        .Annotation("SqlServer:Clustered", true);
                    table.ForeignKey(
                        name: "FK_dbo.Person_dbo.Person_MotherId",
                        column: x => x.MotherId,
                        principalSchema: "dbo",
                        principalTable: "Person",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Price",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateTime>(type: "datetime", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dbo.Price", x => x.Id)
                        .Annotation("SqlServer:Clustered", true);
                });

            migrationBuilder.CreateTable(
                name: "ReservedSqlKeyword",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<int>(type: "int", nullable: true),
                    Identity = table.Column<int>(type: "int", nullable: true),
                    Select = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dbo.ReservedSqlKeyword", x => x.Id)
                        .Annotation("SqlServer:Clustered", true);
                });

            migrationBuilder.CreateTable(
                name: "SummaryReportFROMTableASExtent",
                schema: "dbo",
                columns: table => new
                {
                    ReportID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Entry = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Volume = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PeriodID = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dbo.SummaryReportFROMTableASExtent", x => x.ReportID)
                        .Annotation("SqlServer:Clustered", true);
                });

            migrationBuilder.CreateTable(
                name: "TeamWithDbGeneratedGuid",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dbo.TeamWithDbGeneratedGuid", x => x.Id)
                        .Annotation("SqlServer:Clustered", true);
                });

            migrationBuilder.CreateTable(
                name: "TeamWithUserGeneratedGuid",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dbo.TeamWithUserGeneratedGuid", x => x.Id)
                        .Annotation("SqlServer:Clustered", true);
                });

            migrationBuilder.CreateTable(
                name: "Visitor",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dbo.Visitor", x => x.Id)
                        .Annotation("SqlServer:Clustered", true);
                });

            migrationBuilder.CreateTable(
                name: "Post",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BlogId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dbo.Post", x => x.Id)
                        .Annotation("SqlServer:Clustered", true);
                    table.ForeignKey(
                        name: "FK_dbo.Post_dbo.Blog_BlogId",
                        column: x => x.BlogId,
                        principalSchema: "dbo",
                        principalTable: "Blog",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Employee",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EmployerId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dbo.Employee", x => x.Id)
                        .Annotation("SqlServer:Clustered", true);
                    table.ForeignKey(
                        name: "FK_dbo.Employee_dbo.Company_EmployerId",
                        column: x => x.EmployerId,
                        principalSchema: "dbo",
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Point",
                schema: "dbo",
                columns: table => new
                {
                    XCoordinateId = table.Column<int>(type: "int", nullable: false),
                    YCoordinateId = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dbo.Point", x => new { x.XCoordinateId, x.YCoordinateId })
                        .Annotation("SqlServer:Clustered", true);
                    table.ForeignKey(
                        name: "FK_dbo.Point_dbo.Coordinate_XCoordinateId",
                        column: x => x.XCoordinateId,
                        principalSchema: "dbo",
                        principalTable: "Coordinate",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_dbo.Point_dbo.Coordinate_YCoordinateId",
                        column: x => x.YCoordinateId,
                        principalSchema: "dbo",
                        principalTable: "Coordinate",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Course",
                schema: "dbo",
                columns: table => new
                {
                    CourseID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Credits = table.Column<int>(type: "int", nullable: false),
                    DepartmentID = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dbo.Course", x => x.CourseID)
                        .Annotation("SqlServer:Clustered", true);
                    table.ForeignKey(
                        name: "FK_dbo.Course_dbo.Department_DepartmentID",
                        column: x => x.DepartmentID,
                        principalSchema: "dbo",
                        principalTable: "Department",
                        principalColumn: "DepartmentID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OfficeAssignment",
                schema: "dbo",
                columns: table => new
                {
                    InstructorID = table.Column<int>(type: "int", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Timestamp = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dbo.OfficeAssignment", x => x.InstructorID)
                        .Annotation("SqlServer:Clustered", true);
                    table.ForeignKey(
                        name: "FK_dbo.OfficeAssignment_dbo.Instructor_InstructorID",
                        column: x => x.InstructorID,
                        principalSchema: "dbo",
                        principalTable: "Instructor",
                        principalColumn: "InstructorID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BatchInvoiceItem",
                schema: "dbo",
                columns: table => new
                {
                    PrimaryKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BatchInvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dbo.BatchInvoiceItem", x => x.PrimaryKey)
                        .Annotation("SqlServer:Clustered", true);
                    table.ForeignKey(
                        name: "FK_dbo.BatchInvoiceItem_dbo.BatchInvoice_BatchInvoiceId",
                        column: x => x.BatchInvoiceId,
                        principalSchema: "dbo",
                        principalTable: "BatchInvoice",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_dbo.BatchInvoiceItem_dbo.Invoice_InvoiceId",
                        column: x => x.InvoiceId,
                        principalSchema: "dbo",
                        principalTable: "Invoice",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceItem",
                schema: "dbo",
                columns: table => new
                {
                    PrimaryKey = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JournalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dbo.InvoiceItem", x => x.PrimaryKey)
                        .Annotation("SqlServer:Clustered", true);
                    table.ForeignKey(
                        name: "FK_dbo.InvoiceItem_dbo.Invoice_InvoiceId",
                        column: x => x.InvoiceId,
                        principalSchema: "dbo",
                        principalTable: "Invoice",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_dbo.InvoiceItem_dbo.Journal_JournalId",
                        column: x => x.JournalId,
                        principalSchema: "dbo",
                        principalTable: "Journal",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Number",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Value = table.Column<long>(type: "bigint", nullable: false),
                    ParityId = table.Column<int>(type: "int", nullable: false),
                    PrimeId = table.Column<int>(type: "int", nullable: true),
                    CompositeId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dbo.Number", x => x.Id)
                        .Annotation("SqlServer:Clustered", true);
                    table.ForeignKey(
                        name: "FK_dbo.Number_dbo.Parity_ParityId",
                        column: x => x.ParityId,
                        principalSchema: "dbo",
                        principalTable: "Parity",
                        principalColumn: "Key",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CoachTeamsWithDbGeneratedGuid",
                schema: "dbo",
                columns: table => new
                {
                    CoachId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dbo.CoachTeamsWithDbGeneratedGuid", x => new { x.CoachId, x.TeamId })
                        .Annotation("SqlServer:Clustered", true);
                    table.ForeignKey(
                        name: "FK_dbo.CoachTeamsWithDbGeneratedGuid_dbo.CoachWithDbGeneratedGuid_CoachId",
                        column: x => x.CoachId,
                        principalSchema: "dbo",
                        principalTable: "CoachWithDbGeneratedGuid",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_dbo.CoachTeamsWithDbGeneratedGuid_dbo.TeamWithDbGeneratedGuid_TeamId",
                        column: x => x.TeamId,
                        principalSchema: "dbo",
                        principalTable: "TeamWithDbGeneratedGuid",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlayerWithDbGeneratedGuid",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Firstname = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Lastname = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dbo.PlayerWithDbGeneratedGuid", x => x.Id)
                        .Annotation("SqlServer:Clustered", true);
                    table.ForeignKey(
                        name: "FK_dbo.PlayerWithDbGeneratedGuid_dbo.TeamWithDbGeneratedGuid_TeamId",
                        column: x => x.TeamId,
                        principalSchema: "dbo",
                        principalTable: "TeamWithDbGeneratedGuid",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CoachTeamsWithUserGeneratedGuid",
                schema: "dbo",
                columns: table => new
                {
                    CoachId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dbo.CoachTeamsWithUserGeneratedGuid", x => new { x.CoachId, x.TeamId })
                        .Annotation("SqlServer:Clustered", true);
                    table.ForeignKey(
                        name: "FK_dbo.CoachTeamsWithUserGeneratedGuid_dbo.CoachWithUserGeneratedGuid_CoachId",
                        column: x => x.CoachId,
                        principalSchema: "dbo",
                        principalTable: "CoachWithUserGeneratedGuid",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_dbo.CoachTeamsWithUserGeneratedGuid_dbo.TeamWithUserGeneratedGuid_TeamId",
                        column: x => x.TeamId,
                        principalSchema: "dbo",
                        principalTable: "TeamWithUserGeneratedGuid",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlayerWithUserGeneratedGuid",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Firstname = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Lastname = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dbo.PlayerWithUserGeneratedGuid", x => x.Id)
                        .Annotation("SqlServer:Clustered", true);
                    table.ForeignKey(
                        name: "FK_dbo.PlayerWithUserGeneratedGuid_dbo.TeamWithUserGeneratedGuid_TeamId",
                        column: x => x.TeamId,
                        principalSchema: "dbo",
                        principalTable: "TeamWithUserGeneratedGuid",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Keyword",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PostId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dbo.Keyword", x => x.Id)
                        .Annotation("SqlServer:Clustered", true);
                    table.ForeignKey(
                        name: "FK_dbo.Keyword_dbo.Post_PostId",
                        column: x => x.PostId,
                        principalSchema: "dbo",
                        principalTable: "Post",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PostVisitor",
                schema: "dbo",
                columns: table => new
                {
                    PostsId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VisitorsId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostVisitor", x => new { x.PostsId, x.VisitorsId });
                    table.ForeignKey(
                        name: "FK_PostVisitor_Post_PostsId",
                        column: x => x.PostsId,
                        principalSchema: "dbo",
                        principalTable: "Post",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PostVisitor_Visitor_VisitorsId",
                        column: x => x.VisitorsId,
                        principalSchema: "dbo",
                        principalTable: "Visitor",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VisitorPosts",
                schema: "dbo",
                columns: table => new
                {
                    VisitorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PostId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dbo.VisitorPosts", x => new { x.VisitorId, x.PostId })
                        .Annotation("SqlServer:Clustered", true);
                    table.ForeignKey(
                        name: "FK_dbo.VisitorPosts_dbo.Post_PostId",
                        column: x => x.PostId,
                        principalSchema: "dbo",
                        principalTable: "Post",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_dbo.VisitorPosts_dbo.Visitor_VisitorId",
                        column: x => x.VisitorId,
                        principalSchema: "dbo",
                        principalTable: "Visitor",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CourseInstructor",
                schema: "dbo",
                columns: table => new
                {
                    CourseID = table.Column<int>(type: "int", nullable: false),
                    InstructorID = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dbo.CourseInstructor", x => new { x.CourseID, x.InstructorID })
                        .Annotation("SqlServer:Clustered", true);
                    table.ForeignKey(
                        name: "FK_dbo.CourseInstructor_dbo.Course_CourseID",
                        column: x => x.CourseID,
                        principalSchema: "dbo",
                        principalTable: "Course",
                        principalColumn: "CourseID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_dbo.CourseInstructor_dbo.Instructor_InstructorID",
                        column: x => x.InstructorID,
                        principalSchema: "dbo",
                        principalTable: "Instructor",
                        principalColumn: "InstructorID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Composite",
                schema: "dbo",
                columns: table => new
                {
                    NumberId = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dbo.Composite", x => x.NumberId)
                        .Annotation("SqlServer:Clustered", true);
                    table.ForeignKey(
                        name: "FK_dbo.Composite_dbo.Number_NumberId",
                        column: x => x.NumberId,
                        principalSchema: "dbo",
                        principalTable: "Number",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Prime",
                schema: "dbo",
                columns: table => new
                {
                    NumberId = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dbo.Prime", x => x.NumberId)
                        .Annotation("SqlServer:Clustered", true);
                    table.ForeignKey(
                        name: "FK_dbo.Prime_dbo.Number_NumberId",
                        column: x => x.NumberId,
                        principalSchema: "dbo",
                        principalTable: "Number",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CompositePrime",
                schema: "dbo",
                columns: table => new
                {
                    CompositeId = table.Column<long>(type: "bigint", nullable: false),
                    PrimeId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dbo.CompositePrime", x => new { x.CompositeId, x.PrimeId })
                        .Annotation("SqlServer:Clustered", true);
                    table.ForeignKey(
                        name: "FK_dbo.CompositePrime_dbo.Composite_CompositeId",
                        column: x => x.CompositeId,
                        principalSchema: "dbo",
                        principalTable: "Composite",
                        principalColumn: "NumberId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_dbo.CompositePrime_dbo.Prime_PrimeId",
                        column: x => x.PrimeId,
                        principalSchema: "dbo",
                        principalTable: "Prime",
                        principalColumn: "NumberId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BatchInvoiceId",
                schema: "dbo",
                table: "BatchInvoiceItem",
                column: "BatchInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceId",
                schema: "dbo",
                table: "BatchInvoiceItem",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_CoachId",
                schema: "dbo",
                table: "CoachTeamsWithDbGeneratedGuid",
                column: "CoachId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamId",
                schema: "dbo",
                table: "CoachTeamsWithDbGeneratedGuid",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_CoachId",
                schema: "dbo",
                table: "CoachTeamsWithUserGeneratedGuid",
                column: "CoachId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamId",
                schema: "dbo",
                table: "CoachTeamsWithUserGeneratedGuid",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_ParentCompanyId",
                schema: "dbo",
                table: "Company",
                column: "ParentCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_NumberId",
                schema: "dbo",
                table: "Composite",
                column: "NumberId");

            migrationBuilder.CreateIndex(
                name: "IX_CompositeId",
                schema: "dbo",
                table: "CompositePrime",
                column: "CompositeId");

            migrationBuilder.CreateIndex(
                name: "IX_PrimeId",
                schema: "dbo",
                table: "CompositePrime",
                column: "PrimeId");

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentID",
                schema: "dbo",
                table: "Course",
                column: "DepartmentID");

            migrationBuilder.CreateIndex(
                name: "IX_CourseID",
                schema: "dbo",
                table: "CourseInstructor",
                column: "CourseID");

            migrationBuilder.CreateIndex(
                name: "IX_InstructorID",
                schema: "dbo",
                table: "CourseInstructor",
                column: "InstructorID");

            migrationBuilder.CreateIndex(
                name: "IX_EmployerId",
                schema: "dbo",
                table: "Employee",
                column: "EmployerId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceId",
                schema: "dbo",
                table: "InvoiceItem",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalId",
                schema: "dbo",
                table: "InvoiceItem",
                column: "JournalId");

            migrationBuilder.CreateIndex(
                name: "IX_PostId",
                schema: "dbo",
                table: "Keyword",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_ParityId",
                schema: "dbo",
                table: "Number",
                column: "ParityId");

            migrationBuilder.CreateIndex(
                name: "IX_InstructorID",
                schema: "dbo",
                table: "OfficeAssignment",
                column: "InstructorID");

            migrationBuilder.CreateIndex(
                name: "IX_MotherId",
                schema: "dbo",
                table: "Person",
                column: "MotherId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamId",
                schema: "dbo",
                table: "PlayerWithDbGeneratedGuid",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamId",
                schema: "dbo",
                table: "PlayerWithUserGeneratedGuid",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_XCoordinateId",
                schema: "dbo",
                table: "Point",
                column: "XCoordinateId");

            migrationBuilder.CreateIndex(
                name: "IX_YCoordinateId",
                schema: "dbo",
                table: "Point",
                column: "YCoordinateId");

            migrationBuilder.CreateIndex(
                name: "IX_BlogId",
                schema: "dbo",
                table: "Post",
                column: "BlogId");

            migrationBuilder.CreateIndex(
                name: "IX_PostVisitor_VisitorsId",
                schema: "dbo",
                table: "PostVisitor",
                column: "VisitorsId");

            migrationBuilder.CreateIndex(
                name: "IX_NumberId",
                schema: "dbo",
                table: "Prime",
                column: "NumberId");

            migrationBuilder.CreateIndex(
                name: "IX_PeriodID",
                schema: "dbo",
                table: "SummaryReportFROMTableASExtent",
                column: "PeriodID");

            migrationBuilder.CreateIndex(
                name: "IX_PostId",
                schema: "dbo",
                table: "VisitorPosts",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_VisitorId",
                schema: "dbo",
                table: "VisitorPosts",
                column: "VisitorId");

            migrationBuilder.Sql("CREATE VIEW Contact AS SELECT FirstName, LastName FROM Person");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BatchInvoiceItem",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "CoachTeamsWithDbGeneratedGuid",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "CoachTeamsWithUserGeneratedGuid",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "CompositePrime",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "CourseInstructor",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Employee",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "EmptyTable",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "InvoiceItem",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Keyword",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LogItem",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "OfficeAssignment",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Person",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PlayerWithDbGeneratedGuid",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PlayerWithUserGeneratedGuid",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Point",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PostVisitor",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Price",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ReservedSqlKeyword",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "SummaryReportFROMTableASExtent",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "VisitorPosts",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "BatchInvoice",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "CoachWithDbGeneratedGuid",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "CoachWithUserGeneratedGuid",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Composite",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Prime",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Course",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Company",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Invoice",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Journal",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Instructor",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TeamWithDbGeneratedGuid",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TeamWithUserGeneratedGuid",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Coordinate",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Post",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Visitor",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Number",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Department",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Blog",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Parity",
                schema: "dbo");
        }
    }
}
