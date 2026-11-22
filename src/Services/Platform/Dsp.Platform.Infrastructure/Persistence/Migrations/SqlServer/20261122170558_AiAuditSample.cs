using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dsp.Platform.Infrastructure.Persistence.Migrations.SqlServer
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
                type: "int",
                nullable: false,
                defaultValue: 0);
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
