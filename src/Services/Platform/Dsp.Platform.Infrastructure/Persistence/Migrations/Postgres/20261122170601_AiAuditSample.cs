using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dsp.Platform.Infrastructure.Persistence.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AiAuditSample : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AuditSamplePercent",
                schema: "platform",
                table: "AiSettings",
                type: "integer",
                nullable: false,
                defaultValue: 10); // existing settings start with a 10% audit sample
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AuditSamplePercent",
                schema: "platform",
                table: "AiSettings");
        }
    }
}
