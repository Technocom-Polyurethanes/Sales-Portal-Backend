using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using sales_portal.infrastructure.Contexts;

#nullable disable

namespace sales_portal.infrastructure.Migrations
{
    /// <summary>Indexes for the columns the offer list filters and sorts by.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261120100000_AddOfferListIndexes")]
    public partial class AddOfferListIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_offers_status",
                table: "offers",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_offers_updated_at",
                table: "offers",
                column: "updated_at");

            migrationBuilder.CreateIndex(
                name: "ix_offers_created_at",
                table: "offers",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_offers_issue_date",
                table: "offers",
                column: "issue_date");

            migrationBuilder.CreateIndex(
                name: "ix_offers_serial_number",
                table: "offers",
                column: "serial_number");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_offers_status",
                table: "offers");

            migrationBuilder.DropIndex(
                name: "ix_offers_updated_at",
                table: "offers");

            migrationBuilder.DropIndex(
                name: "ix_offers_created_at",
                table: "offers");

            migrationBuilder.DropIndex(
                name: "ix_offers_issue_date",
                table: "offers");

            migrationBuilder.DropIndex(
                name: "ix_offers_serial_number",
                table: "offers");
        }
    }
}
