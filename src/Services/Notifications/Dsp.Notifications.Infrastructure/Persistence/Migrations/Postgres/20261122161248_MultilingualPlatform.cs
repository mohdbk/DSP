using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dsp.Notifications.Infrastructure.Persistence.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class MultilingualPlatform : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BodyMessage",
                schema: "notifications",
                table: "Notifications",
                type: "character varying(8000)",
                maxLength: 8000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TitleMessage",
                schema: "notifications",
                table: "Notifications",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BodyMessage",
                schema: "notifications",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "TitleMessage",
                schema: "notifications",
                table: "Notifications");
        }
    }
}
