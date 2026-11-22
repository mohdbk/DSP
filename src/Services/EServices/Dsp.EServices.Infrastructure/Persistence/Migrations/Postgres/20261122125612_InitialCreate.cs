using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dsp.EServices.Infrastructure.Persistence.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "eservices");

            migrationBuilder.CreateTable(
                name: "Applications",
                schema: "eservices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ServiceCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ServiceName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    WorkflowVersion = table.Column<int>(type: "integer", nullable: false),
                    ApplicantId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ApplicantName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DataJson = table.Column<string>(type: "text", nullable: false),
                    ReferenceValue = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Fee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Approved = table.Column<bool>(type: "boolean", nullable: true),
                    DecisionComment = table.Column<string>(type: "text", nullable: true),
                    DecidedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CertificateDocumentId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Applications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InboxMessages",
                schema: "eservices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                schema: "eservices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReviewTasks",
                schema: "eservices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ServiceCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ServiceName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ApplicantName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ReferenceValue = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewTasks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Applications_ApplicantId",
                schema: "eservices",
                table: "Applications",
                column: "ApplicantId");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_Number",
                schema: "eservices",
                table: "Applications",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Applications_ServiceCode_ReferenceValue",
                schema: "eservices",
                table: "Applications",
                columns: new[] { "ServiceCode", "ReferenceValue" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_PublishedAt_OccurredAt",
                schema: "eservices",
                table: "OutboxMessages",
                columns: new[] { "PublishedAt", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewTasks_ApplicationId",
                schema: "eservices",
                table: "ReviewTasks",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewTasks_Status_CreatedAt",
                schema: "eservices",
                table: "ReviewTasks",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Applications",
                schema: "eservices");

            migrationBuilder.DropTable(
                name: "InboxMessages",
                schema: "eservices");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "eservices");

            migrationBuilder.DropTable(
                name: "ReviewTasks",
                schema: "eservices");
        }
    }
}
