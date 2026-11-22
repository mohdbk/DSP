using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dsp.EServices.Infrastructure.Persistence.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class ServiceBuilderAgenticReviewArabic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AiRecommendation",
                schema: "eservices",
                table: "ReviewTasks",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InformationRequestCount",
                schema: "eservices",
                table: "Applications",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "InformationRequestJson",
                schema: "eservices",
                table: "Applications",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ServiceVersion",
                schema: "eservices",
                table: "Applications",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "AiAssessments",
                schema: "eservices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Round = table.Column<int>(type: "int", nullable: false),
                    Recommendation = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    FindingsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    QuestionsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiAssessments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ServiceVersions",
                schema: "eservices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DefinitionJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PublishedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceVersions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiAssessments_ApplicationId_Round",
                schema: "eservices",
                table: "AiAssessments",
                columns: new[] { "ApplicationId", "Round" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceVersions_Code_Status",
                schema: "eservices",
                table: "ServiceVersions",
                columns: new[] { "Code", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceVersions_Code_Version",
                schema: "eservices",
                table: "ServiceVersions",
                columns: new[] { "Code", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiAssessments",
                schema: "eservices");

            migrationBuilder.DropTable(
                name: "ServiceVersions",
                schema: "eservices");

            migrationBuilder.DropColumn(
                name: "AiRecommendation",
                schema: "eservices",
                table: "ReviewTasks");

            migrationBuilder.DropColumn(
                name: "InformationRequestCount",
                schema: "eservices",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "InformationRequestJson",
                schema: "eservices",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "ServiceVersion",
                schema: "eservices",
                table: "Applications");
        }
    }
}
