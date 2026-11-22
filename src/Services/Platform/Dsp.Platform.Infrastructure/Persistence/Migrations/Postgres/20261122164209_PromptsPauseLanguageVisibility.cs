using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dsp.Platform.Infrastructure.Persistence.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class PromptsPauseLanguageVisibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChatEnabled",
                schema: "platform",
                table: "AiSettings");

            migrationBuilder.RenameColumn(
                name: "ReviewEnabled",
                schema: "platform",
                table: "AiSettings",
                newName: "Paused");

            migrationBuilder.AddColumn<bool>(
                name: "HiddenInBackoffice",
                schema: "platform",
                table: "Languages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HiddenInPortal",
                schema: "platform",
                table: "Languages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ChatPrompt",
                schema: "platform",
                table: "AiSettings",
                type: "character varying(8000)",
                maxLength: 8000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewPrompt",
                schema: "platform",
                table: "AiSettings",
                type: "character varying(8000)",
                maxLength: 8000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HiddenInBackoffice",
                schema: "platform",
                table: "Languages");

            migrationBuilder.DropColumn(
                name: "HiddenInPortal",
                schema: "platform",
                table: "Languages");

            migrationBuilder.DropColumn(
                name: "ChatPrompt",
                schema: "platform",
                table: "AiSettings");

            migrationBuilder.DropColumn(
                name: "ReviewPrompt",
                schema: "platform",
                table: "AiSettings");

            migrationBuilder.RenameColumn(
                name: "Paused",
                schema: "platform",
                table: "AiSettings",
                newName: "ReviewEnabled");

            migrationBuilder.AddColumn<bool>(
                name: "ChatEnabled",
                schema: "platform",
                table: "AiSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
