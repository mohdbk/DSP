using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dsp.Notifications.Infrastructure.Persistence.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class ServiceBuilderAgenticReviewArabic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BodyAr",
                schema: "notifications",
                table: "Notifications",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TitleAr",
                schema: "notifications",
                table: "Notifications",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BodyAr",
                schema: "notifications",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "TitleAr",
                schema: "notifications",
                table: "Notifications");
        }
    }
}
