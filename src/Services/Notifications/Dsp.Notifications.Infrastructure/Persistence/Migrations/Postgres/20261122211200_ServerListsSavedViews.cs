using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dsp.Notifications.Infrastructure.Persistence.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class ServerListsSavedViews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Body",
                schema: "notifications",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "BodyAr",
                schema: "notifications",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "Title",
                schema: "notifications",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "TitleAr",
                schema: "notifications",
                table: "Notifications");

            migrationBuilder.AlterColumn<string>(
                name: "TitleMessage",
                schema: "notifications",
                table: "Notifications",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BodyMessage",
                schema: "notifications",
                table: "Notifications",
                type: "character varying(8000)",
                maxLength: 8000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(8000)",
                oldMaxLength: 8000,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "TitleMessage",
                schema: "notifications",
                table: "Notifications",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000);

            migrationBuilder.AlterColumn<string>(
                name: "BodyMessage",
                schema: "notifications",
                table: "Notifications",
                type: "character varying(8000)",
                maxLength: 8000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(8000)",
                oldMaxLength: 8000);

            migrationBuilder.AddColumn<string>(
                name: "Body",
                schema: "notifications",
                table: "Notifications",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BodyAr",
                schema: "notifications",
                table: "Notifications",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                schema: "notifications",
                table: "Notifications",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TitleAr",
                schema: "notifications",
                table: "Notifications",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);
        }
    }
}
