using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dsp.EServices.Infrastructure.Persistence.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class ServerListsSavedViews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Applications_ApplicantId",
                schema: "eservices",
                table: "Applications");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_ApplicantId_UpdatedAt",
                schema: "eservices",
                table: "Applications",
                columns: new[] { "ApplicantId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Applications_Status_UpdatedAt",
                schema: "eservices",
                table: "Applications",
                columns: new[] { "Status", "UpdatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Applications_ApplicantId_UpdatedAt",
                schema: "eservices",
                table: "Applications");

            migrationBuilder.DropIndex(
                name: "IX_Applications_Status_UpdatedAt",
                schema: "eservices",
                table: "Applications");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_ApplicantId",
                schema: "eservices",
                table: "Applications",
                column: "ApplicantId");
        }
    }
}
