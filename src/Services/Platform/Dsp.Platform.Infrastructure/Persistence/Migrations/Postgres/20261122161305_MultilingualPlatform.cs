using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dsp.Platform.Infrastructure.Persistence.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class MultilingualPlatform : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "platform");

            migrationBuilder.CreateTable(
                name: "AiSettings",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BaseUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProtectedApiKey = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Temperature = table.Column<double>(type: "double precision", nullable: false),
                    TimeoutSeconds = table.Column<int>(type: "integer", nullable: false),
                    ChatEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ReviewEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    MaxToolIterations = table.Column<int>(type: "integer", nullable: false),
                    MaxHistoryMessages = table.Column<int>(type: "integer", nullable: false),
                    ChatInstructions = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    ReviewInstructions = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Languages",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: false),
                    Name = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Direction = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Languages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Translations",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LanguageCode = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: false),
                    Text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Translations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Languages_Code",
                schema: "platform",
                table: "Languages",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Translations_Key_LanguageCode",
                schema: "platform",
                table: "Translations",
                columns: new[] { "Key", "LanguageCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Translations_LanguageCode",
                schema: "platform",
                table: "Translations",
                column: "LanguageCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiSettings",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "Languages",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "Translations",
                schema: "platform");
        }
    }
}
