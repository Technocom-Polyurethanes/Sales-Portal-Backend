using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace sales_portal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditUserAgent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("SET LOCAL lock_timeout = '5s'; SET LOCAL statement_timeout = '30s';");

            migrationBuilder.AddColumn<int>(
                name: "duration_ms",
                table: "audit_logs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "user_agent",
                table: "audit_logs",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "duration_ms",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "user_agent",
                table: "audit_logs");
        }
    }
}
